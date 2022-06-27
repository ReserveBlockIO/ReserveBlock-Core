using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ReserveBlockCore.Utilities
{
    public class NFTAssetFileUtility 
    {
        public static bool MoveAsset(string fileLocation, string fileName, string scUID)
        {
            var assetLocation = Program.IsTestNet != true ? "Assets" : "AssetsTestNet";

            scUID = scUID.Replace(":", ""); //remove the ':' because some folder structures won't allow it.

            string path = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                path = homeDirectory + Path.DirectorySeparatorChar + "rbx" + Path.DirectorySeparatorChar + assetLocation + Path.DirectorySeparatorChar + scUID + Path.DirectorySeparatorChar;
            }
            else
            {
                if (Debugger.IsAttached)
                {
                    path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + assetLocation + Path.DirectorySeparatorChar + scUID + Path.DirectorySeparatorChar;
                }
                else
                {
                    path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar + "RBX" + Path.DirectorySeparatorChar + assetLocation + Path.DirectorySeparatorChar + scUID + Path.DirectorySeparatorChar;
                }
            }
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var newPath = path + fileName;

            try
            {
                File.Copy(fileLocation, newPath);
                return true;
            }
            catch(Exception ex)
            {
                ErrorLogUtility.LogError(ex.Message, "NFTAssetFileUtility.MoveAsset(string fileLocation, string fileName)");
                NFTLogUtility.Log("Error Saving NFT File.", "NFTAssetFileUtility.MoveAsset(string fileLocation, string fileName)");
                return false;
            }
        }


    }
}
