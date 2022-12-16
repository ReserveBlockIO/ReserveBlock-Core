using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ReserveBlockCore.Utilities
{
    public class LogUtility
    {
        private static ConcurrentDictionary<string, SemaphoreSlim> FileLock = new ConcurrentDictionary<string, SemaphoreSlim>();
        public static async Task LogAsync(string message, string location)
        {
            var slim = FileLock.GetOrAdd(location, new SemaphoreSlim(1, 1));
            try
            {
                await slim.WaitAsync();
                var databaseLocation = Globals.IsTestNet != true ? "Databases" : "DatabasesTestNet";
                var mainFolderPath = Globals.IsTestNet != true ? "RBX" : "RBXTest";

                var text = "[" + DateTime.Now.ToString() + "]" + " : " + "[" + location + "]" + " : " + message;
                string path = "";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    path = homeDirectory + Path.DirectorySeparatorChar + mainFolderPath.ToLower() + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                }
                else
                {
                    if (Debugger.IsAttached)
                    {
                        path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "DBs" + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                    }
                    else
                    {
                        path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar + mainFolderPath + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                    }
                }

                if (!string.IsNullOrEmpty(Globals.CustomPath))
                {
                    path = Globals.CustomPath + mainFolderPath + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                }

                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                await File.AppendAllTextAsync(path + "rbxlogasync.txt", Environment.NewLine + text);
            }
            catch { }
            finally
            {
                if (slim.CurrentCount == 0)
                    slim.Release();
            }
        }

        public static async void Log(string message, string location, bool firstEntry = false)
        {
            try
            {
                var databaseLocation = Globals.IsTestNet != true ? "Databases" : "DatabasesTestNet";
                var mainFolderPath = Globals.IsTestNet != true ? "RBX" : "RBXTest";

                var text = "[" + DateTime.Now.ToString() +  "]" + " : " + "[" + location + "]" + " : " + message;
                string path = "";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    path = homeDirectory + Path.DirectorySeparatorChar + mainFolderPath.ToLower() + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                }
                else
                {
                    if (Debugger.IsAttached)
                    {
                        path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "DBs" + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                    }
                    else
                    {
                        path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar + mainFolderPath + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                    }
                }

                if (!string.IsNullOrEmpty(Globals.CustomPath))
                {
                    path = Globals.CustomPath + mainFolderPath + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                }

                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                if (firstEntry == true)
                {
                    await File.AppendAllTextAsync(path + "rbxlog.txt", Environment.NewLine + " ");


                }

                await File.AppendAllTextAsync(path + "rbxlog.txt", Environment.NewLine + text);
            }
            catch (Exception ex)
            {

            }
        }

        public static async Task ClearLog()
        {
            var databaseLocation = Globals.IsTestNet != true ? "Databases" : "DatabasesTestNet";
            var mainFolderPath = Globals.IsTestNet != true ? "RBX" : "RBXTest";

            string path = "";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                path = homeDirectory + Path.DirectorySeparatorChar + mainFolderPath.ToLower() + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
            }
            else
            {
                if (Debugger.IsAttached)
                {
                    path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "DBs" + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                }
                else
                {
                    path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar + mainFolderPath + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                }
            }

            if (!string.IsNullOrEmpty(Globals.CustomPath))
            {
                path = Globals.CustomPath + mainFolderPath + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
            }

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            await File.WriteAllTextAsync(path + "rbxlog.txt", "");
        }

        public static async Task<string> ReadLog()
        {
            var databaseLocation = Globals.IsTestNet != true ? "Databases" : "DatabasesTestNet";
            var mainFolderPath = Globals.IsTestNet != true ? "RBX" : "RBXTest";

            string path = "";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                path = homeDirectory + Path.DirectorySeparatorChar + mainFolderPath.ToLower() + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
            }
            else
            {
                if (Debugger.IsAttached)
                {
                    path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "DBs" + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                }
                else
                {
                    path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar + mainFolderPath + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                }
            }

            if (!string.IsNullOrEmpty(Globals.CustomPath))
            {
                path = Globals.CustomPath + mainFolderPath + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
            }

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var result = await File.ReadAllLinesAsync(path + "rbxlog.txt");

            StringBuilder strBld = new StringBuilder();

            foreach (var line in result)
            {
                strBld.AppendLine(line);
            }

            return strBld.ToString();
        }
    }
}
