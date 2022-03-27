using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ReserveBlockCore.Utilities
{
    public class DebugUtility
    {
        public static async void WriteToDebugFile()
        {
            try
            {
                var databaseLocation = Program.IsTestNet != true ? "Databases" : "DatabasesTestNet";
                var text = await StaticVariableUtility.GetStaticVars();
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

                await File.WriteAllTextAsync(path + "debug.txt", text);
            }
            catch(Exception ex)
            {

            }
        }
    }
}
