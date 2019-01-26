using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


using System.Xml;
using System.Net;
using System.IO;
using System.Xml.Serialization;


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
