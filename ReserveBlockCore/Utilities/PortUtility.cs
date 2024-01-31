using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

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
        public static bool IsUdpPortInUseOSAgnostic(int port)
        {
            try
            {
                using (var client = new UdpClient(port))
                {
                    return false;
                }
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
                {
                    return true;
                }
                else
                {
                    throw;
                }
            }
        }

        public static bool IsPortOpen(string host, int port)
        {
            using (TcpClient tcpClient = new TcpClient())
            {
                try
                {
                    var result = tcpClient.BeginConnect(host, port, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));
                    if (!success)
                    {
                        throw new SocketException();
                    }
                    return true;
                }
                catch (SocketException)
                {
                    return false;
                }
            }
        }

        public static int FindOpenUDPPort(int port)
        {
            var portFound = false;
            var count = 1;
            if (port > 50000)
                port = Globals.DSTClientPort + 1; // reset port back down

            if (port < 13341)
                port = Globals.DSTClientPort;

            while (!portFound) 
            {
                var nextPort = port + count;
                //testing this without OS agnostic method for now.
                var portInUse = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? IsUdpPortInUse(nextPort) : IsUdpPortInUseOSAgnostic(nextPort);
                if (!portInUse)
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
