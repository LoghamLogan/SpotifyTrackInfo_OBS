using SpotifyAPI.Web; //Base Namespace
using SpotifyAPI.Web.Auth; //All Authentication-related classes
using SpotifyAPI.Web.Enums; //Enums
using SpotifyAPI.Web.Models; //Models for the JSON-responses
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using System.Net;
using SimpleWebServer;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;
using System.Net.NetworkInformation;

/// <summary>
/// This console application will spin up a webserver and server Spotify information about the current track.
/// 
/// Quickly hacked together something to read my Spotify's now playing in to something OBS can point to.
/// </summary>
namespace SpotifyInfo
{
    class Program
    {
        private static SpotifyWebAPI _spotify;
        private static WebServer _webServer;
        private static WebServer _webServer2;
        private static string _json = "";
        private static string _lastTrackId = "";

        private static void ConfigCheck()
        {
            // Quick/simple checks for valid config.
            if (String.IsNullOrEmpty(ConfigurationManager.AppSettings["spotify_api_key"]))
            {
                throw new Exception("No spotify api key found in App.config (spotify_api_key)");
            }
            if (String.IsNullOrEmpty(ConfigurationManager.AppSettings["webaddr_render"]))
            {
                throw new Exception("webaddr_render not set in App.config");
            }
            if (String.IsNullOrEmpty(ConfigurationManager.AppSettings["webaddr_json"]))
            {
                throw new Exception("webaddr_json not set in App.config");
            }
            int result = -1;
            int.TryParse(ConfigurationManager.AppSettings["spotify_api_port"], out result);
            if (result == -1)
            {
                throw new Exception("Invalid spotify webapi port defined");
            }            
        }

        public static void Main(String[] args)
        {
            Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            string buildDate = "RELEASE";
#if DEBUG
            buildDate = "DEBUG";
#endif
            string displayableVersion = $"{version} ({buildDate})";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"***Logham's Spotify Info Script :: v{displayableVersion}***\n");
            Console.ForegroundColor = ConsoleColor.Green;
            try
            {
                Console.Write($"Checking config... ");
                ConfigCheck();
                Console.Write("done\n");
                Console.Write($"Starting webserver... ");
                RunWebServer();
                Console.Write($"done (navigate to: {_webServer.Address})\n");
                Console.ForegroundColor = ConsoleColor.Yellow;
                int currentCursor = Console.CursorTop;
                Console.WriteLine($"Waiting for authentication with spotify... (CHECK BROWSER)");
                Authenticate();
                Console.SetCursorPosition(0, currentCursor);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Waiting for authentication with spotify... SUCCESS        ");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: {ex.Message}");
                Console.ReadKey();
                return;
            }

            Console.SetCursorPosition(0, 7);
            DrawControls();

            var timerWaitTime = TimeSpan.FromSeconds(6);

            CancellationTokenSource source = new CancellationTokenSource();
            CancellationToken cancelToken = source.Token;
            Task perdiodicTask = PeriodicTaskFactory.Start(() =>
            {
                UpdateTrackInfo();
            },
            intervalInMilliseconds: (int)timerWaitTime.TotalMilliseconds,
            delayInMilliseconds: 0,
            //duration: 10, //max time allowed in seconds
            synchronous: true,
            maxIterations: Timeout.Infinite, 
            cancelToken: cancelToken, 
            periodicTaskCreationOptions: TaskCreationOptions.None);

            bool hasStopped = false;
            while (!hasStopped)
            {
                if (perdiodicTask.IsCompleted || perdiodicTask.IsCanceled)
                {
                    hasStopped = true;
                    Console.SetCursorPosition(0, 9);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("ERROR: Work thread has stopped unexpectedly.");
                    break;
                }
                if ((Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape))
                {
                    Console.SetCursorPosition(0, 9);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Recieved quit signal");
                    break;
                }
            }
            source.Cancel(true);
            while (!perdiodicTask.IsCompleted) {
                Thread.Sleep(100);
            }
            perdiodicTask.Dispose();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Everything has been stopped successfully...");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Press any key to quit");
            Console.ReadKey();
        }

        private static async void Authenticate()
        {
            WebAPIFactory webApiFactory = new WebAPIFactory(
                "http://localhost/",
                int.Parse(ConfigurationManager.AppSettings["spotify_api_port"]), // def. 8000
                ConfigurationManager.AppSettings["spotify_api_key"],
                Scope.UserReadPlaybackState,
                TimeSpan.FromSeconds(20)
            );

            try
            {
                //This will open the user's browser and returns once the user is authorized.
                _spotify = await webApiFactory.GetWebApi();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            if (_spotify == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("AUTH ERROR (1)");
                return;
            }
        }

        public static void UpdateTrackInfo()
        {
            try
            {                
                if (_spotify == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    throw new Exception("AUTH ERROR (2)");                    
                }
                PlaybackContext track = _spotify.GetPlayingTrack();
                if (track == null || track.Item == null)
                {
                    throw new Exception("No track info returned by api");
                }
                // Create a file to write to.
                _json = JsonConvert.SerializeObject(track);

                if (!_lastTrackId.Equals(track.Item.Id))
                {
                    _lastTrackId = track.Item.Id;
                    string artistString = String.Join(", ", track.Item.Artists.Select(x => x.Name).ToArray());
                    string nowPlaying = "Now playing: ";
                    string artist = " by artist: ";
                    Console.SetCursorPosition(0, 5);
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.Write($"{nowPlaying}");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write($"{track.Item.Name}");
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.Write($"{artist}");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write($"{artistString}");
                    Console.Write(new string(' ', Math.Max(0, Console.WindowWidth - nowPlaying.Length - track.Item.Name.Length - artist.Length - artistString.Length)));
                }
                Console.ForegroundColor = ConsoleColor.Magenta;

                Console.SetCursorPosition(0, 6);
                DrawTextProgressBar(track.ProgressMs, track.Item.DurationMs);
            }
            catch (Exception ex)
            {
                Console.SetCursorPosition(0, 8);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("\nEXCEPTION: ");
                Console.Error.Write($"{ex.Message}\n");
            }
        }

        public static void ClearCurrentConsoleLine()
        {
            int currentLineCursor = Console.CursorTop;
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, currentLineCursor);
        }

        private static void DrawControls()
        {
            Console.Write("Press ESC to stop\n");
        }

        private static void DrawTextProgressBar(int progress, int total)
        {
            //draw empty progress bar
            Console.CursorLeft = 0;
            Console.Write("["); //start
            Console.CursorLeft = 32;
            Console.Write("]"); //end
            Console.CursorLeft = 1;
            float onechunk = 30.0f / total;

            //draw filled part
            int position = 1;
            for (int i = 0; i < onechunk * progress; i++)
            {
                Console.BackgroundColor = ConsoleColor.Gray;
                Console.CursorLeft = position++;
                Console.Write(" ");
            }

            //draw unfilled part
            for (int i = position; i <= 31; i++)
            {
                Console.BackgroundColor = ConsoleColor.Green;
                Console.CursorLeft = position++;
                Console.Write(" ");
            }

            //draw totals
            Console.CursorLeft = 35;
            Console.BackgroundColor = ConsoleColor.Black;
            TimeSpan pts = TimeSpan.FromMilliseconds(progress);
            TimeSpan tts = TimeSpan.FromMilliseconds(total);
            string elapsedStr = string.Format("{0:D2}m:{1:D2}s", pts.Minutes, pts.Seconds);
            string totaStrl = string.Format("{0:D2}m:{1:D2}s", tts.Minutes, tts.Seconds);
            Console.Write(elapsedStr + " of " + totaStrl + "    ");
        }

        private static void RunWebServer()
        {
            // Using SimpleWebServer because it's super easy.

            // Current track render (OBS should point here)
            var address = ConfigurationManager.AppSettings["webaddr_render"]; //"http://localhost:8080/music/";
            _webServer = new WebServer(SendResponse, address);
            _webServer.Run();

            // JSON response
            var address2 = ConfigurationManager.AppSettings["webaddr_json"]; //"http://localhost:8080/current/";
            _webServer2 = new WebServer(SendResponse2, address2);
            _webServer2.Run();
        }

        public static string SendResponse(HttpListenerRequest request)
        {
            try
            {
                return File.ReadAllText(Directory.GetCurrentDirectory() + "/pages/MusicInfo.html", System.Text.Encoding.UTF8);
            }
            catch (Exception ex) {
                Console.Error.WriteLine(ex.Message);
                return "Response Error";
            }
        }

        public static string SendResponse2(HttpListenerRequest request)
        {
            return _json;
        }

    }
}
