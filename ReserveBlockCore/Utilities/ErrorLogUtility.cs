using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ReserveBlockCore.Utilities
{
    public class ErrorLogUtility
    {
        public static async void LogError(string message, string location)
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
                
                if (File.Exists(path + "errorlog.txt")) 
                { 
                    var bytes = File.ReadAllBytes(path + "errorlog.txt").Length;
                    var totalMB = bytes / 1024 / 1024;
                    if (totalMB > 100)
                        writeLog = false; 
                    else
                        writeLog = true;
                }

                if(writeLog)
                    await File.AppendAllTextAsync(path + "errorlog.txt", Environment.NewLine + text);
            }
            catch (Exception ex)
            {

            }
        }
    }
}
