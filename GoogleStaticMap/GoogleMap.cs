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

            if(thread.IsAlive)
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
            public Nullable<SizeOptions> Size { get;set; }

            public enum ColorOptions { black, brown, green, purple, yellow, blue, gray, orange, red, white }
            public Nullable<ColorOptions> Color { get;set; }
            public string UserColor {get;set;}
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
                    if(custom.Length>2)
                        g = mask & (argb>>8);
                    if (custom.Length > 4)
                        r = mask & (argb >> 16);
                    if (custom.Length > 6)
                        a = mask & (argb >> 24);
                    var result = System.Drawing.Color.FromArgb(Convert.ToInt32(a),Convert.ToInt32(r),Convert.ToInt32(g),Convert.ToInt32(b));
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
                        return System.Drawing.Color.FromArgb(255,229,178,46); //e5b22e; System.Drawing.Color.Yellow;
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
                else if(s.StartsWith("legend:"))
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
                if (this.Size!=null)
                    sb.AppendFormat("size:{0}", this.Size.ToString());
                
                if(this.Color!=null || this.UserColor!=null)
                {
                    if(sb.Length>0)
                        sb.Append("%7C");
                    if(this.UserColor==null)
                        sb.AppendFormat("color:{0}", this.Color.ToString());
                    else
                        sb.AppendFormat("color:{0}", this.UserColor);
                }

                if(this.Label!=null)
                {
                    if(sb.Length>0)
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
            public Nullable<int> Weight {get;set;} //If no weight parameter is set, the path will appear in its default thickness (5 pixels).

            public enum ColorOptions { black, brown, green, purple, yellow, blue, gray, orange, red, white }
            public Nullable<ColorOptions> Color { get;set; }
            public string UserColor {get;set;} // 32-bit hexadecimal value (example: color=0xFFFFCCFF), the last two characters specify the 8-bit alpha transparency value

            public Nullable<ColorOptions> FillColor {get;set;}
            public string UserFillColor { get; set; }
            public Nullable<bool> Geodesic { get; set; }  //not set, is false

            public bool ParseAttrib(string s)
            {
                uint i=0;
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
                One = 1, Two=2, Four=4
            }
            public enum ZoomOptions
            {
                One = 1, //World
                Two = 2, Three=3, Four=4, Five=5, Six=6, Seven=7, Eight=8, Nine=9, 
                Ten=10, //City
                Eleven=11, Twelve=12, Thirteen=13, Fourteen=14, Fifteen=15, Sixteen=16, Seventeen=17, Eighteen=18, Nineteen=19, 
                Twenty=20 //Buildings
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
                    if (!skip)
                    {
                        skip = true;
                        continue;
                    }
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
                if(cdl + name.Length > 260)
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

                
                return Convert.ToBase64String(hash).Replace("=","").Replace("/","-");
            }



            string fileescape(string candidate)
            {
                char[] invalid = Path.GetInvalidFileNameChars();
                string name = candidate.Replace("%20", "+").Replace(" ","").Replace(":","_");                
                var escaped = name.Select<char, char>(s=>Array.IndexOf<char>(invalid,s)>=0 ? '_' : s).ToArray<char>();

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
                    string delimiter="&markers=";
                    string options = m.Key.ToString();
                    if (options != null)
                    {
                        markall.Append(delimiter);
                        markall.Append(options);
                        delimiter = "|";
                    }
                    foreach(var spot in m.Value)
                    {
                        markall.Append(delimiter);
                        markall.Append(System.Uri.EscapeDataString(spot).Replace("%20","+"));
                        delimiter = "|";
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
                        delimiter = "|";
                    }
                    foreach (var spot in p.Value)
                    {
                        pathall.Append(delimiter);
                        pathall.Append(System.Uri.EscapeDataString(spot).Replace("%20", "+"));
                        delimiter = "|";
                    }
                }

                // google supports https, but in linux, the certificate handling in the http object is not working
                return string.Format("https://maps.googleapis.com/maps/api/staticmap?key={7}&center={0}&zoom={1}&size={2}x{2}&scale={3}&maptype={4}{5}{6}",
                    System.Uri.EscapeDataString(this.Center), (int)this.Zoom,(int)this.Size,(int)this.Scale, System.Uri.EscapeDataString(this.Type.ToString()),
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

                    case "-markers":
                        mode = modeoptions.marker;
                        if (req.Markers.Count != 0 || mr==null)
                        {
                            mr = new MarkerRequest();
                            req.Markers.Add(mr, new List<string>());
                        }
                        break;
                    case "-path":
                        mode = modeoptions.path;
                        if (req.Paths.Count != 0 || pr==null)
                        {
                            pr = new PathRequest();
                            req.Paths.Add(pr, new List<string>());
                        }
                        break;

                    case "-load":
                        i++;
                        var filename = args[i];
                        foreach(var line in File.ReadAllLines(filename))
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
                            if(mr==null || item.Key.Index > mr.Index)
                                mr = item.Key;
                    // Console.WriteLine("stdin mr " + mr.Index + " / " + req.Markers.Count);
                    while (Console.In.Peek()>=0)
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

        static public Bitmap getCachedMap(MapRequest userinput)
        {
            string cachefile = userinput.ToFilename();

            if (File.Exists(cachefile))
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
                var justmapRaw = getMapFromGoogle(justmapreq);
                Bitmap justmapBMP = null;
                using(var justmapIndexedBMP = new Bitmap(justmapRaw))
                    justmapBMP = justmapIndexedBMP.Clone(new Rectangle(0,0,justmapIndexedBMP.Width, justmapIndexedBMP.Height), System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                var justmapBlock = (new LockBitmap(justmapBMP)).TwoD;

                // get the markers... abort if the markers still exceed 2048
                int len = userinput.Markers.Count + userinput.Paths.Count;
                MapRequest[] req = new MapRequest[len];
                //Bitmap[] bmp = new Bitmap[len];
                List<List<Pixel>> completed = new List<List<Pixel>>();

                var pathkeys = userinput.Paths.Keys.ToArray();
                var markerkeys = userinput.Markers.Keys.ToArray();
                for (int i = 0; i < userinput.Paths.Count; i++)
                {
                    MapRequest pathreq = cloneWoFeatures(userinput);
                    var key = pathkeys[i];
                    pathreq.Paths.Add(key, userinput.Paths[key]);
                    if (pathreq.ToString().Length > MAX_GOOGLE_LENGTH)
                        throw new ArgumentOutOfRangeException("Path too long " + pathreq.ToString());

                    ThreadPool.QueueUserWorkItem(delegate(object state)
                    {
                        List<Pixel> chgs = null;
                        try
                        {
                            MapRequest casted = (MapRequest)state;
                            using (var pathBMP = getMapFromGoogle(casted))
                            using (var indexedbmp = new Bitmap(pathBMP))
                            using (var bmp = indexedbmp.Clone(new Rectangle(0, 0, indexedbmp.Width, indexedbmp.Height), System.Drawing.Imaging.PixelFormat.Format24bppRgb))
                            {
                                var block = (new LockBitmap(bmp)).TwoD;
                                chgs = findDiff(justmapBlock, block);
                            }
                        }
                        finally
                        {
                            completed.Add(chgs);
                        }
                    }, pathreq);
                }
                int offset = userinput.Paths.Count;
                for (int i = 0; i < userinput.Markers.Count; i++)
                {
                    MapRequest markerreq = cloneWoFeatures(userinput);
                    var key = markerkeys[i];
                    markerreq.Markers.Add(key, userinput.Markers[key]);
                    if (markerreq.ToString().Length > MAX_GOOGLE_LENGTH)
                        throw new ArgumentOutOfRangeException("Markers too long " + markerreq.ToString());

                    ThreadPool.QueueUserWorkItem(delegate(object state)
                    {
                        List<Pixel> chgs = null;
                        try
                        {
                            MapRequest casted = (MapRequest)state;
                            using (var pathBMP = getMapFromGoogle(casted))
                            using (var indexedbmp = new Bitmap(pathBMP))
                            using (var bmp = indexedbmp.Clone(new Rectangle(0, 0, indexedbmp.Width, indexedbmp.Height), System.Drawing.Imaging.PixelFormat.Format24bppRgb))
                            {
                                var block = (new LockBitmap(bmp)).TwoD;
                                chgs = findDiff(justmapBlock, block);
                            }
                        }
                        finally
                        {
                            completed.Add(chgs);
                        }
                    }, markerreq);
                }

                while (completed.Count != len) Thread.Sleep(100);

                var lockedBMP = new LockBitmap(justmapBMP);
                lockedBMP.LockBits();
                foreach (var set in completed)
                {
                    foreach (var item in set)
                        lockedBMP.SetPixel(item.X, item.Y, item.Color);
                }
                lockedBMP.UnlockBits();

                justmapRaw.Dispose();
                //justmapBMP.Dispose();

                //
                MemoryStream ms = new MemoryStream();
                justmapBMP.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;
                return ms;
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

        static public MapRequest cloneWoFeatures(MapRequest userinput)
        {
            MapRequest clone = new MapRequest() { 
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

        public class LockBitmap
        {
            Bitmap source = null;
            IntPtr Iptr = IntPtr.Zero;
            System.Drawing.Imaging.BitmapData bitmapData = null;

            public byte[] Pixels { get; set; }
            public int Depth { get; private set; }
            public int Width { get; private set; }
            public int Height { get; private set; }
            public bool IsIndexed { get; private set; }

            public LockBitmap(Bitmap source)
            {
                this.source = source;
            }

            public Color[,] TwoD
            {
                get
                {
                    var bmp = this.source;
                    int w = bmp.Width;
                    int h = bmp.Height;
                    Color[,] pixels = new Color[w, h];
                    //LockBitmap lockBitmap = new LockBitmap(this.source);
                    this.LockBits();

                    for (int x = 0; x < w; x++)
                        for (int y = 0; y < h; y++)
                            pixels[x, y] = this.GetPixel(x, y);
                    
                    this.UnlockBits();

                    return pixels;
                }
            }

            /// <summary>
            /// Lock bitmap data
            /// </summary>
            public void LockBits()
            {
                try
                {
                    // Get width and height of bitmap
                    Width = source.Width;
                    Height = source.Height;

                    // get total locked pixels count
                    int PixelCount = Width * Height;

                    // Create rectangle to lock
                    Rectangle rect = new Rectangle(0, 0, Width, Height);

                    System.Drawing.Imaging.PixelFormat[] indexed = new System.Drawing.Imaging.PixelFormat[] {
                        System.Drawing.Imaging.PixelFormat.Format1bppIndexed,
                        System.Drawing.Imaging.PixelFormat.Format4bppIndexed,
                        System.Drawing.Imaging.PixelFormat.Format8bppIndexed,
                        System.Drawing.Imaging.PixelFormat.Indexed,
                    };
                    if (Array.IndexOf(indexed, source.PixelFormat) >= 0)
                    {
                        this.IsIndexed = true;
                    }

                    // get source bitmap pixel format size
                    Depth = System.Drawing.Bitmap.GetPixelFormatSize(source.PixelFormat);

                    // Check if bpp (Bits Per Pixel) is 8, 24, or 32
                    if (Depth != 8 && Depth != 24 && Depth != 32)
                    {
                        throw new ArgumentException("Only 8, 24 and 32 bpp images are supported.");
                    }

                    // Lock bitmap and return bitmap data
                    bitmapData = source.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite,
                                                 source.PixelFormat);

                    // create byte array to copy pixel values
                    int step = Depth / 8;
                    Pixels = new byte[PixelCount * step];
                    Iptr = bitmapData.Scan0;

                    // Copy data from pointer to array
                    System.Runtime.InteropServices.Marshal.Copy(Iptr, Pixels, 0, Pixels.Length);
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            /// <summary>
            /// Unlock bitmap data
            /// </summary>
            public void UnlockBits()
            {
                try
                {
                    // Copy data from byte array to pointer
                    System.Runtime.InteropServices.Marshal.Copy(Pixels, 0, Iptr, Pixels.Length);

                    // Unlock bitmap data
                    source.UnlockBits(bitmapData);
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            /// <summary>
            /// Get the color of the specified pixel
            /// </summary>
            /// <param name="x"></param>
            /// <param name="y"></param>
            /// <returns></returns>
            public Color GetPixel(int x, int y)
            {
                if (this.IsIndexed)
                {
                    return this.GetIndexedPixel(x, y);
                }


                Color clr = Color.Empty;

                // Get color components count
                int cCount = Depth / 8;

                // Get start index of the specified pixel
                int i = ((y * Width) + x) * cCount;

                if (i > Pixels.Length - cCount)
                    throw new IndexOutOfRangeException();

                if (Depth == 32) // For 32 bpp get Red, Green, Blue and Alpha
                {
                    byte b = Pixels[i];
                    byte g = Pixels[i + 1];
                    byte r = Pixels[i + 2];
                    byte a = Pixels[i + 3]; // a
                    clr = Color.FromArgb(a, r, g, b);
                }
                if (Depth == 24) // For 24 bpp get Red, Green and Blue
                {
                    byte b = Pixels[i];
                    byte g = Pixels[i + 1];
                    byte r = Pixels[i + 2];
                    clr = Color.FromArgb(r, g, b);
                }
                if (Depth == 8)
                // For 8 bpp get color value (Red, Green and Blue values are the same)
                {
                    byte c = Pixels[i];
                    clr = Color.FromArgb(c, c, c);
                }
                return clr;
            }

            /// <summary>
            /// Set the color of the specified pixel
            /// </summary>
            /// <param name="x"></param>
            /// <param name="y"></param>
            /// <param name="color"></param>
            public void SetPixel(int x, int y, Color color)
            {
                if (this.IsIndexed)
                {
                    this.SetIndexedPixel(x, y, color);
                    return;
                }

                // Get color components count
                int cCount = Depth / 8;

                // Get start index of the specified pixel
                int i = ((y * Width) + x) * cCount;

                if (Depth == 32) // For 32 bpp set Red, Green, Blue and Alpha
                {
                    Pixels[i] = color.B;
                    Pixels[i + 1] = color.G;
                    Pixels[i + 2] = color.R;
                    Pixels[i + 3] = color.A;
                }
                if (Depth == 24) // For 24 bpp set Red, Green and Blue
                {
                    Pixels[i] = color.B;
                    Pixels[i + 1] = color.G;
                    Pixels[i + 2] = color.R;
                }
                if (Depth == 8)
                // For 8 bpp set color value (Red, Green and Blue values are the same)
                {
                    Pixels[i] = color.B;
                }
            }

            /// <summary>
            /// Get the color of the specified pixel
            /// </summary>
            /// <param name="x"></param>
            /// <param name="y"></param>
            /// <returns></returns>
            public Color GetIndexedPixel(int x, int y)
            {
                var lookup = this.source.Palette.Entries;

                Color clr = Color.Empty;

                // Get color components count
                int cCount = Depth / 8;

                // Get start index of the specified pixel
                int i = ((y * Width) + x) * cCount;

                if (i > Pixels.Length - cCount)
                    throw new IndexOutOfRangeException();

                if (Depth == 32) // For 32 bpp get Red, Green, Blue and Alpha
                {
                    int index = BitConverter.ToInt32(Pixels,i);
                    return lookup[index];
                }
                if (Depth == 24) // For 24 bpp get Red, Green and Blue
                {
                    int index1 = Pixels[i];
                    int index2 = Pixels[i+1];
                    int index3 = Pixels[i+2];
                    return lookup[index1<<16 + index2<<8 + index3];
                }
                if (Depth == 8)
                // For 8 bpp get color value (Red, Green and Blue values are the same)
                {
                    int index = Pixels[i];
                    return lookup[index];
                }
                return clr;
            }

            Dictionary<Color, int> reverse = null;

            /// <summary>
            /// Set the color of the specified pixel
            /// </summary>
            /// <param name="x"></param>
            /// <param name="y"></param>
            /// <param name="color"></param>
            public void SetIndexedPixel(int x, int y, Color color)
            {
                if (reverse == null)
                {
                    var lookup = this.source.Palette.Entries;
                    var len =lookup.Length;
                    reverse = new Dictionary<Color, int>();
                    for(int j=0; j<len; j++)
                        reverse.Add(lookup[j], j);
                }

                // Get color components count
                int cCount = Depth / 8;

                // Get start index of the specified pixel
                int i = ((y * Width) + x) * cCount;

                int bits = reverse.Count <= 256 ? 1 : reverse.Count <= 65536 ? 2 : reverse.Count <= 16777216 ? 3 : reverse.Count <= int.MaxValue ? 4 : -1;
                if (bits == -1)
                    throw new Exception("too many entries in palette");
                byte[] data = BitConverter.GetBytes(reverse[color]);

                if (Depth == 32) // For 32 bpp set Red, Green, Blue and Alpha
                {
                    Pixels[i] = data[0];
                    Pixels[i + 1] = data[1];
                    Pixels[i + 2] = data[2];
                    Pixels[i + 3] = data[3];
                }
                if (Depth == 24) // For 24 bpp set Red, Green and Blue
                {
                    Pixels[i] = data[0];
                    Pixels[i + 1] = data[1];
                    Pixels[i + 2] = data[2];
                }
                if (Depth == 8)
                // For 8 bpp set color value (Red, Green and Blue values are the same)
                {
                    Pixels[i] = data[0];
                }
            }

            public void Apply(List<Pixel> set)
            {
                foreach (var item in set)
                    this.SetPixel(item.X, item.Y, item.Color);
            }
        }

        public class Pixel 
        {
            public int X;
            public int Y;
            public Color Color;
        }
        static public List<Pixel> findDiff(Color[,] basis, Color[,] altered)
        {
            int w = basis.GetLength(0);
            int h = basis.GetLength(1);
            List<Pixel> diff = new List<Pixel>();
            for(int x=0; x<w;x++)
                for (int y = 0; y < h; y++)
                {
                    Color color = altered[x,y];
                    Color original = basis[x, y];
                    //if (Math.Abs(EigenVector(color) - EigenVector(original)) >  40)
                    if (DiffGreaterThan(color, original, 0.05))
                        diff.Add(new Pixel() { X=x, Y=y, Color=color });
                }
            // Console.WriteLine("findDiff smell check {0} {1} {2} {3}", w, h, w*h, diff.Count);
            return diff;
        }
        static bool DiffGreaterThan(Color pixel1, Color pixel2, double pct)
        {
            var a = Math.Abs((double)pixel1.A - (double)pixel2.A)/255;
            var r = Math.Abs((double)pixel1.R - (double)pixel2.R) / 255;
            var g = Math.Abs((double)pixel1.G - (double)pixel2.G) / 255;
            var b = Math.Abs((double)pixel1.B - (double)pixel2.B) / 255;
            var grey = (a > pct) || (r > pct) || (g > pct) || (b > pct);

            return grey;
        }


        static private Bitmap createChart(MapRequest userrequest, List<ChartSeries> data)
        {
            int GOOGLE_MAP_DIM = (int)userrequest.Scale*userrequest.Size;
            int CHART_TOP_MARGIN = 100;
            int CHART_LEFT_MARGIN = 100;
            Size size = new Size(GOOGLE_MAP_DIM + CHART_LEFT_MARGIN, GOOGLE_MAP_DIM + CHART_TOP_MARGIN);

            

            RectangleF corners = new RectangleF();

            Bitmap bmp = createChart(size, corners, data);
            return bmp;
        }
        class ChartPoint
        {
            public double X;
            public double Y;
            public string Label;
        }
        class ChartSeries : List<ChartPoint>
        {
            public string Name;
        }
        static private Bitmap createChart(Size size, RectangleF minmax, List<ChartSeries> data)
        {
            // create the chart
            var chart = new Chart();
            chart.Size = size;

            var chartArea = new ChartArea();
            //chartArea.AxisX.LabelStyle.Format = "dd/MMM\nhh:mm"; //default format for datatype
            chartArea.AxisX.MajorGrid.LineColor = Color.LightGray;
            chartArea.AxisY.MajorGrid.LineColor = Color.LightGray;
            chartArea.AxisX.LabelStyle.Font = new Font("Consolas", 8);
            chartArea.AxisY.LabelStyle.Font = new Font("Consolas", 8);
            chartArea.AxisX.Minimum = minmax.Left;
            chartArea.AxisY.Minimum = minmax.Top;
            chartArea.AxisX.Maximum = minmax.Right;
            chartArea.AxisY.Maximum = minmax.Bottom;
            chartArea.AxisX.Enabled = AxisEnabled.True; // 
            chartArea.AxisY.Enabled = AxisEnabled.True;
            chartArea.AxisX.LabelStyle.Enabled = false;
            chartArea.AxisY.LabelStyle.Enabled = false;
            chart.ChartAreas.Add(chartArea);

            const string INDEX_LEGEND = "Legend";
            if (data.Count >= 1)
            {
                // Create a new legend called "Legend2".
                chart.Legends.Add(new Legend(INDEX_LEGEND));

                // Set Docking of the Legend chart to the Default Chart Area.
                chart.Legends[INDEX_LEGEND].DockedToChartArea = string.Empty;

            }

            int index = 0;
            foreach (var dataseries in data)
            {
                var series = new Series() { ChartType=SeriesChartType.Point }; //only supports the point chart type
                series.Name = dataseries.Name;
                series.Legend = INDEX_LEGEND;
                series.IsVisibleInLegend = true;

                series.XValueType = ChartValueType.Double; //only supports double data
                series.YValueType = ChartValueType.Double; //only supports double data 

                chart.Series.Add(series);

                // bind the datapoints
                var dataX = dataseries.Select<ChartPoint, double>(s => s.X);
                var dataY = dataseries.Select<ChartPoint, double>(s => s.Y);
                chart.Series[dataseries.Name].Points.DataBindXY(dataX, dataY);
                //add labels
                int count = dataseries.Count;
                for (int i = 0; i < count; i++)
                    series.Points[i].Label = dataseries[i].Label;
                
                //no axis titles

                index++;
            }



            // draw!
            chart.Invalidate();

            // create bitmap
            Bitmap bmp = null;
            using (var ms = new MemoryStream())
            {
                var pos = chart.ChartAreas[0].InnerPlotPosition;
                string name = string.Format("{0}.{1}.{2}.{3}.png", pos.X, pos.Y, pos.Right, pos.Bottom);
                chart.SaveImage(name, ChartImageFormat.Png);

                chart.SaveImage(ms, ChartImageFormat.Png);
                chart.Dispose();
                ms.Position = 0;
                bmp = new Bitmap(ms);
            }
            return bmp;
        }
        static public List<Pixel> separateForeground(LockBitmap imgbytes, Color bgcolor, Rectangle crop)
        {
            List<Pixel> diff = new List<Pixel>();
            for (int x = crop.Left; x <= crop.Right; x++)
                for (int y = crop.Top; y <= crop.Bottom; y++)
                {
                    Color color = imgbytes.GetPixel(x,y);
                    //if (Math.Abs(EigenVector(color) - EigenVector(original)) >  40)
                    if (DiffGreaterThan(color, bgcolor, 0.01))
                        diff.Add(new Pixel() { X = x, Y = y, Color = color });
                }
            // Console.WriteLine("findDiff smell check {0} {1} {2} {3}", w, h, w*h, diff.Count);
            return diff;
        }
    }

    static public class GoogleMapCoordinate
    {
        //(half of the earth circumference's in pixels at zoom level 21)
        static double offset = 268435456;
        static double radius = offset / Math.PI;
        static double CforX = radius * Math.PI / 180d;
        static double CforY = 0.035d * radius /2;
        // X,Y ... location in degrees
        // xcenter,ycenter ... center of the map in degrees (same value as in 
        // the google static maps URL)
        // zoomlevel (same value as in the google static maps URL)
        // xr, yr and the returned Point ... position of X,Y in pixels relativ 
        // to the center of the bitmap
        static Point Adjust(double X, double Y, double xcenter, double ycenter, int zoomlevel)
        {
            int xr = (LToX(X) - LToX(xcenter)) >> (21 - zoomlevel);
            int yr = (LToY(Y) - LToY(ycenter)) >> (21 - zoomlevel);
            Point p = new Point(xr, yr);
            return p;
        }
        //(c+Ax)-(C+Ay) = A(x-y), (radius * Math.PI / 180)(x-y)
        static int LToX(double x)
        {
            return (int)(Math.Round(offset + radius * x * Math.PI / 180));
        }

        //(offset - (radius/2) * Math.Log((1 + Math.Sin(y * Math.PI / 180)) / (1 - Math.Sin(y * Math.PI / 180))) )     -(C+Ay) 
        static int LToY(double y)
        {
            return (int)(Math.Round(offset - radius * Math.Log((1 +
                         Math.Sin(y * Math.PI / 180)) / (1 - Math.Sin(y *
                         Math.PI / 180))) / 2));
        }

        static int OffsetLonToOffsetX(double offsetL, int zoomlevel)
        {
            // y = 0.035x + 3E-05,R² = 1
            return Convert.ToInt32(CforX * offsetL) >> (21 - zoomlevel);
        }
        static int OffsetLatToOffsetY(double offsetL, int zoomlevel)
        {
            //(c+Ax)-(C+Ay) = A(x-y), (radius * Math.PI / 180)(x-y)

            return Convert.ToInt32(CforY * offsetL) >> (21 - zoomlevel);
        }

        static public Bitmap DrawMark(this Bitmap bmp, double centerLat, double centerLon, double lat, double lon, GoogleMap.MapRequest.ZoomOptions zoomlevel, double degr)
        {
            int zoom = 1 + (int)zoomlevel;
            var pt = Adjust(lon, lat, centerLon, centerLat, zoom);
            var pt2 = Adjust(lon, lat+1, centerLon, centerLat, zoom);
            int r = Convert.ToInt32(degr * (pt2.Y - pt.Y));
            const float CENTER_X = 641.5f;
            const float CENTER_Y = 641.5f;

            var tempBitmap = bmp.DrawMark(pt.X + CENTER_X, pt.Y + CENTER_Y, r);

            // Use tempBitmap as you would have used originalBmp
            return tempBitmap;
        }
        static public Bitmap DrawMark(this Bitmap bmp, float x, float y, float r)
        {
            // The original bitmap with the wrong pixel format. 
            // You can check the pixel format with originalBmp.PixelFormat

            // Create a blank bitmap with the same dimensions
            Bitmap tempBitmap = new Bitmap(bmp.Width, bmp.Height);

            // From this bitmap, the graphics can be obtained, because it has the right PixelFormat
            using (Graphics g = Graphics.FromImage(tempBitmap))
            {
                // Draw the original bitmap onto the graphics of the new bitmap
                g.DrawImage(bmp, 0, 0);
                //g.DrawRectangle(Pens.Black, x, y, w, h);
                r = 100;
                g.DrawLine(Pens.Black, x-r, y-r, x+r, y+r);
                g.DrawLine(Pens.Black, x+r, y-r, x-r, y+r);

                Console.WriteLine("----------------");
                Console.WriteLine("{0:g}",x);
                Console.WriteLine("{0:g}", y);
                Console.WriteLine("{0:g}", r);
                Console.WriteLine("----------------");

                //g.DrawRectangle(Pens.Black, 0, 0, 200, 200);

                g.Flush();
            }

            // Use tempBitmap as you would have used originalBmp
            return tempBitmap;
        }

        static PointF Reverse(Point offsetcenter, double centerLon, double centerLat, int zoomlevel)
        {
            int X = offsetcenter.X;
            int Y = offsetcenter.Y;
            double xr = XToL(X << (21 - zoomlevel)) + centerLon;
            double yr = XToL(Y << (21 - zoomlevel)) + centerLat;

            PointF p = new PointF(Convert.ToSingle(xr), Convert.ToSingle(yr));
            return p;
        }

        static double XToL(int x)
        {
            var lon = ((x-offset)*180d)/(radius * Math.PI);
            return lon;
        }

        static double YToL(int y)
        {
            return (int)(Math.Round(offset - radius * Math.Log((1 +
                         Math.Sin(y * Math.PI / 180)) / (1 - Math.Sin(y *
                         Math.PI / 180))) / 2));
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
