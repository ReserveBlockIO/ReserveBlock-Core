using System.Net.NetworkInformation;

namespace ReserveBlockCore.Utilities
{
    public class PortUtility
    {
        public static bool IsUdpPortInUse(int port)
        {
            var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            var udpListeners = ipGlobalProperties.GetActiveUdpListeners();
            return udpListeners.Any(l => l.Port == port);
        }

        public static int FindOpenUDPPort(int port)
        {
            var portFound = false;
            var count = 1;
            if (port > 50000)
                port = Globals.DSTClientPort + 1; // reset port back down

            while (!portFound) 
            {
                var nextPort = port + count;
                var portInUse = IsUdpPortInUse(nextPort);
                if(!portInUse)
                {
                    portFound = true;
                    return nextPort;
                }
                else
                {
                    count += 1;
                }
            }

            return -1;
        }
    }
}
