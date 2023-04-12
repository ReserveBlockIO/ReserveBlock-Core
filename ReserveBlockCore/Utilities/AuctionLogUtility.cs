using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ReserveBlockCore.Utilities
{
    public class AuctionLogUtility
    {
        public static async void Log(string message, string location, bool firstEntry = false)
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
                    await File.AppendAllTextAsync(path + "auctionlog.txt", Environment.NewLine + " ");


                }

                await File.AppendAllTextAsync(path + "auctionlog.txt", Environment.NewLine + text);
            }
            catch { }
        }
        
    }
}

