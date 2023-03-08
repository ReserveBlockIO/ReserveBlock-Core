using System.Net.Sockets;
using System.Net;
using System.Text;
using ReserveBlockCore.P2P;

namespace ReserveBlockCore.DST
{
    public class DSTServer
    {
        static int Port = Globals.IsTestNet ? Globals.DSTPort + 10000 : Globals.DSTPort;
        static UdpClient udpClient;

        public static async Task Run()
        {
            var successMessage = Encoding.UTF8.GetBytes("successful");
            udpClient = new UdpClient(Port);

            Console.WriteLine($"Started listening on port {Port}...");

            var peersData = new Dictionary<string, IList<IPEndPoint>>();

            while (true)
            {
                var (endPoint, group) = ReadData();
                Console.WriteLine($"received {group} from {endPoint}");

                if (!peersData.ContainsKey(group))
                {
                    peersData[group] = new List<IPEndPoint>();
                }
                peersData[group].Add(endPoint);
                udpClient.Send(successMessage, endPoint);

                if (peersData[group].Count == 2)
                {
                    InformClient(peersData[group][0], peersData[group][1]);
                    InformClient(peersData[group][1], peersData[group][0]);
                    peersData.Remove(group);
                    Console.WriteLine($"removed group {group}");
                }
            }
        }
        static void InformClient(IPEndPoint destinationEndPoint, IPEndPoint sourceEndPoint)
        {
            var dataString = $"{sourceEndPoint.Address} {sourceEndPoint.Port}";
            var dataBytes = Encoding.UTF8.GetBytes(dataString);
            Console.WriteLine($"sending \"{dataString}\"\n  to {destinationEndPoint}...");
            udpClient.Send(dataBytes, destinationEndPoint);
        }

        static (IPEndPoint, string) ReadData()
        {
            var endPoint = new IPEndPoint(IPAddress.Any, 0);
            var receivedData = udpClient.Receive(ref endPoint);
            var group = Encoding.UTF8.GetString(receivedData);

            return (endPoint, group);
        }
    }
}
