using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ReserveBlockCore.Utilities
{
    public class BanLogUtility
    {
        public static async void Log(string message, string location)
        {
            try
            {
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
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                await File.AppendAllTextAsync(path + "banlog.txt", Environment.NewLine + text);
            }
            catch (Exception ex)
            {

            }
        }
    }
}
