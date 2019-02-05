using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Threading;

namespace DistanceBetween
{

    /// <summary>
    /// Instead of tree for hash collisions which are going to be annoying to make threadsafe
    /// Can we do a geometrically series hash tables... , sized 2^10,2^9,2^8,2^7,2^6,2^5,2^4,2^3 which is always twice the specified size.
    /// So collision in level 1, rehash the index, remask for level2, collision, rehash the index, remask for level3, etc.
    /// but this doesnt prevent collisions, just shares memory space for collisions in multiple slots in level above it
    ///   ... do we really need to have the data always available?  We expect cache misses don't we?
    ///   ... so why not add the latest one, and overwrite the oldest?
    ///   ... what are the chances of 5 consecutive collisions, when the collision space is supposed to be garbage collected...
    ///   perfect operation means
    ///   [A B C D E F G H] level1 hashtble of weak references
    ///   [A B C D E F G H] strong references table shows the latest objects inserted
    ///   [I J K L] level 2 all weak references collected bc they don't exist in strong references table
    ///   [M N] level 3 all weak references collected (above)
    ///   worst case means 
    ///   [A B C D * * * *] level1 hashtble of weak references, but multiple hash collisions push storage into high levels, where shared slots with collision overflow with other slots are also incurring high number of collisions.
    ///   [A B C D E F G H] strong references table shows the latest objects inserted
    ///   [E G I J] level 2, slot 1 and 2, all have strong references still bc of multiple collisions
    ///   [F H] level 3,slot 1 and 2, all have strong references still bc of multiple collisions
    ///   What do we do in worst case?  
    ///     We offer no guarantees the object you inserted before is still there, when you try to get it.
    ///     Depending on the memory pressure and timing of trying to get the data, it may or may nor appear
    ///     So we just assume the later record is more valuable and overwrite the oldest record in the hashslot
    ///   Improvement... The file caching in Windows seems excellent.  With SSD, and corei5-4series repeated reads can reduce read times to 10% of what they were.
    ///   At best, re-reading the same file with this caching the resulting object, results anywhere from 100% worse with completely random, multi-threaded file reads and a cache 10x smaller than number of files read
    ///     but when single threaded random reads, it seems to reduce read time in half, strangely not in proportion to size of cache to files
    ///     but in multi-threaded, sorted so repeated reads of same file are close together, it improves 10x from reading from file system.
    /// </summary>
    public class FixedSizeObjectCache
    {
        static int DEFAULT_LEVELS = 13;
        static int DEFAULT_HASHTABLE_SIZE = 1 << DEFAULT_LEVELS;
        static int DEFAULT_CACHE_SIZE = 1 << DEFAULT_LEVELS;
        static HashSlot[][] createCascadeHashtable(int size)
        {
            var jagged = new HashSlot[size][];
            for (int i = 0; i < size; i++)
                jagged[i] = new HashSlot[1 << (size - i)];
            return jagged;
        }
        static int collisionBuffersToMask(int collisionbuffercount)
        {
            return collisionBuffersToMaxHashtableSize(collisionbuffercount) - 1;
        }
        static int collisionBuffersToMaxHashtableSize(int collisionbuffercount)
        {
            return 1 << collisionbuffercount;
        }


        byte _masksize = 13; // 1<<13=8192   //3k x 10,000 = 30MB
        public byte MaskSize
        {
            get { return _masksize; }
            set
            {
                _masksize = value;
                var size = 1 << value;
                if (size != _cache.Length)
                {
                    _cache = createCascadeHashtable(value);
                    _expiration = new object[this.MaxCacheSize];
                    _nextexpire = 0;
                }
            }
        }
        public int MaxCacheSize { get { return 1 << _masksize; } }
        public int MaxHashSize { get { return 1 << _masksize; } }
        public int HashMask { get { return MaxCacheSize - 1; } }


        HashSlot[][] _cache = createCascadeHashtable(DEFAULT_LEVELS); //new HashSlot[DEFAULT_LEVELS][];
        object[] _expiration = new object[DEFAULT_CACHE_SIZE];
        int _nextexpire = 0;



        int _missbccollected = 0;
        int _hit = 0;
        int _miss = 0;
        int _max = 0;
        public object GetFromObjectCache(string filename)
        {
            var cache = _cache;
            var hash = filename.GetHashCode();
            //var mask = this.HashMask;
            //var index = filename.GetHashCode() & mask;

            var maxlevel = _masksize;
            for (int level = 0; level < maxlevel - 1; level++) ////one day, replace this loop with foreach(getBufferRecord())
            {
                var hashtable = cache[level];
                var remainingbuffers = maxlevel - level;
                var mask = collisionBuffersToMask(remainingbuffers);
                var index = hash & mask;
                var found = hashtable[index];
                if (found.Key == filename)
                {
                    _hit++;
                    return found.Value.Target;
                }
                hash = hash.GetHashCode(); //re-randomize for next level
            }

            _miss++;
            return null;
        }
        public double GetHitRate()
        {
            var den = _hit + _miss;
            if (den != 0)
                return (double)_hit / den;
            return 0;
        }
        public double GetMaxActual()
        {
            return _max;
        }
        public double MissBcCollected()
        {
            return _missbccollected;
        }
        public void Reset()
        {
            _max = 0;
            _missbccollected = 0;
            _hit = 0;
            _miss = 0;
            this.MaskSize = 13;

            //_cache = createCascadeHashtable(_masksize);
            //_expiration = new object[this.MaxCacheSize];
            //_nextexpire = 0;
        }
        public void AddToObjectCache(string filename, object obj)
        {
            var cache = _cache;
            var hash = filename.GetHashCode();

            var isinserted = false;
            var maxlevel = _masksize;
            DateTime oldest = DateTime.MaxValue;
            int oldestindex = 0;
            int oldestlevel = 0;
            for (int level = 0; level < maxlevel - 1; level++) //one day, replace this loop with foreach(getIndexInBuffer())
            {
                var remainingbuffers = maxlevel - level;
                var mask = collisionBuffersToMask(remainingbuffers);
                var index = hash & mask;

                var hashtable = cache[level];
                var found = hashtable[index];
                if (oldest < found.CreatedDate)
                {
                    oldest = found.CreatedDate;
                    oldestindex = index;
                    oldestlevel = level;
                }
                if (found.Key == null || found.Key == filename) //empty or same filename, so replace (there should only be one with same name, so don't bother checking for duplicate)
                {
                    hashtable[index] = new HashSlot(filename, new WeakReference(obj), null);
                    isinserted = true;
                    break;
                }
                else if (found.Value.Target == null) //everything falling here, has to have key and therefore a WeakReference, so checking if expired.  If so, remove prev entry, if one exists
                {
                    hashtable[index] = new HashSlot(filename, new WeakReference(obj), null);
                    removeStartingAtLevel(filename, level + 1, hash);
                    isinserted = true;
                    break;
                }
                hash = hash.GetHashCode(); //re-randomize for next level
            }
            if (!isinserted)
            {
                //all collision buffers are filled AND active
                //overwrite the oldest
                cache[oldestlevel][oldestindex] = new HashSlot(filename, new WeakReference(obj), null);
            }


            pinInRotatingMemory(obj);
        }

        object pinInRotatingMemory(object obj)
        {
            var next = _nextexpire;
            if (next > _masksize) //the increment is interlocked, but the reset to zero isn't part of that "transaction"
                next = next % _masksize;
            var expired = _expiration[next];
            _expiration[next] = obj;
            Interlocked.Increment(ref _nextexpire);
            if (_nextexpire >= this.MaxCacheSize)
                _nextexpire = 0;

            return expired; //free to be garbage collected once refernce is lost... may be null, which means everything not added, is still cached
        }
        void removeStartingAtLevel(string filename, int startinglevel, int hash)
        {
            var cache = _cache;
            foreach (var index in getIndexInBuffer(startinglevel, hash))
            {
                var hashtable = cache[startinglevel++];
                var found = hashtable[index];
                if (found.Key == filename)
                {
                    hashtable[index] = default(HashSlot);
                    break;
                }
            }
        }
        IEnumerable<int> getIndexInBuffer(int startinglevel, int hash)
        {
            var maxlevel = _masksize;
            for (int level = startinglevel; level < maxlevel - 1; level++)
            {
                var remainingbuffers = maxlevel - level;
                var mask = collisionBuffersToMask(remainingbuffers);
                var index = hash & mask;
                yield return index;
            }
        }
        IEnumerable<HashSlot> getBufferRecord(int startinglevel, int hash)
        {
            var cache = _cache;
            foreach (var index in getIndexInBuffer(startinglevel, hash))
            {
                var hashtable = cache[startinglevel++];
                yield return hashtable[index];
            }
        }

        public struct HashSlot
        {
            public HashSlot(string key, WeakReference value, SlotCounter counter)
            {
                this.Key = key;
                this.Value = value;
                this.CreatedDate = DateTime.Now;
                this.Counter = counter;
            }

            public string Key;
            public WeakReference Value;
            public DateTime CreatedDate;
            public SlotCounter Counter;
        }
        public class SlotCounter
        {
            HashSet<int> _occupied = new HashSet<int>();
            public void Occupied(int level)
            {
                _occupied.Add(level);
            }
            public void Released(int level)
            {
                _occupied.Remove(level);
            }
            public int Max
            {
                get
                {
                    var occupied = _occupied;
                    while (true)
                        try
                        {
                            var max = occupied.Max();
                            return max;
                        }
                        catch { } //keep retrying until this works... assuming that this won't affect a correct write operation, but a write operation can cause exception here
                }
            }
        }
    }

}
