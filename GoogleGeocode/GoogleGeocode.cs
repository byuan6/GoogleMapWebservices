using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Xml;
using System.Net;
using System.IO;
using System.Xml.Serialization;

namespace CoordinatesOf
{
    public class GoogleGeocode
    {
        static string __API_KEY = GoogleApiKeys.GeocodeApiKey;
        static int Main(string[] args)
        {
            if (args.Length == 0 && System.Console.WindowWidth != 0 && System.Console.WindowHeight != 0)
            {
                showUsage();
                return 1;
            }

            if (string.IsNullOrWhiteSpace(GoogleApiKeys.GeocodeApiKey))
            {
                Console.WriteLine("No API key for Geocoding.  Please enter:");
                Console.Write(">");
                var key = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(key))
                {
                    Console.WriteLine("Bad API key");
                    return 2;
                }
                else
                    __API_KEY = GoogleApiKeys.GeocodeApiKey = key;
            }

            //check for -diags t check caches
            if (Array.IndexOf<string>(args, "-diag") >= 0)
            {
                foreach (var file in Directory.GetFiles(".", "*.geocached"))
                {
                    var data = ReadXML(file);
                    bool good = IsLocationValid(data);
                    //Console.WriteLine("{0,-70} {1} {2} {3} {4}", file, data.IsCached, data.Lat, data.Long, data.Formatted);
                    Console.WriteLine("{0,-70} {1}", file, good);
                }
                return 0;
            }

            //see if its a file
            string last = null;
            int rcode = 0;
            if (args.Length == 1 && System.Console.WindowWidth != 0 && System.Console.WindowHeight != 0 && File.Exists(args[0]))
            {
                using(var fs = File.OpenText(args[0]))
                    while(!fs.EndOfStream)
                    try
                    {
                        var line = last = fs.ReadLine();
                        LocationResults location = getCachedLocation(line);
                        if (location != null)
                            showResult(line, location);
                        else
                            rcode = 2;
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine("[{0}] produced error [{1}]", last, ex.Message);
                    }

                return rcode;
            }

            // stdin
            string sin;
            if (System.Console.WindowWidth == 0 && System.Console.WindowHeight == 0)
                while ((sin = last = Console.ReadLine()) != null)
                try
                {
                    LocationResults location = getCachedLocation(sin);
                    if (location != null)
                        showResult(sin, location);
                    else
                        rcode = 2;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[{0}] produced error [{1}]", last, ex.Message);
                }

            // all the arguments must be locations then
            foreach(var term in args)
                try
                {
                    last = term;

                    LocationResults location = getCachedLocation(term);
                    if (location != null)
                        showResult(term, location);
                    else
                        rcode = 2;
                }
                catch(Exception ex)
                {
                    Console.WriteLine("[{0}] produced error [{1}]", last, ex.Message);
                }

#if DEBUG
            Console.WriteLine("Press [enter] to end");
            Console.ReadLine();
#endif

            return rcode;
        }

        static void showUsage()
        {
            Console.WriteLine("Usage:         CoordinatesOf [filename]");
            Console.WriteLine("               CoordinatesOf \"[search location1]\" \"[search location2]\"");
            Console.WriteLine("  [location] | CoordinatesOf");
        }
        static void showResult(string orig, LocationResults location)
        {
            Console.WriteLine("Searched:\t{0}", orig);
            Console.WriteLine("Cached:\t{0}", location.IsCached);
            Console.WriteLine("Address:\t{0}", location.Formatted);
            Console.WriteLine("Type:\t{0}", location.FormattedAs);

            Console.WriteLine("Lat:\t{0}", location.Lat);
            Console.WriteLine("Long:\t{0}", location.Lon);
            Console.WriteLine("Precision:\t{0}", location.Precision);
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

        public class AddressComponent 
        {
            public string Long { get; set; }
            public string Short{ get; set; }
            public List<string> Category = new List<string>();
        }

        public class LocationResults
        {
            public bool IsCached { get; set; } 
            public double Lat { get; set; } // + means West
            public double Lon { get; set; } // - means North
            public string Precision { get; set; } // returned value

            public string PlaceID { get; set; }
            public string Formatted { get; set; }
            public string FormattedAs { get; set; }
            public readonly List<AddressComponent> Components = new List<AddressComponent>();
        }

        public static void WriteXML(LocationResults store, string f)
        {
            XmlSerializer writer = new XmlSerializer(typeof(LocationResults));

            var path = f;
            using (FileStream file = System.IO.File.Create(path))
            {
                writer.Serialize(file, store);
                file.Close();
            }
        }

        public static LocationResults ReadXML(string f)
        {
            LocationResults myObject;

            // Construct an instance of the XmlSerializer with the type
            // of object that is being deserialized.
            XmlSerializer mySerializer = new XmlSerializer(typeof(LocationResults));

            // To read the file, create a FileStream.
            bool isbad = false;
            using(FileStream myFileStream = new FileStream(f, FileMode.Open))
            {
                // Call the Deserialize method and cast to the object type.
                myObject = (LocationResults)mySerializer.Deserialize(myFileStream);
                myObject.IsCached = true;
            }

            return myObject;
        }

        public static bool IsLocationValid(LocationResults data)
        {
            bool good = !(data.Lat == 0 && data.Lon == 0);
            good = good && !string.IsNullOrWhiteSpace(data.Formatted);
            return good;
        }

        static public LocationResults getCachedLocation(string s)
        {
            string escaped = System.Uri.EscapeDataString(s);
            string cachefile = escaped + ".geocached";

            if (File.Exists(cachefile))
                try
                {
                    var data = ReadXML(cachefile);
                    if (!IsLocationValid(data)) throw new InvalidDataException();
                    return data;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    File.Delete(cachefile);
                }

            LocationResults result = getLocationFromGoogle(escaped);
            WriteXML(result, cachefile);

            return result;
        }

        static public LocationResults getLocationFromGoogle(string s)
        {
            // string url = "http://maps.google.com/maps/api/geocode/xml?address=1600+Amphitheatre+Parkway,+Mountain+View,+CA&sensor=false";
            string url = string.Format(@"https://maps.google.com/maps/api/geocode/xml?key={1}&address={0}&sensor=false", s, __API_KEY);

            XmlDocument doc = MakeRequest(url);

            /*
            <?xml version="1.0" encoding="UTF-8"?>
            <GeocodeResponse>
                <status>OK</status>
                <result>
                    <type>street_address</type>
                    <formatted_address>1600 Amphitheatre Pkwy, Mountain View, CA 94043, USA</formatted_address>
                    <address_component>
                        <long_name>1600</long_name>
                        <short_name>1600</short_name>
                        <type>street_number</type>
                        </address_component>
                    <address_component>
                        <long_name>Amphitheatre Parkway</long_name>
                        <short_name>Amphitheatre Pkwy</short_name>
                        <type>route</type>
                    </address_component>
                    <address_component>
                        <long_name>Mountain View</long_name>
                        <short_name>Mountain View</short_name>
                        <type>locality</type>
                        <type>political</type>
                    </address_component>
                    <address_component>
                        <long_name>Santa Clara County</long_name>
                        <short_name>Santa Clara County</short_name>
                        <type>administrative_area_level_2</type>
                        <type>political</type>
                    </address_component>
                    <address_component>
                        <long_name>California</long_name>
                        <short_name>CA</short_name>
                        <type>administrative_area_level_1</type>
                        <type>political</type>
                    </address_component>
                    <address_component>
                        <long_name>United States</long_name>
                        <short_name>US</short_name>
                        <type>country</type>
                        <type>political</type>
                    </address_component>
                    <address_component>
                        <long_name>94043</long_name>
                        <short_name>94043</short_name>
                        <type>postal_code</type>
                    </address_component>
                    <geometry>
                        <location>
                            <lat>37.4223434</lat>
                            <lng>-122.0843689</lng>
                        </location>
                        <location_type>ROOFTOP</location_type>
                        <viewport>
                            <southwest>
                                <lat>37.4209944</lat>
                                <lng>-122.0857179</lng>
                            </southwest>
                            <northeast>
                                <lat>37.4236924</lat>
                                <lng>-122.0830199</lng>
                            </northeast>
                        </viewport>
                    </geometry>
                    <place_id>ChIJ2eUgeAK6j4ARbn5u_wAGqWA</place_id>
                </result>
            </GeocodeResponse>
             */

            double dbl = 0;
            LocationResults result = new LocationResults();
            XmlNode statusnode = doc.DocumentElement.SelectSingleNode("/GeocodeResponse/status");
            if (statusnode != null)
                if (statusnode.InnerText != "OK")
                    throw new ArgumentException("Bad status received " + statusnode.InnerText);

            XmlNode latnode = doc.DocumentElement.SelectSingleNode("/GeocodeResponse/result/geometry/location/lat");
            if (latnode != null && double.TryParse(latnode.InnerText, out dbl))
                result.Lat = dbl;

            XmlNode lngnode = doc.DocumentElement.SelectSingleNode("/GeocodeResponse/result/geometry/location/lng");
            if (lngnode != null && double.TryParse(lngnode.InnerText, out dbl))
                result.Lon = dbl;

            XmlNode pcnnode = doc.DocumentElement.SelectSingleNode("/GeocodeResponse/result/geometry/location_type");
            if (pcnnode != null)
                result.Precision = pcnnode.InnerText;

            XmlNode plcnode = doc.DocumentElement.SelectSingleNode("/GeocodeResponse/result/place_id");
            if (plcnode != null)
                result.PlaceID = plcnode.InnerText;

            XmlNode typenode = doc.DocumentElement.SelectSingleNode("/GeocodeResponse/result/type");
            if (typenode != null)
                result.FormattedAs = typenode.InnerText;

            XmlNode fmtnode = doc.DocumentElement.SelectSingleNode("/GeocodeResponse/result/formatted_address");
            if (fmtnode != null)
                result.Formatted = fmtnode.InnerText;

            XmlNodeList compnode = doc.DocumentElement.SelectNodes("/GeocodeResponse/result/address_component");
            foreach(XmlNode node in compnode)
            {
                AddressComponent component = new AddressComponent();
                XmlNode lnmnode = node.SelectSingleNode("long_name");
                if (lnmnode != null)
                    component.Long = lnmnode.InnerText;

                XmlNode snmnode = node.SelectSingleNode("short_name");
                if (snmnode != null)
                    component.Short = snmnode.InnerText;

                XmlNodeList catnode = node.SelectNodes("type");
                foreach (XmlNode cat1node in catnode)
                    component.Category.Add(cat1node.InnerText);

                result.Components.Add(component);
            }


            return result;
        }
    }

    static public partial class GoogleApiKeys
    {
        static public string GeocodeApiKey
        {
            get
            {
                var config = System.Configuration.ConfigurationManager.OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.None);
                var element = config.AppSettings.Settings["GoogleGeocodeApiKey"];
                var key = element==null ? null : element.Value;
#if DEBUG
                if (string.IsNullOrWhiteSpace(key))
                {
                    var devkey = Environment.GetEnvironmentVariable("GoogleGeocodeApiKey");
                    if (!string.IsNullOrWhiteSpace(key))
                        GoogleApiKeys.GeocodeApiKey = key = devkey;
                }
#endif
                return key;
            }
            set
            {
                var config = System.Configuration.ConfigurationManager.OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.None);
                if (config.AppSettings.Settings["GoogleGeocodeApiKey"] == null)
                    config.AppSettings.Settings.Add("GoogleGeocodeApiKey", value);
                else
                    config.AppSettings.Settings["GoogleGeocodeApiKey"].Value = value;
                config.Save(System.Configuration.ConfigurationSaveMode.Modified);
            }
        }
    }
}
