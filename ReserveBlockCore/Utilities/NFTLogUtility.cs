using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ReserveBlockCore.Utilities
{
    public class NFTLogUtility
    {
        public static async void Log(string message, string location, bool firstEntry = false)
        {
            try
            {
                var databaseLocation = Program.IsTestNet != true ? "Databases" : "DatabasesTestNet";
                var text = "[" + DateTime.Now.ToString() + "]" + " : " + "[" + location + "]" + " : " + message;
                string path = "";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    path = homeDirectory + Path.DirectorySeparatorChar + "rbx" + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                }
                else
                {
                    if (Debugger.IsAttached)
                    {
                        path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                    }
                    else
                    {
                        path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar + "RBX" + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                    }
                }
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                if (firstEntry == true)
                {
                    await File.AppendAllTextAsync(path + "nftlog.txt", Environment.NewLine + " ");


                }

                await File.AppendAllTextAsync(path + "nftlog.txt", Environment.NewLine + text);
            }
            catch (Exception ex)
            {

            }
        }

        public static async Task ClearLog()
        {
            var databaseLocation = Program.IsTestNet != true ? "Databases" : "DatabasesTestNet";
            string path = "";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                path = homeDirectory + Path.DirectorySeparatorChar + "rbx" + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
            }
            else
            {
                if (Debugger.IsAttached)
                {
                    path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                }
                else
                {
                    path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar + "RBX" + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                }
            }
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            await File.WriteAllTextAsync(path + "nftlog.txt", "");
        }

        public static async Task<string> ReadLog()
        {
            var databaseLocation = Program.IsTestNet != true ? "Databases" : "DatabasesTestNet";
            string path = "";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                path = homeDirectory + Path.DirectorySeparatorChar + "rbx" + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
            }
            else
            {
                if (Debugger.IsAttached)
                {
                    path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                }
                else
                {
                    path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar + "RBX" + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                }
            }
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var result = await File.ReadAllLinesAsync(path + "nftlog.txt");

            StringBuilder strBld = new StringBuilder();

            foreach (var line in result)
            {
                strBld.AppendLine(line);
            }

            return strBld.ToString();
        }
    }
}
