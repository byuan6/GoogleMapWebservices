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
        static int Main(string[] args)
        {
            bool __USER_PERMISSION_FOR_GOOGLE = false;
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
                    GoogleApiKeys.DistanceApiKey = key;
            }

            GoogleLocalDistanceIndex.Index.RemoteRetreive += delegate(object sender, GoogleLocalDistanceIndex.UserRequestEventArgs e)
            {
                Console.WriteLine("Data not local, Remotely requesting : {0}", e.Requested);
                if (!__USER_PERMISSION_FOR_GOOGLE)
                {
                    Console.WriteLine();
                    Console.WriteLine("Google charges $ for Distance Matrix API per Data Element. \nThis application can request hundreds of data elements.  \nPlease check the terms of your Google use agreement about cost.  This may incur billing to the API KEY.");
                    Console.Write("Do you wisth to proceed? (Y/N) >");
                    var response = Console.ReadLine();
                    if (response == "Y" || response == "y")
                        __USER_PERMISSION_FOR_GOOGLE = true;
                    else
                        Environment.Exit(0);
                    Console.WriteLine();
                }
            };
            GoogleLocalDistanceIndex.Index.UserRequestError += delegate(object sender, GoogleLocalDistanceIndex.UserRequestEventArgs e)
            {
                Console.WriteLine();
                Console.WriteLine("Error with : {0}", e.Requested);
                Console.WriteLine("Message    : {0}", e.Error);
                Console.WriteLine();
            };

            //see if its a file
            int rcode = 0;
            if (args.Length == 1 && System.Console.WindowWidth != 0 && System.Console.WindowHeight != 0 && File.Exists(args[0]))
            {
                using (var fs = File.OpenText(args[0]))
                {
                    var line = fs.ReadToEnd().Split('\n');
                    //DistanceResults location = getCachedDistances(line);
                    DistanceResults location = GoogleLocalDistanceIndex.Index.GetDistanceMatrix(line);
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
                //DistanceResults location = getCachedDistances(args);
                DistanceResults location = GoogleLocalDistanceIndex.Index.GetDistanceMatrix(args);
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
                    if (col != null)
                    {
                        Console.WriteLine("\tDuration:\t{0}", col.DurationText);
                        Console.WriteLine("\tDistance:\t{0}", col.DistanceText);
                    }
                    else
                        Console.WriteLine("Data not found.  Check the name of the location.  Chnage it to a land address.");
                    i++;
                }
                j++;
            }
        }

    
    }

    public class GoogleDistanceAPI
    {
        static public readonly GoogleDistanceAPI Proxy = new GoogleDistanceAPI();
        static string __API_KEY = GoogleApiKeys.DistanceApiKey;

        public XmlDocument MakeRequest(string requestUrl)
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

        public void WriteXML(DistanceResults store, string f)
        {
            XmlSerializer writer = new XmlSerializer(typeof(DistanceResults));

            var path = f;
            using (FileStream file = System.IO.File.Create(path))
            {
                writer.Serialize(file, store);
                file.Close();
            }

        }

        public DistanceResults ReadXML(string f)
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
        public DistanceResults getCachedDistances(string[] places)
        {
            string cachefile = GetFilenameFor(places, places);

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

            DistanceResults result = getDistancesFromGoogle(places);
            WriteXML(result, cachefile);

            return result;
        }
        

        public DistanceResults getDistancesFromGoogle(string[] places)
        {
            return GetDistancesFromGoogle(places, places);
        }

        public DistanceResults RetryDistancesFromGoogle(int count, int delayms, string[] from, string[] to)
        {
            for (int i = 0; i < count; i++)
                try
                {
                    return CheckedDistancesFromGoogle(from, to);
                }
                catch
                {
                    if(i<count-1)
                        Thread.Sleep(delayms);
                }
            throw new ApplicationException("Retries exceeded");
        }
        public DistanceResults CheckedDistancesFromGoogle(string[] from, string[] to)
        {
            var result = GetDistancesFromGoogle(from, to);
            if (result.Status == "OVER_QUERY_LIMIT")
                throw new ApplicationException("OVER_QUERY_LIMIT");
            else if (result.Status == "INVALID_REQUEST")
                throw new ApplicationException("INVALID_REQUEST");

            return result;
        }
        public DistanceResults GetDistancesFromGoogle(string[] from, string[] to)
        {
            string joined1 = string.Join("|", ToUrlSafeRequest(from));
            string joined2 = from == to ? joined1 : string.Join("|", ToUrlSafeRequest(to));
            // string url = "http://maps.googleapis.com/maps/api/distancematrix/xml?origins=New+York+NY|Seattle&destinations=San+Francisco|New+York+NY|Boston&mode=driving&language=en-US&sensor=false";
            string url = string.Format(@"https://maps.googleapis.com/maps/api/distancematrix/xml?key={0}&origins={1}&destinations={2}&mode=driving&language=en-US&sensor=false", __API_KEY, joined1, joined2);

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

        static public string[] ToUrlSafeRequest(string[] places)
        {
            string[] escaped = places.Select<string, string>(s => System.Uri.EscapeDataString(s)).ToArray<string>();
            return escaped;
        }

        static public string GetFilenameFor(string[] from, string[] to)
        {
            if (from == to)
            {
                string[] escaped = from.Select<string, string>(s => System.Uri.EscapeDataString(s)).ToArray<string>();
                string joined = string.Join("|", escaped);
                string cachefile = joined.Replace("|", "_") + ".distcached";
                return cachefile;
            }
            else
                throw new NotImplementedException("GetFilenameFor(string,string) not complete");
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

        public string[] OriginResponse;
        public string[] DestinationResponse;
    }



    public class GoogleLocalDistanceIndex
    {
        static GoogleLocalDistanceIndex __index = new GoogleLocalDistanceIndex();
        static public GoogleLocalDistanceIndex Index
        {
            get
            {
                return __index;
            }
        }


        public GoogleLocalDistanceIndex() 
        {
            try
            {
                this.Load();
            }
            catch {
                this.Refresh();
            }

            var watcher = _watcher;
            watcher.Changed+= new FileSystemEventHandler(watcher_Changed);
            watcher.Created+= new FileSystemEventHandler(watcher_Created);
            watcher.Deleted+=new FileSystemEventHandler(watcher_Deleted);
            watcher.Renamed += new RenamedEventHandler(watcher_Renamed);
            _watcher.EnableRaisingEvents = true;
        }

        GoogleDistanceAPI _service = new GoogleDistanceAPI();
        FixedSizeObjectCache _cache = new FixedSizeObjectCache();

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
        public bool Contains(string origin, string dest)
        {
            var bindex = _o2dindex;
            if (bindex.ContainsKey(origin))
                if (bindex[origin].ContainsKey(dest))
                    return true;
            return false;
        }
        public List<DistElement> GetDistancesFrom(string origin)
        {
            throw new NotImplementedException();
        }
        public List<DistElement> GetDistancesTo(string dest)
        {
            throw new NotImplementedException();
        }

        public DistanceResults GetDistanceMatrix(IList<string> origindest)
        {
            return GetDistanceMatrix(origindest, origindest);
        }

        static string calculateBase64Hash(string input)
        {
            // step 1, calculate MD5 hash from input
            System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
            byte[] hash = md5.ComputeHash(inputBytes);


            return Convert.ToBase64String(hash).Replace("=", "").Replace("/", "-");
        }
        static string filehash(string candidate)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            string name = candidate.Replace("%20", "+").Replace(" ", "").Replace("%2C", ",");
            var escaped = name.Select<char, char>(s => Array.IndexOf<char>(invalid, s) >= 0 ? '_' : s).ToArray<char>();

            return new string(escaped);
        }
        static public string getNTFSName(string[] origin, string[] dest)
        {
            string[] escaped1 = origin.Select<string, string>(s => filehash(s)).ToArray<string>();
            string[] escaped2 = dest.Select<string, string>(s => filehash(s)).ToArray<string>();
            string joined = filehash(string.Join("-", escaped1) + string.Join("-", escaped2));
            if (joined.Length > 120)
                joined = joined.Substring(0, 100) + "."+ calculateBase64Hash(joined);

            string cachefile = joined + ".distcached";
            return cachefile;
        }
        static public string getNTFSName(string bigname)
        {
            string namevalidchar = filehash(bigname);
            if (namevalidchar.Length > 120)
                namevalidchar = namevalidchar.Substring(0, 100) + "." + calculateBase64Hash(namevalidchar);

            string cachefile = namevalidchar + ".distcached";
            return cachefile;
        }


        //we want a system of ordering the requests to make best use of caching... order the requests by origin and destination
        public DistanceResults GetDistanceMatrix(IList<string> origin, IList<string> dest)
        {
            DistanceResults matrix = new DistanceResults() { Status = "OK" };
            matrix.Origin.AddRange(origin);
            matrix.Destination.AddRange(dest);
            matrix.OriginResponse = new string[origin.Count];
            matrix.DestinationResponse = new string[dest.Count];

            //find out if an existing resultset was requested and cached
            var filename = getNTFSName(matrix.ToFilename());
            //hash the filename to comply with NTFS rules
            if (File.Exists(filename))
                try
                {
                    return _service.ReadXML(filename);
                    //return GoogleDistanceMatrix.ReadXML(filename);
                }
                catch { File.Delete(filename); }

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
            var notfound = false;
            var matrixRow = matrix.Row;
            var oricount = origin.Count;
            var destcount = dest.Count;
            bool[,] found = new bool[destcount, oricount];
            for(int j=0; j<oricount; j++)
                for (int i= 0; i < destcount; i++)
                    {
                        var from = origin[j];
                        var to= dest[j];
                        if (this.Contains(from, to))
                        {
                            found[j, i] = true;
                            matrixRow[j][i] = this[from, to];
                        }
                        else if (!notfound)
                            notfound = true;
                    }

            if (notfound)
            {
                ReduceSurfaceAreaByReordering waterdrop = new ReduceSurfaceAreaByReordering() { Area = found };
                waterdrop.SortPass += delegate(object sender, EventArgs e)
                {
                    Console.WriteLine("Finding missing elements");
                };
                BiggestContiguousBlock skimmer = new BiggestContiguousBlock(waterdrop);
                foreach (var item in skimmer.Chunkify(false))
                {
                    //item.SetAllTrue();

                    const int GOOGLE_MAX_ORIGIN_OR_DEST = 25;
                    const int GOOGLE_MAX_REQUESTS = 100;
                    const int GOOGLE_DEFACTO_MAX_ORIGIN_AND_DEST = 10;
                    foreach (var subitem in item.Chunkify(GOOGLE_DEFACTO_MAX_ORIGIN_AND_DEST))
                    {
                        var suborigin = subitem.RowIndices.Select(s => origin[s]).ToArray();
                        var subdest = subitem.ColIndices.Select(s => dest[s]).ToArray();

                        if (this.RemoteRetreive != null)
                            this.RemoteRetreive(this, new UserRequestEventArgs("[" + string.Join("; ", suborigin) + "] to [" + string.Join("; ",subdest) + "]"));

                        var googleresults = _service.RetryDistancesFromGoogle(10, 1000, suborigin, subdest);  //GetDistancesFromGoogle(suborigin, subdest); //change this to 
                        var row=0;
                        var col=0;
                        foreach (var fromindex in subitem.RowIndices)
                        {
                            col = 0;
                            foreach (var toindex in subitem.ColIndices)
                            {
                                var from = origin[fromindex];
                                var to = dest[toindex];
                                var data = googleresults.Row[row][col++];
                                if (data.Status == "OK")
                                    matrixRow[fromindex][toindex] = data;
                                else 
                                    if (this.UserRequestError != null)
                                        this.UserRequestError(this, new UserRequestEventArgs("[" + from + "] to [" + to + "]", data.Status));
                            }
                            row++;
                        }
                        
                        //until alias system is up, update with user requested name
                        row=0;
                        col=0;
                        foreach (var fromindex in subitem.RowIndices)
                            matrix.OriginResponse[fromindex] = googleresults.Origin[row++];
                        foreach (var toindex in subitem.ColIndices)
                            matrix.DestinationResponse[toindex] = googleresults.Destination[col++];

                        subitem.SetAllTrue();
                    }
                }

                

                _service.WriteXML(matrix, filename);
            }


            //and group the stuff cached, in such a way that it takes the best use of the deserialized object cache... (obviously file caching works better too)

            return matrix;

        }

        public event EventHandler<UserRequestEventArgs> RemoteRetreive;
        public event EventHandler<UserRequestEventArgs> UserRequestError;
        public class UserRequestEventArgs : EventArgs
        {
            public UserRequestEventArgs(string request) { this.Requested = request; }
            public UserRequestEventArgs(string request, string error):this(request) { this.Error = error; }

            readonly public string Requested;
            readonly public string Error;
        }

        bool _recordsmodified = false;
        int _next = 0;
        List<IndexEntry> _records = new List<IndexEntry>();
        Dictionary<string, List<IndexEntry>> _originindex = new Dictionary<string, List<IndexEntry>>();
        Dictionary<string, List<IndexEntry>> _destindex = new Dictionary<string, List<IndexEntry>>();
        Dictionary<string, List<IndexEntry>> _filenameindex = new Dictionary<string, List<IndexEntry>>();
        Dictionary<string, Dictionary<string, List<IndexEntry>>> _o2dindex = new Dictionary<string, Dictionary<string, List<IndexEntry>>>();
        HashSet<string> _unprocessedadds = new HashSet<string>();

        /// <summary>
        /// Deleting is going to be a watch thing only.  Refresh will never pick it up.  The user will not have option.
        /// Deletes get removed with each load.  Numbering gets reset;
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void  watcher_Deleted(object sender, FileSystemEventArgs e)
        {
            var name = e.FullPath;
            if (_filenameindex.ContainsKey(name))
            {
                var list = _filenameindex[name];
                _filenameindex.Remove(name);
                foreach (var item in list)
                    item.IsDeleted = true;
                if (!_recordsmodified) _recordsmodified = true;
            }
        }
        /// <summary>
        /// Adding new file is controlled by Inventory(), but will not be triggered sychronously with user action.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void  watcher_Created(object sender, FileSystemEventArgs e)
        {
            try
            {
                this.Inventory(e.FullPath);
                foreach (var item in _unprocessedadds)
                    this.Inventory(item);
            }
            catch
            {
                _unprocessedadds.Add(e.FullPath);
            }
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
            var matrix = _service.ReadXML(filename);
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
                    lock (_o2dindex)
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
            //foreach(var item in list)
//                this.Inventory(item);

            //foreach (var item in list)
            ParallelOptions options = new ParallelOptions() { MaxDegreeOfParallelism=Environment.ProcessorCount };
            Parallel.ForEach(list, options, delegate(string item)
            {
                try
                {
                    this.Inventory(item);
                    System.Diagnostics.Debug.WriteLine(item);
                }
                catch { }
            });

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
            entry.AppPersistance = _service; //make sure the caching and persistance layer is applied (we have a in-between layer between this, and the file system provided by System.IO)
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
            public IndexEntry(GoogleLocalDistanceIndex parentIndex) { _parentIndex = parentIndex; _appspecificPersistanceLayer = parentIndex._service; }
            GoogleLocalDistanceIndex _parentIndex = null;
            GoogleDistanceAPI _appspecificPersistanceLayer = null;
            bool _wasparameterlessconstructor = false;

            internal GoogleDistanceAPI AppPersistance
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
                var matrix = _appspecificPersistanceLayer.ReadXML(this.Filename);
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
                    var cachingmechanism = _parentIndex._cache;

                    var filename = this.Filename;
                    var obj = cachingmechanism.GetFromObjectCache(filename);
                    if (obj != null && obj is DistanceResults)
                        return (DistanceResults)obj;

                    //cache miss
                    var matrix = _appspecificPersistanceLayer.ReadXML(this.Filename);
                    cachingmechanism.AddToObjectCache(filename, matrix);

                    return matrix;
                }
                else if (!_wasparameterlessconstructor)
                    return _appspecificPersistanceLayer.ReadXML(this.Filename);
                else
                    throw new NotSupportedException("You need to instantiate IndexEntry(GoogleLocalDistanceIndex), even if it is null.  The parameterless constructor was strictly for XmlSerializer, which was intended to be used by ObjectCache, which requires to know the persistance layer/object type it is caching.");
            }
        }

        public readonly FixedSizeObjectCache CachingMechanism = new FixedSizeObjectCache();

    }

    public class DistanceRequest
    {
        public UserRequestedName[] Origins;
        public UserRequestedName[] Destinations;
    }
    public class UserRequestedName
    {
        public UserRequestedName() { }
        public UserRequestedName(string locationname) { this.LocationName = locationname; }

        public string LocationName;
        public List<string> Alias = new List<string>();
        public void Save()
        {
            //save .distalias, so the alias-->userrequest can be established (if we want to show the original user request)
            //save .distreqname, so user request-->alias can be established (indexed entry is under alias)
        }
        public void Load()
        {
            //Load .distreqname
        }
        public void ParseResponseName(string response)
        {
            //get response, check if returned name is different from .UserRequest
            //Save().
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
