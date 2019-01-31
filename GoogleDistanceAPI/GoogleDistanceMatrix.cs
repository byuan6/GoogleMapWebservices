using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


using System.Xml;
using System.Net;
using System.IO;
using System.Xml.Serialization;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace DistanceBetween
{
    public class GoogleDistanceMatrix
    {
        static string __API_KEY = GoogleApiKeys.DistanceApiKey;

        static int Main(string[] args)
        {
            if (args.Length == 0 && System.Console.WindowWidth != 0 && System.Console.WindowHeight != 0)
            {
                showUsage();
                return 1;
            }

            if (string.IsNullOrWhiteSpace(GoogleApiKeys.DistanceApiKey))
            {
                Console.WriteLine("No API key for Distance API.  Please enter:");
                Console.Write(">");
                var key = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(key))
                {
                    Console.WriteLine("Bad API key");
                    return 2;
                }
                else
                    __API_KEY = GoogleApiKeys.DistanceApiKey = key;
            }

            //see if its a file
            int rcode = 0;
            if (args.Length == 1 && System.Console.WindowWidth != 0 && System.Console.WindowHeight != 0 && File.Exists(args[0]))
            {
                using (var fs = File.OpenText(args[0]))
                {
                    var line = fs.ReadToEnd().Split('\n');
                    DistanceResults location = getCachedDistances(line);
                    if (location != null)
                        showResult(line, location);
                    else
                        rcode = 2;
                }

                return rcode;
            }

            // stdin
            /*
            string sin;
            if (System.Console.WindowWidth == 0 && System.Console.WindowHeight == 0)
                while ((sin = Console.ReadLine()) != null)
                {
                    LocationResults location = getCachedLocation(sin);
                    if (location != null)
                        showResult(sin, location);
                    else
                        rcode = 2;
                }
            */

            // all the arguments must be locations then
            {
                DistanceResults location = getCachedDistances(args);
                if (location != null)
                    showResult(args, location);
                else
                    rcode = 2;
            }

#if DEBUG
            Console.WriteLine("Press [enter] to end");
            Console.ReadLine();
#endif

            return rcode;
        }




        static void showUsage()
        {
            Console.WriteLine("Usage:         DistanceBetween [filename]");
            Console.WriteLine("               DistanceBetween \"[search location1]\" \"[search location2]\"");
            Console.WriteLine("  [location] | DistanceBetween");
        }
        static void showResult(string[] orig, DistanceResults distances)
        {
            
            Console.WriteLine("Searched:\t{0}", string.Join(" | ", orig));
            Console.WriteLine("Status:\t{0}", distances.Status);
            Console.WriteLine("Locations:\t{0}", string.Join(" | ", distances.Origin));

            int j=0;
            foreach (var row in distances.Row)
            {
                int i=0;
                foreach (var col in row)
                {
                    Console.WriteLine("Path:\t{0}...{1}", distances.Origin[i], distances.Destination[j]);
                    Console.WriteLine("\tDuration:\t{0}", col.DurationText);
                    Console.WriteLine("\tDistance:\t{0}", col.DistanceText);
                    i++;
                }
                j++;
            }
        }

        public static XmlDocument MakeRequest(string requestUrl)
        {
            try
            {
                HttpWebRequest request = WebRequest.Create(requestUrl) as HttpWebRequest;
                HttpWebResponse response = request.GetResponse() as HttpWebResponse;

                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(response.GetResponseStream());

                response.Close();

                return (xmlDoc);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }

        public static void WriteXML(DistanceResults store, string f)
        {
            XmlSerializer writer = new XmlSerializer(typeof(DistanceResults));

            var path = f;
            using (FileStream file = System.IO.File.Create(path))
            {
                writer.Serialize(file, store);
                file.Close();
            }

        }

        public static DistanceResults ReadXML(string f)
        {
            DistanceResults myObject;

            // Construct an instance of the XmlSerializer with the type
            // of object that is being deserialized.
            XmlSerializer mySerializer = new XmlSerializer(typeof(DistanceResults));

            // To read the file, create a FileStream.
            using (FileStream myFileStream = new FileStream(f, FileMode.Open))
            {
                // Call the Deserialize method and cast to the object type.
                myObject = (DistanceResults)mySerializer.Deserialize(myFileStream);
                myObject.IsCached = true;
            }

            return myObject;
        }

        static TimeSpan __30_DAYS = new TimeSpan(30, 0, 0, 0);
        static public DistanceResults getCachedDistances(string[] places)
        {
            string[] escaped = places.Select<string, string>(s => System.Uri.EscapeDataString(s)).ToArray<string>();
            string joined = string.Join("|", escaped);
            string cachefile = joined.Replace("|","_") + ".distcached";

            if (File.Exists(cachefile) && DateTime.Now - (new FileInfo(cachefile)).LastWriteTime < __30_DAYS)
                try
                {
                    return ReadXML(cachefile);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    File.Delete(cachefile);
                }

            DistanceResults result = getDistancesFromGoogle(escaped);
            WriteXML(result, cachefile);

            return result;
        }

        static public DistanceResults getDistancesFromGoogle(string[] places)
        {
            string joined = string.Join("|", places);
            // string url = "http://maps.googleapis.com/maps/api/distancematrix/xml?origins=New+York+NY|Seattle&destinations=San+Francisco|New+York+NY|Boston&mode=driving&language=en-US&sensor=false";
            string url = string.Format(@"https://maps.googleapis.com/maps/api/distancematrix/xml?key={1}&origins={0}&destinations={0}&mode=driving&language=en-US&sensor=false", joined, __API_KEY);

            XmlDocument doc = MakeRequest(url);

            /*
            <?xml version="1.0" encoding="UTF-8"?>
            <DistanceMatrixResponse>
                <status>OK</status>
                <origin_address>New York, NY, USA</origin_address>
                <origin_address>Seattle, WA, USA</origin_address>
                <destination_address>San Francisco, CA, USA</destination_address>
                <destination_address>New York, NY, USA</destination_address>
                <destination_address>Boston, MA, USA</destination_address>
                <row>
                    <element>
                        <status>OK</status>
                        <duration>
                        <value>152102</value>
                        <text>1 day 18 hours</text>
                        </duration>
                        <distance>
                        <value>4674274</value>
                        <text>4,674 km</text>
                        </distance>
                    </element>
                    <element>
                        <status>OK</status>
                        <duration>
                        <value>0</value>
                        <text>1 min</text>
                        </duration>
                        <distance>
                        <value>0</value>
                        <text>1 m</text>
                        </distance>
                    </element>
                    <element>
                        <status>OK</status>
                        <duration>
                        <value>13039</value>
                        <text>3 hours 37 mins</text>
                        </duration>
                        <distance>
                        <value>346503</value>
                        <text>347 km</text>
                        </distance>
                    </element>
                </row>
                <row>
                    <element>
                        <status>OK</status>
                        <duration>
                        <value>44447</value>
                        <text>12 hours 21 mins</text>
                        </duration>
                        <distance>
                        <value>1299975</value>
                        <text>1,300 km</text>
                        </distance>
                    </element>
                    <element>
                        <status>OK</status>
                        <duration>
                        <value>148892</value>
                        <text>1 day 17 hours</text>
                        </duration>
                        <distance>
                        <value>4589407</value>
                        <text>4,589 km</text>
                        </distance>
                    </element>
                    <element>
                        <status>OK</status>
                        <duration>
                        <value>158468</value>
                        <text>1 day 20 hours</text>
                        </duration>
                        <distance>
                        <value>4901431</value>
                        <text>4,901 km</text>
                        </distance>
                    </element>
                </row>
            </DistanceMatrixResponse>
            */

            int value = 0;
            DistanceResults result = new DistanceResults();
            XmlNode statusnode = doc.DocumentElement.SelectSingleNode("/DistanceMatrixResponse/status");
            if (statusnode != null)
                result.Status = statusnode.InnerText;

            XmlNodeList orinode = doc.DocumentElement.SelectNodes("/DistanceMatrixResponse/origin_address");
            foreach (XmlNode node in orinode)
                result.Origin.Add(node.InnerText);

            XmlNodeList destnode = doc.DocumentElement.SelectNodes("/DistanceMatrixResponse/destination_address");
            foreach (XmlNode node in destnode)
                result.Destination.Add(node.InnerText);


            XmlNodeList rownode = doc.DocumentElement.SelectNodes("/DistanceMatrixResponse/row");
            foreach (XmlNode node in rownode)
            {
                List<DistElement> row = new List<DistElement>();
                XmlNodeList colnode = node.SelectNodes("element");
                foreach (XmlNode elenode in colnode)
                {
                    DistElement element = new DistElement();
                    XmlNode statmnode = elenode.SelectSingleNode("status");
                    if (statmnode != null)
                        element.Status = statmnode.InnerText;

                    XmlNode dursnode = elenode.SelectSingleNode("duration/value");
                    if (dursnode != null && int.TryParse(dursnode.InnerText, out value))
                        element.DurationSeconds = dursnode.InnerText;
                    XmlNode durnode = elenode.SelectSingleNode("duration/text");
                    if (durnode != null)
                        element.DurationText = durnode.InnerText;

                    XmlNode dstmnode = elenode.SelectSingleNode("distance/value");
                    if (dstmnode != null && int.TryParse(dstmnode.InnerText, out value))
                        element.DistanceMeters = dstmnode.InnerText;
                    XmlNode dstnode = elenode.SelectSingleNode("distance/text");
                    if (dstnode != null)
                        element.DistanceText = dstnode.InnerText;

                    row.Add(element);
                }

                result.Row.Add(row);
            }

            return result;
        }
    }

    
    public class DistElement
    {
        public string Status { get;set; }
        public string DurationSeconds { get;set; }
        public string DurationText { get;set; }
        public string DistanceMeters { get;set; }
        public string DistanceText { get;set; }
    }

    public class DistanceResults
    {
        public bool IsCached { get; set; }
        public string Status { get; set; }
        public readonly List<string> Origin = new List<string>();
        public readonly List<string> Destination = new List<string>();

        public List<List<DistElement>> Row = new List<List<DistElement>>();

        /// <summary>
        /// Keep the naive version... Any hashing of the filename, is going to be based on persistance layer
        /// </summary>
        /// <returns></returns>
        public string ToFilename()
        {
            string[] escaped1 = this.Origin.Select<string, string>(s => System.Uri.EscapeDataString(s)).ToArray<string>();
            string[] escaped2 = this.Destination.Select<string, string>(s => System.Uri.EscapeDataString(s)).ToArray<string>();
            string joined = string.Join("|", escaped1) + "|" + string.Join("|", escaped2);
            string cachefile = joined.Replace("|", "!") + ".distcached";
            return cachefile;
        }
    }
    public class DistanceMatrix : DistanceResults
    {

    }



    public class GoogleLocalDistanceIndex
    {
        static GoogleLocalDistanceIndex __index = new GoogleLocalDistanceIndex();
        static GoogleLocalDistanceIndex Index
        {
            get
            {
                return __index;
            }
        }


        public GoogleLocalDistanceIndex() 
        { 
            var watcher = _watcher;
            watcher.Changed+= new FileSystemEventHandler(watcher_Changed);
            watcher.Created+= new FileSystemEventHandler(watcher_Created);
            watcher.Deleted+=new FileSystemEventHandler(watcher_Deleted);
            watcher.Renamed += new RenamedEventHandler(watcher_Renamed);
            _watcher.EnableRaisingEvents = true;
        }

        

        const string WATCH_EXT = "*.distcached";
        string _watchFolder = Environment.CurrentDirectory;
        public string WatchFolder { get; set; }
        FileSystemWatcher _watcher = new FileSystemWatcher(Environment.CurrentDirectory, WATCH_EXT);


        public DistElement this[string origin, string dest]
        {
            get
            {                
                throw new NotImplementedException();
            }
        }
        public List<DistElement> GetDistancesFrom(string origin)
        {
            throw new NotImplementedException();
        }
        public List<DistElement> GetDistancesTo(string dest)
        {
            throw new NotImplementedException();
        }
        //we want a system of ordering the requests to make best use of caching... order the requests by origin and destination
        public DistanceMatrix GetDistanceMatrix(IList<string> origin, IList<string> dest)
        {
            throw new NotImplementedException();


            DistanceMatrix matrix = new DistanceMatrix();
            matrix.Origin.AddRange(origin);
            matrix.Destination.AddRange(dest);

            //find out if an existing resultset was requested and cached
            var filename = matrix.ToFilename();
            //see if filename cached, then filename exists

            //resultset not cached
            matrix.Row.AddRange(origin.Select(s => new List<DistElement>(dest.Select<string, DistElement>(s2 => null))));

            //so assume some are cached...some are not... 
            //how do we group the stuff not cached which gets send remotely

            //get the stuff cached, and assign scores to each origin with a correlated misses
            //getting the misses is easy... how do we group the misses together, so we make a one big request with stuff we want.
            //previously, we did it with same counts...
            // we can do it with flags ANDed together
            //   A B C D E F
            // Z 1 1 1 0 0 1 = 4 (previous code grouped all the 4's together and tried to figure which dest matched)
            // Y 1 1 1 0 0 1 = 4
            // X 1 1 0 1 0 1 = 4
            // W 1 1 0 0 1 1 = 4
            // ----------------
            // 1 1 0 0 0 1 = 3 But if we AND all the bits, then look at it, we know we only request [Z Y X W] x [A B F]
            //                 But this means we have to do a cross-AND with every row, of every origin with same number of misses, to determine 2nd-order sort
            //                   plus the excluded [Z Y X W] x [C D E], which only has 4x 1's, out of 12 requests
            //
            // so this turns above into 
            //   A B F          C D E 
            // Z 1 1 1 = 3      1 0 0 
            // Y 1 1 1 = 3      1 0 0 
            // X 1 1 1 = 3      0 1 0 
            // W 1 1 1 = 3      0 0 1
            //
            //                  C       
            // Z                1
            // Y                1
            //                    D
            // X                  1
            //                      E
            // W                    1
            //
            // so to minimize the number of costable requests to google..., though we can call [Z Y X W] x [C D E], with a 75% miss rate
            // is a 



            //and group the stuff cached, in such a way that it takes the best use of the deserialized object cache... (obviously file caching works better too)

        }


        bool _recordsmodified = false;
        int _next = 0;
        List<IndexEntry> _records = new List<IndexEntry>();
        Dictionary<string, List<IndexEntry>> _originindex = new Dictionary<string, List<IndexEntry>>();
        Dictionary<string, List<IndexEntry>> _destindex = new Dictionary<string, List<IndexEntry>>();
        Dictionary<string, List<IndexEntry>> _filenameindex = new Dictionary<string, List<IndexEntry>>();
        Dictionary<string, Dictionary<string, List<IndexEntry>>> _o2dindex = new Dictionary<string, Dictionary<string, List<IndexEntry>>>();

        /// <summary>
        /// Deleting is going to be a watch thing only.  Refresh will never pick it up.  The user will not have option.
        /// Deletes get removed with each load.  Numbering gets reset;
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void  watcher_Deleted(object sender, FileSystemEventArgs e)
        {
            var name = e.FullPath;
            var list = _filenameindex[name];
            _filenameindex.Remove(name);
            foreach (var item in list)
                item.IsDeleted = true;
            if (!_recordsmodified) _recordsmodified = true;
        }
        /// <summary>
        /// Adding new file is controlled by Inventory(), but will not be triggered sychronously with user action.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void  watcher_Created(object sender, FileSystemEventArgs e)
        {
            this.Inventory(e.FullPath);
        }
        /// <summary>
        /// Assumes worst case, that the file is completely different.  Removes old entries
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void  watcher_Changed(object sender, FileSystemEventArgs e)
        {
            watcher_Deleted(sender, e); //delete the entries associated with this file, and re-insert;
            this.Inventory(e.FullPath);
        }
        void watcher_Renamed(object sender, RenamedEventArgs e)
        {
            var name = e.FullPath;
            var list = _filenameindex[e.OldFullPath];
            _filenameindex.Remove(e.OldFullPath);
            foreach (var item in list)
            {
                item.Filename = name;
            }
            _filenameindex.Add(name,list);
            if (!_recordsmodified) _recordsmodified = true;
        }

        public void Inventory(string filename)
        {
            var records = _records;
            var matrix = GoogleDistanceMatrix.ReadXML(filename);
            var o = matrix.Origin;
            var h = o.Count;
            var d = matrix.Destination;
            var w = d.Count;
            for(int j=0; j<h; j++)
                for (int i = 0; i < w; i++)
                {
                    var element = matrix.Row[j][i];
                    var entry = new IndexEntry(this) { Col = i, Row = j, Filename = filename, Origin = o[j], Dest = d[i] };
                    records.Add(entry);
                    lock (this)
                        addToHashtables(entry);
                }
            if (!_recordsmodified) _recordsmodified = true;
        }
        public void Clear()
        {
            _records.Clear();
            if (!_recordsmodified) _recordsmodified = true;
        }
        public void Refresh() 
        {
            this.Clear();

            var list = Directory.GetFiles(_watchFolder, WATCH_EXT);

            //foreach (var item in list)
            ParallelOptions options = new ParallelOptions() { MaxDegreeOfParallelism=Environment.ProcessorCount/2 };
            Parallel.ForEach(list, new Action<string>(delegate(string item)
            {
                this.Inventory(item);
            }));

            if (!_recordsmodified) _recordsmodified = true;
            this.Save();
        }
        public void Save()
        {
            if (!_recordsmodified)
                return;

            XmlSerializer writer = new XmlSerializer(typeof(List<IndexEntry>));

            var path = "primary.distcached.index";
            using (FileStream fs = System.IO.File.Create(path))
            {
                writer.Serialize(fs, _records);
            }
        }
        public void Load()
        {
            XmlSerializer reader = new XmlSerializer(typeof(List<IndexEntry>));

            // To read the file, create a FileStream.
            var path = "primary.distcached.index";
            using (FileStream fs = new FileStream(path, FileMode.Open))
            {
                // Call the Deserialize method and cast to the object type.
                var list = _records = (List<IndexEntry>)reader.Deserialize(fs);
                _next = 0;
                var len = list.Count;
                for(int i=0; i<len; i++)
                {
                    var item = list[i];
                    if (!item.IsDeleted)
                        lock (this) //the indexes need to be locked bc they aren't threadsafe
                            addToHashtables(item);
                    else
                    {
                        lock (list) //the main table of index proxies isn't threadsafe, and needs to be locked for modification... if we have read threading problems, "page out" the old list with new instance.
                            list.RemoveAt(i);
                        i--;
                    }
                }
            }
            _recordsmodified = false;
        }
        public void addToHashtables(IndexEntry entry)
        {
            entry.AppPersistance = this; //make sure the caching and persistance layer is applied (we have a in-between layer between this, and the file system provided by System.IO)
            entry.Number = _next++;

            if (_originindex.ContainsKey(entry.Origin))
                _originindex[entry.Origin].Add(entry);
            else
                _originindex.Add(entry.Origin, new List<IndexEntry>() { entry });
            if (_destindex.ContainsKey(entry.Dest))
                _destindex[entry.Dest].Add(entry);
            else
                _destindex.Add(entry.Dest, new List<IndexEntry>() { entry });
            if (_filenameindex.ContainsKey(entry.Filename))
                _filenameindex[entry.Filename].Add(entry);
            else
                _filenameindex.Add(entry.Filename, new List<IndexEntry>() { entry });

            if (_o2dindex.ContainsKey(entry.Origin))
                if (_o2dindex[entry.Origin].ContainsKey(entry.Dest))
                    _o2dindex[entry.Origin][entry.Dest].Add(entry);
                else
                    _o2dindex[entry.Origin].Add(entry.Dest, new List<IndexEntry>() { entry });
            else
                _o2dindex.Add(entry.Origin, new Dictionary<string, List<IndexEntry>>() { { entry.Dest, new List<IndexEntry>() { entry } } });
        }


        public class IndexEntry
        {
            public IndexEntry() { _wasparameterlessconstructor = true; }
            public IndexEntry(GoogleLocalDistanceIndex persistanceLayer) { _appspecificPersistanceLayer = persistanceLayer; }
            GoogleLocalDistanceIndex _appspecificPersistanceLayer = null;
            bool _wasparameterlessconstructor = false;

            internal GoogleLocalDistanceIndex AppPersistance
            {
                set
                {
                    _appspecificPersistanceLayer = value;
                }
            }

            public int Number;
            public string Filename;
            public string Origin;
            public string Dest;
            public int Row;
            public int Col;
            public DateTime SourceDate;
            public bool IsDeleted;

            public DistElement GetRecord()
            {
                var matrix = GoogleDistanceMatrix.ReadXML(this.Filename);
                var entry = matrix.Row[this.Row][this.Col];

                return entry;
            }
            public DistElement GetRecordFromCache()
            {
                var matrix = GetFileFromCache();
                var entry = matrix.Row[this.Row][this.Col];

                return entry;
            }

            DistanceResults GetFileFromCache()
            {
                if (_appspecificPersistanceLayer != null)
                {
                    var persistance = _appspecificPersistanceLayer;
                    var cachingmechanism = persistance.CachingMechanism;

                    var filename = this.Filename;
                    var obj = cachingmechanism.GetFromObjectCache(filename);
                    if (obj != null && obj is DistanceResults)
                        return (DistanceResults)obj;

                    //cache miss
                    var matrix = GoogleDistanceMatrix.ReadXML(this.Filename);
                    cachingmechanism.AddToObjectCache(filename, matrix);

                    return matrix;
                }
                else if (!_wasparameterlessconstructor)
                    return GoogleDistanceMatrix.ReadXML(this.Filename);
                else
                    throw new NotSupportedException("You need to instantiate IndexEntry(GoogleLocalDistanceIndex), even if it is null.  The parameterless constructor was strictly for XmlSerializer, which was intended to be used by ObjectCache, which requires to know the persistance layer/object type it is caching.");
            }
        }

        public readonly FixedSizeObjectCache CachingMechanism = new FixedSizeObjectCache();

    }


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


        HashSlot[][] _cache = new HashSlot[DEFAULT_HASHTABLE_SIZE][];
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


    static public partial class GoogleApiKeys
    {
        static public string DistanceApiKey
        {
            get
            {
                var config = System.Configuration.ConfigurationManager.OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.None);
                var element = config.AppSettings.Settings["GoogleDistanceApiKey"];
                var key = element == null ? null : element.Value;
#if DEBUG
                if (string.IsNullOrWhiteSpace(key))
                {
                    var devkey = Environment.GetEnvironmentVariable("GoogleDistanceApiKey");
                    if (!string.IsNullOrWhiteSpace(key))
                        GoogleApiKeys.DistanceApiKey = key = devkey;
                }
#endif
                return key;
            }
            set
            {
                var config = System.Configuration.ConfigurationManager.OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.None);
                if (config.AppSettings.Settings["GoogleDistanceApiKey"] == null)
                    config.AppSettings.Settings.Add("GoogleDistanceApiKey", value);
                else
                    config.AppSettings.Settings["GoogleDistanceApiKey"].Value = value;
                config.Save(System.Configuration.ConfigurationSaveMode.Modified);
            }
        }
    }
}
