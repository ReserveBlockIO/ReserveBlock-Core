using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ReserveBlockCore.Utilities
{
    public class SCLogUtility
    {
        public static async Task<string> LogAndReturn(string message, string location, bool success)
        {
            try
            {
                bool writeLog = true;
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

                if (File.Exists(path + "sclog.txt"))
                {
                    var bytes = File.ReadAllBytes(path + "sclog.txt").Length;
                    var totalMB = bytes / 1024 / 1024;
                    if (totalMB > 100)
                        writeLog = false;
                    else
                        writeLog = true;
                }

                if (writeLog)
                    await File.AppendAllTextAsync(path + "sclog.txt", Environment.NewLine + text);
            }
            catch (Exception ex)
            {
            }

            if(success)
            {
                VFXLogging.LogInfo(message, location);
            }
            else
            {
                VFXLogging.LogError(message, location, true);
            }
            return JsonConvert.SerializeObject(new { Success = success, Message = message });
        }
        public static async void Log(string message, string location, bool firstEntry = false)
        {
            try
            {
                bool writeLog = true;
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
                if (firstEntry == true)
                {
                    await File.AppendAllTextAsync(path + "sclog.txt", Environment.NewLine + " ");
                }

                if (File.Exists(path + "sclog.txt"))
                {
                    var bytes = File.ReadAllBytes(path + "sclog.txt").Length;
                    var totalMB = bytes / 1024 / 1024;
                    if (totalMB > 100)
                        writeLog = false;
                    else
                        writeLog = true;
                }

                if(writeLog)
                    await File.AppendAllTextAsync(path + "sclog.txt", Environment.NewLine + text);
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
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            await File.WriteAllTextAsync(path + "sclog.txt", "");
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
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var result = await File.ReadAllLinesAsync(path + "sclog.txt");

            StringBuilder strBld = new StringBuilder();

            foreach (var line in result)
            {
                strBld.AppendLine(line);
            }

            return strBld.ToString();
        }
    }
}
