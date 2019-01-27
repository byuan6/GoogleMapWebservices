using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Net;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

using System.Threading;
using System.Threading.Tasks;
using System.Drawing.Imaging;

namespace MapAt
{
    public class GoogleMap
    {
        static string __API_KEY = GoogleApiKeys.MapsApiKey;
        static int Main(string[] args)
        {
            if (args.Length == 0 && System.Console.WindowWidth != 0 && System.Console.WindowHeight != 0)
            {
                showUsage();
                return 1;
            }

            if (Array.IndexOf(args, "-ReduceUrlTo928") >= 0)
            {
                MAX_GOOGLE_LENGTH = 928;
                args[Array.IndexOf(args, "-ReduceUrlTo928")]=null;
            }

            if (string.IsNullOrWhiteSpace(GoogleApiKeys.MapsApiKey))
            {
                Console.WriteLine("No API key for Static Maps.  Please enter:");
                Console.Write(">");
                var key = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(key))
                {
                    Console.WriteLine("Bad API key");
                    return 2;
                }
                else
                    __API_KEY = GoogleApiKeys.MapsApiKey = key;
            }

            bool loop = true;
            Thread thread = new Thread(delegate()
            {
                Console.Write("  Getting map data...");
                int counter = 0;
                while (loop)
                {
                    Console.CursorLeft = 0;
                    switch (counter)
                    {
                        case 0:
                            Console.Write("/");
                            break;
                        case 1:
                            Console.Write("-");
                            break;
                        case 2:
                            Console.Write(@"\");
                            break;
                        case 3:
                            Console.Write("|");
                            break;
                    }
                    counter++;
                    if (counter > 3) counter = 0;
                }
            }) { IsBackground = true };
            thread.Start();

            int rcode = 0;
            try
            {
                MapRequest userinput = parseInput(args);
                parseStdin(userinput);
                Bitmap location = getCachedMap(userinput);


                loop = false;
                Console.WriteLine();
                if (location != null)
                    if (userinput.NoGUI)
                        Console.WriteLine("Saved to: {0}", userinput.ToFilename());
                    else
                        showResult(string.Join(".", args), location);
                else
                    rcode = 2;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error!");
                Console.WriteLine(ex.Message);
                rcode = 10;
            }

            if (thread.IsAlive)
                thread.Abort();

            return rcode;
        }


        static public void showUsage()
        {
            Console.WriteLine("Usage:        MapAt [Center] [flags] [marker1] [marker2] [marker3] [opt:-path] [marker1] [marker2] [marker3] [opt:-marker] [marker4]");
            Console.WriteLine("Flags:");
            Console.WriteLine("      -NoGUI .....Do not show map, just output filename");
            Console.WriteLine("    Map Scaling (optional, choose 1)");
            Console.WriteLine("      -Scale1 ....Normal Sized image");
            Console.WriteLine("      -Scale2 ....Double Sized image");
            Console.WriteLine("      -Scale4 ....Quad Sized image");
            Console.WriteLine("    Magnification (optional, choose 1)");
            Console.WriteLine("      -Zoom1 .....Zoomed out to global scope");
            Console.WriteLine("                  -Zoom2 -Zoom3 -Zoom4 -Zoom5 -Zoom6 -Zoom7 -Zoom8 -Zoom9");
            Console.WriteLine("      -Zoom10 ....Zoomed to view to see several cities");
            Console.WriteLine("                  -Zoom11 -Zoom12 -Zoom13 -Zoom14 -Zoom15 ");
            Console.WriteLine("                  -Zoom16 -Zoom17 -Zoom18 -Zoom19");
            Console.WriteLine("      -Zoom20 ....Zoomed into Building scope");
            Console.WriteLine("    Map Types (optional, choose 1)");
            Console.WriteLine("      -roadmap ...Just view of map");
            Console.WriteLine("      -satellite .Satellite view");
            Console.WriteLine("      -terrain ...Roadmap view with physical relief");
            Console.WriteLine("      -hybrid ....satellite with roadmap");

            Console.WriteLine("    Marker (opt, default, multi supported) [marker option] [loc1] [loc2] ...");
            Console.WriteLine("      -marker ....Start new series of marker points");
            Console.WriteLine("      -load [file] Load from file the list of marker points");

            Console.WriteLine("    Path (opt, multi supported) [path option] [loc1] [loc2] ...");
            Console.WriteLine("      -path ....Start new series of path points");
            Console.WriteLine("      -load [file] Load from file the list of path points");

            Console.WriteLine("    Marker options");
            Console.WriteLine("      name:*  (only used when)");
            Console.WriteLine("      weight:[tiny|mid|small]");
            Console.WriteLine("      color:[0xRRGGBB | 0xAARRGGBB]");
            Console.WriteLine("      color:[black|brown|green|purple|yellow|blue|gray|orange|red|white]");
            Console.WriteLine("      fillcolor:[black|brown|green|purple|yellow|blue|gray|orange|red|white]");
            Console.WriteLine("      fillcolor:0xRRGGBB or 0xAARRGGBB");

            Console.WriteLine("    Debugging options");
            Console.WriteLine("      -ReduceUrlTo928");
        }
        static void showResult(string orig, Bitmap map)
        {
            Console.WriteLine("Searched:\t{0}", orig);
            ShowImage.ShowResult(orig, map);
        }

        public static Stream MakeRequest(string requestUrl)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(requestUrl);
                Stream result;

                // returned values are returned as a stream, then read into a string
                // String lsResponse = string.Empty;
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                        return null;

                    result = new MemoryStream();
                    response.GetResponseStream().CopyTo(result);
                    return result;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                //return null;
                throw;
            }
        }


        public static void WriteData(Stream store, string f)
        {
            store.Position = 0;
            try
            {
                using (FileStream fs = new FileStream(f, FileMode.Create))
                {
                    store.CopyTo(fs);
                    fs.Close();
                }
            }
            catch
            {
                Console.WriteLine("Problem writing to " + f);
                throw;
            }
        }

        public static Bitmap ReadData(string f)
        {
            //Bitmap myObject = new Bitmap(f);
            //return myObject;

            using (FileStream fs = File.OpenRead(f))
                return new Bitmap(f);
        }

        public class MarkerRequest
        {
            static int nextuuid = 0;
            static int getUUID()
            {
                return nextuuid++;
            }
            public readonly int Index = getUUID();

            public enum SizeOptions { tiny, mid, small }
            public Nullable<SizeOptions> Size { get; set; }

            public enum ColorOptions { black, brown, green, purple, yellow, blue, gray, orange, red, white }
            public Nullable<ColorOptions> Color { get; set; }
            public string UserColor { get; set; }
            public Color GetMatchingWindowsColor()
            {
                var custom = this.UserColor;
                var predefined = this.Color;
                if (custom != null)
                {
                    const uint mask = 255;
                    uint a = 255;
                    uint r = 0;
                    uint g = 0;
                    uint b = 0;
                    uint argb = uint.Parse(custom, System.Globalization.NumberStyles.HexNumber, null);
                    b = mask & argb;
                    if (custom.Length > 2)
                        g = mask & (argb >> 8);
                    if (custom.Length > 4)
                        r = mask & (argb >> 16);
                    if (custom.Length > 6)
                        a = mask & (argb >> 24);
                    var result = System.Drawing.Color.FromArgb(Convert.ToInt32(a), Convert.ToInt32(r), Convert.ToInt32(g), Convert.ToInt32(b));
                    return result;
                }
                else if (predefined.HasValue)
                {
                    var color = predefined.Value;
                    if (color == ColorOptions.black)
                        return System.Drawing.Color.Black;
                    if (color == ColorOptions.brown)
                        return System.Drawing.Color.FromArgb(255, 150, 118, 74); ; //96764a
                    if (color == ColorOptions.green)
                        return System.Drawing.Color.FromArgb(255, 138, 175, 0); // 8aaf00 System.Drawing.Color.Green;
                    if (color == ColorOptions.purple)
                        return System.Drawing.Color.FromArgb(255, 143, 110, 167); ; //936ea7
                    if (color == ColorOptions.yellow)
                        return System.Drawing.Color.FromArgb(255, 229, 178, 46); //e5b22e; System.Drawing.Color.Yellow;
                    if (color == ColorOptions.blue)
                        return System.Drawing.Color.FromArgb(255, 125, 171, 253); ; //7dabfd
                    if (color == ColorOptions.gray)
                        return System.Drawing.Color.FromArgb(255, 147, 110, 167); //System.Drawing.Color.Gray; //936ea7
                    if (color == ColorOptions.orange)
                        return System.Drawing.Color.FromArgb(255, 220, 133, 0); //System.Drawing.Color.Orange; //dc8500
                    if (color == ColorOptions.red)
                        return System.Drawing.Color.FromArgb(255, 233, 67, 53); //e94335 System.Drawing.Color.Red;
                    if (color == ColorOptions.white)
                        return System.Drawing.Color.White;
                    else
                        return System.Drawing.Color.Black;
                }
                else
                    return System.Drawing.Color.Black;
            }

            public Nullable<char> Label { get; set; }

            public string MarkerGroupText { get; set; }
            public bool? IncludeLegend { get; set; }

            public bool ParseAttrib(string s)
            {
                bool tf = false;
                uint i = 0;
                ColorOptions c;
                SizeOptions z;
                if (s.StartsWith("label:"))
                    if (s.Substring(6).Length == 1)
                        this.Label = s.Substring(6)[0];
                    else
                        throw new ArgumentException("Label not allowed" + s.Substring(6));

                else if (s.StartsWith("color:0x"))
                    if (uint.TryParse(s.Substring(8), System.Globalization.NumberStyles.HexNumber, null, out i) && i >= 0 && i < 16777216)
                        this.UserColor = s.Substring(8);
                    else
                        throw new ArgumentException("Color Hex is invalid " + s.Substring(8));
                else if (s.StartsWith("color:"))
                    if (Enum.TryParse<ColorOptions>(s.Substring(6), out c))
                        this.Color = c;
                    else
                        throw new ArgumentException("unknown color " + s.Substring(6));

                else if (s.StartsWith("size:"))
                    if (Enum.TryParse<SizeOptions>(s.Substring(5), out z))
                        this.Size = z;
                    else
                        throw new ArgumentException("unknown size " + s.Substring(5));

                else if (s.StartsWith("name:"))
                    this.MarkerGroupText = s.Substring(5);
                else if (s.StartsWith("legend:"))
                    if (bool.TryParse(s.Substring(7), out tf))
                        this.IncludeLegend = tf;
                    else
                        throw new ArgumentException("unknown value for legend (true/false): " + s.Substring(7));

                else if (s == "-marker") //not really an marker option, but everything that's not a recognized marker option, is assumed to be a marker location
                    return true;

                else
                    return false;
                //throw new ArgumentException("unknown attribute:" + s);

                return true;
            }

            public override string ToString()
            {
                if (this.Size == null && this.Color == null && this.UserColor == null && this.Label == null)
                    return null;

                StringBuilder sb = new StringBuilder();
                if (this.Size != null)
                    sb.AppendFormat("size:{0}", this.Size.ToString());

                if (this.Color != null || this.UserColor != null)
                {
                    if (sb.Length > 0)
                        sb.Append("%7C");
                    if (this.UserColor == null)
                        sb.AppendFormat("color:{0}", this.Color.ToString());
                    else
                        sb.AppendFormat("color:{0}", this.UserColor);
                }

                if (this.Label != null)
                {
                    if (sb.Length > 0)
                        sb.Append("%7C");
                    sb.AppendFormat("label:{0}", this.Label);
                }

                return sb.ToString();
            }
        }

        public class PathRequest
        {
            static int nextuuid = 0;
            static int getUUID()
            {
                return nextuuid++;
            }

            public readonly int Index = getUUID();
            public Nullable<int> Weight { get; set; } //If no weight parameter is set, the path will appear in its default thickness (5 pixels).

            public enum ColorOptions { black, brown, green, purple, yellow, blue, gray, orange, red, white }
            public Nullable<ColorOptions> Color { get; set; }
            public string UserColor { get; set; } // 32-bit hexadecimal value (example: color=0xFFFFCCFF), the last two characters specify the 8-bit alpha transparency value

            public Nullable<ColorOptions> FillColor { get; set; }
            public string UserFillColor { get; set; }
            public Nullable<bool> Geodesic { get; set; }  //not set, is false

            public bool ParseAttrib(string s)
            {
                uint i = 0;
                ColorOptions c;
                if (s.StartsWith("weight:"))
                    if (uint.TryParse(s.Substring(7), out i) && i > 0 && i < 10)
                        this.Weight = Convert.ToInt32(i);
                    else
                        throw new ArgumentException("Weight is not allowed " + s.Substring(7));

                else if (s.StartsWith("color:0x"))
                {
                    if (uint.TryParse(s.Substring(8), System.Globalization.NumberStyles.HexNumber, null, out i) && i >= 0 && i < 16777216)
                        this.UserColor = s.Substring(6);
                    else
                        throw new ArgumentException("Color Hex is invalid (0x000000).  No argb support(just lazy bc a=0 means it's transparent, ff==0000ff is blue) " + s.Substring(8));
                }
                else if (s.StartsWith("color:"))
                    if (Enum.TryParse<ColorOptions>(s.Substring(6), out c))
                        this.Color = c;
                    else
                        throw new ArgumentException("unknown color " + s.Substring(6) + ". Use " + allenumtostring(typeof(ColorOptions)));

                else if (s.StartsWith("fillcolor:"))
                    if (Enum.TryParse<ColorOptions>(s.Substring(10), out c))
                        this.Color = c;
                    else
                        throw new ArgumentException("unknown color " + s.Substring(10));
                else if (s.StartsWith("fillcolor:0x"))
                    if (uint.TryParse(s.Substring(12), System.Globalization.NumberStyles.HexNumber, null, out i) && i >= 0 && i < int.MaxValue)
                        this.UserFillColor = s.Substring(10);
                    else
                        throw new ArgumentException("Color Hex is invalid" + s.Substring(12));

                else
                    return false;
                //throw new ArgumentException("unknown attribute:" + s);

                return true;
            }

            string allenumtostring(Type enumerationtype)
            {
                if (!enumerationtype.IsEnum)
                    return null;
                StringBuilder sb = new StringBuilder();
                foreach (var item in Enum.GetValues(enumerationtype))
                {
                    sb.Append(item);
                    sb.Append(" ");
                }
                return sb.ToString();
            }

            public override string ToString()
            {
                if (this.Weight == null && this.Color == null && this.FillColor == null && this.Geodesic == null && this.UserColor == null)
                    return null;

                StringBuilder sb = new StringBuilder();
                if (this.Weight != null)
                    sb.AppendFormat("weight:{0}", (int)this.Weight);

                if (this.Color != null || this.UserColor != null)
                {
                    if (sb.Length > 0)
                        sb.Append("%7C");
                    if (this.UserColor == null)
                        sb.AppendFormat("color:{0}", this.Color.ToString());
                    else
                        sb.AppendFormat("color:{0}", this.UserColor);
                }
                if (this.FillColor != null && this.UserFillColor == null)
                {
                    if (sb.Length > 0)
                        sb.Append("%7C");
                    if (this.UserFillColor == null)
                        sb.AppendFormat("fillcolor:{0}", this.FillColor.ToString());
                    else
                        sb.AppendFormat("fillcolor:{0}", this.UserFillColor);
                }

                if (this.Geodesic != null)
                {
                    if (sb.Length > 0)
                        sb.Append("%7C");
                    sb.AppendFormat("geodesic:{0}", this.Geodesic);
                }

                return sb.ToString();
            }

            /// <summary>
            /// https://developers.google.com/maps/documentation/utilities/polylinealgorithm
            /// </summary>
            /// <returns></returns>
            public string ToPolyline(IEnumerable<PathCoordinate> list)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("enc:");

                PathCoordinate? last = null;
                foreach (var item in list)
                {
                    if (last.HasValue)
                        sb.Append(item.ToPolyline(last.Value));
                    else
                        sb.Append(item.ToPolyline());
                }

                return sb.ToString();
            }

            public struct PathCoordinate
            {
                public PathCoordinate(double lat, double lon)
                {
                    this.Lat = lat;
                    this.Lon = lon;
                }

                public double Lat;
                public double Lon;

                public string ToPolyline()
                {
                    int count = 0;
                    char[] polyline = new char[7];

                    var lat = (Int32)Math.Round(this.Lat * 5);
                    var left = (lat << 1);
                    if (lat < 0)
                        left = ~left;
                    var mask = 63;
                    var follows = 0;
                    while (left != 0)
                    {
                        var chunk = mask & left;
                        if (follows != 0)
                            chunk = chunk | follows;
                        var ch = (char)(chunk + 63);
                        polyline[count++] = ch;
                        left = left >> 5;

                        follows = 0x20;
                    }
                    return new string(polyline, 0, count);
                }
                public string ToPolyline(PathCoordinate prev)
                {
                    throw new NotImplementedException();
                }
            }
        }
        public class MapRequest
        {
            public const int DEFAULT_SIZE = 640;

            public MapRequest()
            {
                this.Type = MapType.roadmap;
                this.Size = 640;
                this.Scale = ScaleOptions.Two;
                this.Zoom = ZoomOptions.Fourteen;
            }

            public bool NoGUI { get; set; }

            public enum ScaleOptions
            {
                One = 1, Two = 2, Four = 4
            }
            public enum ZoomOptions
            {
                One = 1, //World
                Two = 2, Three = 3, Four = 4, Five = 5, Six = 6, Seven = 7, Eight = 8, Nine = 9,
                Ten = 10, //City
                Eleven = 11, Twelve = 12, Thirteen = 13, Fourteen = 14, Fifteen = 15, Sixteen = 16, Seventeen = 17, Eighteen = 18, Nineteen = 19,
                Twenty = 20 //Buildings
            }
            public enum MapType
            {
                roadmap, satellite, terrain, hybrid
            }

            public Nullable<MapType> Type { get; set; }
            public string Center { get; set; }
            public int Size { get; set; }
            public ZoomOptions Zoom { get; set; }
            public ScaleOptions Scale { get; set; }
            public readonly Dictionary<MarkerRequest, List<string>> Markers = new Dictionary<MarkerRequest, List<string>>();
            public readonly Dictionary<PathRequest, List<string>> Paths = new Dictionary<PathRequest, List<string>>();
            public modeoptions LastMode { get; set; }
            public int StdinCount { get; set; }

            public string ToFilename()
            {
                StringBuilder markall = new StringBuilder();
                StringBuilder pathall = new StringBuilder();

                bool skip = false;
                foreach (var m in this.Markers)
                {
                    string delimiter = ".";
                    string options = m.Key.ToString();
                    if (options != null)
                    {
                        markall.Append(fileescape(options));
                        delimiter = ".";
                    }
                    foreach (var spot in m.Value)
                    {
                        markall.Append(delimiter);
                        markall.Append(fileescape(spot));
                        delimiter = ".";
                    }
                }

                foreach (var p in this.Paths)
                {
                    string delimiter = ".";
                    string options = p.Key.ToString();
                    if (options != null)
                    {
                        pathall.Append(fileescape(options));
                        delimiter = ".";
                    }
                    foreach (var spot in p.Value)
                    {
                        pathall.Append(delimiter);
                        pathall.Append(fileescape(spot));
                        delimiter = ".";
                    }
                }

                string name = string.Format("{0}.{1}.{2}.{3}.{4}{5}{6}.{7}.mapcached.png",
                    this.fileescape(this.Center), this.Zoom.ToString(), this.Size.ToString(), this.Scale.ToString(), this.fileescape(this.Type.ToString()),
                    markall.ToString(),
                    pathall.ToString(),
                    this.StdinCount);

                System.Diagnostics.Debug.WriteLine(compressMD5Hash(name));
                if (name.Length > 248)
                    name = name.Substring(0, 200) + "." + compressMD5Hash(name) + ".mapcached.png";

                int cdl = Environment.CurrentDirectory.Length;
                if (cdl + name.Length > 260)
                    name = name.Substring(0, 210 - cdl) + "." + compressMD5Hash(name) + ".mapcached.png";

                return name;

            }

            string calculateMD5Hash(string input)
            {
                // step 1, calculate MD5 hash from input
                System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
                byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
                byte[] hash = md5.ComputeHash(inputBytes);

                // step 2, convert byte array to hex string
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("X2"));
                }

                return sb.ToString();
            }
            string compressMD5Hash(string input)
            {
                // step 1, calculate MD5 hash from input
                System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
                byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
                byte[] hash = md5.ComputeHash(inputBytes);


                return Convert.ToBase64String(hash).Replace("=", "").Replace("/", "-");
            }



            string fileescape(string candidate)
            {
                char[] invalid = Path.GetInvalidFileNameChars();
                string name = candidate.Replace("%20", "+").Replace(" ", "").Replace(":", "_");
                var escaped = name.Select<char, char>(s => Array.IndexOf<char>(invalid, s) >= 0 ? '_' : s).ToArray<char>();

                return new string(escaped);
            }




            /// <summary>
            /// To Url
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                StringBuilder markall = new StringBuilder();
                StringBuilder pathall = new StringBuilder();

                foreach (var m in this.Markers)
                {
                    string delimiter = "&markers=";
                    string options = m.Key.ToString();
                    if (options != null)
                    {
                        markall.Append(delimiter);
                        markall.Append(options);
                        delimiter = "%7C";
                    }
                    foreach (var spot in m.Value)
                    {
                        markall.Append(delimiter);
                        markall.Append(System.Uri.EscapeDataString(spot).Replace("%20", "+"));
                        delimiter = "%7C";
                    }
                }

                foreach (var p in this.Paths)
                {
                    string delimiter = "&path=";
                    string options = p.Key.ToString();
                    if (options != null)
                    {
                        pathall.Append(delimiter);
                        pathall.Append(options);
                        delimiter = "%7C";
                    }
                    foreach (var spot in p.Value)
                    {
                        pathall.Append(delimiter);
                        pathall.Append(System.Uri.EscapeDataString(spot).Replace("%20", "+"));
                        delimiter = "%7C";
                    }
                }

                // google supports https, but in linux, the certificate handling in the http object is not working
                return string.Format("https://maps.googleapis.com/maps/api/staticmap?key={7}&center={0}&zoom={1}&size={2}x{2}&scale={3}&maptype={4}{5}{6}",
                    System.Uri.EscapeDataString(this.Center), (int)this.Zoom, (int)this.Size, (int)this.Scale, System.Uri.EscapeDataString(this.Type.ToString()),
                    markall.ToString(),
                    pathall.ToString(),
                    __API_KEY);
            }
        }

        public enum modeoptions { marker, path };
        static public MapRequest parseInput(string[] args)
        {
            //enum modeoptions {marker, path};
            modeoptions mode = modeoptions.marker;
            MarkerRequest mr = null;//new MarkerRequest();
            PathRequest pr = null; // new PathRequest();

            MapRequest req = new MapRequest();
            req.Center = args[0];
            //req.Markers.Add(mr, new List<string>() { req.Center });

            for (int i = 1; i < args.Length; i++)
            {
                string item = args[i];
                switch (item)
                {
                    case "-NoGUI":
                        req.NoGUI = true;
                        break;

                    case "-Scale1":
                    case "-Scale2":
                    case "-Scale4":
                        int scale = int.Parse(item.Substring(6));
                        req.Scale = (MapRequest.ScaleOptions)scale;
                        break;

                    case "-Zoom1":
                    case "-Zoom2":
                    case "-Zoom3":
                    case "-Zoom4":
                    case "-Zoom5":
                    case "-Zoom6":
                    case "-Zoom7":
                    case "-Zoom8":
                    case "-Zoom9":
                    case "-Zoom10":
                    case "-Zoom11":
                    case "-Zoom12":
                    case "-Zoom13":
                    case "-Zoom14":
                    case "-Zoom15":
                    case "-Zoom16":
                    case "-Zoom17":
                    case "-Zoom18":
                    case "-Zoom19":
                    case "-Zoom20":
                        int zoom = int.Parse(item.Substring(5));
                        req.Zoom = (MapRequest.ZoomOptions)zoom;
                        break;

                    case "-roadmap":
                    case "-satellite":
                    case "-terrain":
                    case "-hybrid":
                        req.Type = (MapRequest.MapType)Enum.Parse(typeof(MapRequest.MapType), item.Substring(1));
                        break;

                    case "-marker":
                        mode = modeoptions.marker;
                        if (req.Markers.Count != 0 || mr == null)
                        {
                            mr = new MarkerRequest();
                            req.Markers.Add(mr, new List<string>());
                        }
                        break;
                    case "-path":
                        mode = modeoptions.path;
                        if (req.Paths.Count != 0 || pr == null)
                        {
                            pr = new PathRequest();
                            req.Paths.Add(pr, new List<string>());
                        }
                        break;

                    case "-load":
                        i++;
                        var filename = args[i];
                        foreach (var line in File.ReadAllLines(filename))
                            if (mode == modeoptions.marker)
                            {
                                if (!mr.ParseAttrib(line))
                                    req.Markers[mr].Add(line);
                            }
                            else if (mode == modeoptions.path)
                            {
                                if (!pr.ParseAttrib(line))
                                    req.Paths[pr].Add(line);
                            }
                        //Console.WriteLine("Loading into " + mode.ToString() + " "  +mr.Index + " " + pr.Index);
                        break;

                    default:
                        if (item != null)
                        {
                            if (item.StartsWith("-") && !isDecimalCoordinate(item).HasValue)
                                throw new ArgumentException("Unknown option " + item);
                            if (mode == modeoptions.marker)
                            {
                                if (!mr.ParseAttrib(item))
                                    req.Markers[mr].Add(item);
                            }
                            if (mode == modeoptions.path)
                            {
                                if (!pr.ParseAttrib(item))
                                    req.Paths[pr].Add(item);
                            }
                            //Console.WriteLine("Adding into " + mode.ToString()+" " + mr.Index + " " + pr.Index);
                        }
                        break;
                }
            }
            req.LastMode = mode;
            return req;
        }
        static Nullable<PointF> isDecimalCoordinate(string value)
        {
            var parts = value.Split(',');
            if (parts.Length != 2)
                return null;
            float lat;
            float lon;
            if (!float.TryParse(parts[0], out lat))
                return null;
            if (!float.TryParse(parts[1], out lon))
                return null;
            PointF result = new PointF(lat, lon);
            return result;
        }
        static public void parseStdin(MapRequest req)
        {
            try
            {
                var test = Console.KeyAvailable;
            }
            catch
            {
                if (req.LastMode == modeoptions.marker)
                {
                    MarkerRequest mr = null;
                    if (req.Markers.Count == 0)
                        req.Markers.Add(mr = new MarkerRequest(), new List<string>());
                    else
                        foreach (var item in req.Markers)
                            if (mr == null || item.Key.Index > mr.Index)
                                mr = item.Key;
                    // Console.WriteLine("stdin mr " + mr.Index + " / " + req.Markers.Count);
                    while (Console.In.Peek() >= 0)
                    {
                        var line = Console.ReadLine();
                        //Console.WriteLine(Console.In.Peek()+ "marker " + line);
                        req.Markers[mr].Add(line);
                        req.StdinCount++;
                    }
                }
                else
                {
                    PathRequest pr = null;
                    if (req.Paths.Count == 0)
                        req.Paths.Add(pr = new PathRequest(), new List<string>());
                    else
                        foreach (var item in req.Paths)
                            if (pr == null || item.Key.Index > pr.Index)
                                pr = item.Key;
                    // Console.WriteLine("stdin pr " + pr.Index + " / " + req.Paths.Count);
                    while (Console.In.Peek() >= 0)
                    {
                        var line = Console.ReadLine();
                        //Console.WriteLine(Console.In.Peek() + "path " + line);
                        req.Paths[pr].Add(line);
                        req.StdinCount++;
                    }
                }
            }
        }

        static TimeSpan __30_DAYS = new TimeSpan(30, 0, 0, 0);
        static public Bitmap getCachedMap(MapRequest userinput)
        {
            string cachefile = userinput.ToFilename();

            if (File.Exists(cachefile) && DateTime.Now - (new FileInfo(cachefile)).LastWriteTime < __30_DAYS)
                try
                {
                    return ReadData(cachefile);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    File.Delete(cachefile);
                }

            Bitmap result;
            using (var raw = getMapFromGoogle(userinput))
            {
                WriteData(raw, cachefile);
                result = new Bitmap(raw);
            }

            return result;
        }

        static int MAX_GOOGLE_LENGTH = 8192;
        static public Stream getMapFromGoogle(MapRequest userinput)
        {
            // string url = "https://maps.googleapis.com/maps/api/staticmap?center=latitude,long&zoom=14&size=640x640&scale=2"; //max free is 640 by 640
            // &maptype=terrain
            //https://maps.googleapis.com/maps/api/staticmap?center=Berkeley,CA&zoom=14&size=640x640&scale=2&maptype=terrain&markers=label:11-11-16|College+Ave+Berkeley+CA&markers=label:13|Hopkins+St+Berkeley+CA&path=color:0x0000ff|weight:5|sacramento+st+Berkeley+CA|University+ave+Berkeley+CA

            string url = userinput.ToString();
            if (url.Length > MAX_GOOGLE_LENGTH)
                return splitMapFromGoogle(userinput);
            else
            {
                System.Diagnostics.Debug.WriteLine(url);
#if DEBUG
                Console.WriteLine(url);
#endif
                Stream result = MakeRequest(url);

                return result;
            }
        }

        static public Stream splitMapFromGoogle(MapRequest userinput)
        {
            try
            {
                //get just the map
                MapRequest justmapreq = cloneWoFeatures(userinput);
                using (var justmapRaw = getCachedMap(justmapreq))
                using (var comparer = new NaiveDifference(justmapRaw))
                {
                    //List<Bitmap> markermaps = new List<Bitmap>();
                    //List<Bitmap> pathmaps = new List<Bitmap>();
                    var requests = new Dictionary<MapRequest, Bitmap>();
                    foreach (var item in splitRequestToGoogle(userinput))
                    {
#if DEBUG
                        Console.WriteLine("-->" + item.ToString());
#endif
                        requests.Add(item, null);
                    }
                    var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
                    Parallel.ForEach(requests.Keys.ToArray(), options, new Action<MapRequest>(delegate(MapRequest subreq)
                    {
                        if (subreq.ToString().Length > MAX_GOOGLE_LENGTH)
                            throw new InvalidProgramException("request too long " + subreq.ToString());

                        using (var pathBMP = getCachedMap(subreq))
                        {
                            var delta = comparer.Compare(pathBMP);
                            lock (requests)
                                requests[subreq] = delta;
                        }
                    }));

                    
                    var rect = new Rectangle(0,0,justmapRaw.Width, justmapRaw.Height);
                    using (var composite = NaiveDifference.CloneBitmap(justmapRaw))
                    {
                        using (var g = Graphics.FromImage(composite))
                        {
                            foreach (var item in requests.Values)
                            {
                                g.DrawImage(item, rect);
                                item.Dispose();
                            }
                        }

                        //
                        MemoryStream ms = new MemoryStream();
                        composite.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        ms.Position = 0;
                        return ms;
                    }
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
#endif
                throw;
            }
        }

        static IEnumerable<MapRequest> splitRequestToGoogle(MapRequest userinput)
        {
            if(userinput.ToString().Length<MAX_GOOGLE_LENGTH)
            {
                yield return userinput;
            } 
            else
            {
                MapRequest req = cloneWoFeatures(userinput);
                if (userinput.Paths.Count > 1)
                {
                    MapRequest req1 = cloneWoFeatures(req);
                    MapRequest req2 = cloneWoFeatures(req);
                    var half = userinput.Paths.Count;
                    var count=0;
                    foreach(var item in userinput.Paths)
                    {
                        if(count<half)
                            req1.Paths.Add(item.Key, item.Value);
                        if(count>=half)
                            req2.Paths.Add(item.Key, item.Value);
                        foreach(var subreq in splitRequestToGoogle(req1))
                            yield return subreq;
                        foreach(var subreq in splitRequestToGoogle(req2))
                            yield return subreq;
                        count++;
                    }
                }
                else if (userinput.Paths.Count == 1)
                {
                    var key = userinput.Paths.Keys.First();
                    var value = userinput.Paths.Values.First();
                    var count = value.Count;
                    var half = count/2;
                    MapRequest req1 = cloneWoFeatures(req);
                    MapRequest req2 = cloneWoFeatures(req);
                    req1.Paths.Add(key, value.Take(half+1).ToList());
                    req2.Paths.Add(key, value.Skip(half).ToList());
                    foreach (var subreq in splitRequestToGoogle(req1))
                        yield return subreq;
                    foreach (var subreq in splitRequestToGoogle(req2))
                        yield return subreq;
                }
                if (userinput.Markers.Count > 1)
                {
                    MapRequest req1 = cloneWoFeatures(req);
                    MapRequest req2 = cloneWoFeatures(req);
                    var half = userinput.Markers.Count;
                    var count = 0;
                    foreach (var item in userinput.Markers)
                    {
                        if (count < half)
                            req1.Markers.Add(item.Key, item.Value);
                        if (count >= half)
                            req2.Markers.Add(item.Key, item.Value);
                        foreach (var subreq in splitRequestToGoogle(req1))
                            yield return subreq;
                        foreach (var subreq in splitRequestToGoogle(req2))
                            yield return subreq;
                        count++;
                    }
                }
                else if (userinput.Markers.Count == 1)
                {
                    var key = userinput.Markers.Keys.First();
                    var value = userinput.Markers.Values.First();
                    var count = value.Count;
                    var half = count / 2;
                    MapRequest req1 = cloneWoFeatures(req);
                    MapRequest req2 = cloneWoFeatures(req);
                    req1.Markers.Add(key, value.Take(half + 1).ToList());
                    req2.Markers.Add(key, value.Skip(half).ToList());
                    foreach (var subreq in splitRequestToGoogle(req1))
                        yield return subreq;
                    foreach (var subreq in splitRequestToGoogle(req2))
                        yield return subreq;
                }
            }
        }

        static public MapRequest cloneWoFeatures(MapRequest userinput)
        {
            MapRequest clone = new MapRequest()
            {
                Center = userinput.Center,
                Zoom = userinput.Zoom,
                Type = userinput.Type,
                Size = userinput.Size,
                Scale = userinput.Scale,
                NoGUI = userinput.NoGUI,
                LastMode = userinput.LastMode,
                StdinCount = userinput.StdinCount
            };

            return clone;
        }
    }


    public class NaiveDifference : IDisposable
    {
        public NaiveDifference(Bitmap bmp)
        {
            var size = bmp.Size;
            var rect = new Rectangle(new Point(0, 0), size);
            var bmp32 = this.Background = CloneBitmap(bmp); //bmp.Clone(rect, PixelFormat.Format32bppArgb);
            var rowsize = _rowsize = size.Width * 4;
            //var bytesize = size.Height * rowsize;
            //_background = new byte[bytesize];

            //var locked = bmp32.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            //bmp32.UnlockBits(locked);

            _bg = bmp32.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        }

        public Bitmap Background { get; private set; }
        public Color DifferenceMargin = Color.FromArgb(255, 13, 13, 13);
        int _rowsize = 0;
        //byte[] _background = null;
        BitmapData _bg = null;
        //int _bytesread = 0;

        public Bitmap Compare(Bitmap bmp)
        {
            var bg = this.Background;

            var size = bmp.Size;
            var rect = new Rectangle(new Point(0, 0), size);
            if (size != bg.Size)
                throw new ArgumentException("Images do not match");


            //using (var bmp32 = bmp.Clone(rect, PixelFormat.Format32bppArgb))
            using (var bmp32 = CloneBitmap(bmp))
            {
                var read = bmp32.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

                var delta = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
                var write = delta.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                var margin = this.DifferenceMargin;
                var ma = margin.A;
                var mr = margin.R;
                var mg = margin.G;
                var mb = margin.B;
                unsafe
                {
                    var stride = read.Stride;
                    var src = (byte*)read.Scan0;
                    var dest = (byte*)write.Scan0;
                    var same = (byte*)_bg.Scan0;
                    var width = _rowsize;

                    for (int y = 0; y < size.Height; y++)
                    {
                        var start1 = same;
                        var start2 = src;
                        var start3 = dest;
                        for (int i = 0; i < size.Width; i++)
                        {
                            var db = Math.Abs(same[0] - src[0]);
                            var dg = Math.Abs(same[1] - src[1]);
                            var dr = Math.Abs(same[2] - src[2]);
                            var da = Math.Abs(same[3] - src[3]);
                            if (db > mb || dg > mg || dr > mr || da > ma)
                            {
                                dest[0] = src[0];
                                dest[1] = src[1];
                                dest[2] = src[2];
                                dest[3] = src[3];
                            }

                            same += 4;
                            src += 4;
                            dest += 4;
                        }
                        same = start1 + stride;
                        src = start2 + stride;
                        dest = start3 + stride;
                    }
                }

                delta.UnlockBits(write);
                bmp32.UnlockBits(read);
                //bg.UnlockBits(bg2);

                return delta;
            }
        }

        static public Bitmap CloneBitmap(Bitmap bmp)
        {
            //Bitmap orig = new Bitmap(@"c:\temp\24bpp.bmp");
            Bitmap clone = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(clone))
            {
                g.DrawImage(bmp, new Rectangle(0, 0, clone.Width, clone.Height));
            }
            return clone;
        }

        public void Dispose()
        {
            if(_bg!=null)
                this.Background.UnlockBits(_bg);
            this.Background.Dispose();
        }
    }
    

    static public partial class GoogleApiKeys
    {
        static public string MapsApiKey
        {
            get
            {
                var config = System.Configuration.ConfigurationManager.OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.None);
                var element = config.AppSettings.Settings["GoogleMapsApiKey"];
                var key = element == null ? null : element.Value;
#if DEBUG
                if (string.IsNullOrWhiteSpace(key))
                {
                    var devkey = Environment.GetEnvironmentVariable("GoogleMapsApiKey");
                    if (!string.IsNullOrWhiteSpace(key))
                        GoogleApiKeys.MapsApiKey = key = devkey;
                }
#endif
                return key;
            }
            set
            {
                var config = System.Configuration.ConfigurationManager.OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.None);
                if (config.AppSettings.Settings["GoogleMapsApiKey"] == null)
                    config.AppSettings.Settings.Add("GoogleMapsApiKey", value);
                else
                    config.AppSettings.Settings["GoogleMapsApiKey"].Value = value;
                config.Save(System.Configuration.ConfigurationSaveMode.Modified);
            }
        }
    }
}
