using System.Runtime.InteropServices;

namespace ReserveBlockCore.Utilities
{
    public class PlatformUtility
    {
        public static string GetPlatform()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "win";
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "mac";
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "linux";
            }

            return "";
        }
    }
}
