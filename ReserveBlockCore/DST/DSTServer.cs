using System.Net.Sockets;
using System.Net;
using System.Text;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Models.DST;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using ReserveBlockCore.Utilities;
using static ReserveBlockCore.DST.DSTClient;

namespace ReserveBlockCore.DST
{
    public class DSTServer
    {
        public static UdpClient udpClient;
        private static ConcurrentDictionary<int, UdpClient> UdpClientDict = new ConcurrentDictionary<int, UdpClient>();

        public static async Task Run()
        {
            var successMessage = Encoding.UTF8.GetBytes("successful");
            udpClient = new UdpClient(Globals.SelfSTUNPort);

            Console.WriteLine($"Started listening on port {Globals.SelfSTUNPort}...");

            var peersData = new Dictionary<string, IList<IPEndPoint>>();

            while (true)
            {
                ReadData();
            }
        }
        static void InformClient(IPEndPoint destinationEndPoint, IPEndPoint sourceEndPoint)
        {
            var dataString = $"{sourceEndPoint.Address} {sourceEndPoint.Port}";
            var dataBytes = Encoding.UTF8.GetBytes(dataString);
            Console.WriteLine($"sending \"{dataString}\"\n  to {destinationEndPoint}...");
            udpClient.Send(dataBytes, destinationEndPoint);
        }

        static void ReadData()
        {
            try
            {
                var endPoint = new IPEndPoint(IPAddress.Any, 0);
                //this may need to become async
                var receivedData = udpClient.Receive(ref endPoint);
                var payload = Encoding.UTF8.GetString(receivedData);

                if (!string.IsNullOrEmpty(payload))
                {
                    var message = JsonConvert.DeserializeObject<Message>(payload);

                    if (message != null)
                    {
                        MessageService.ProcessMessage(message, endPoint, udpClient);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.ToString()}");
            }
            
        }

        private static string GenerateMessage(Message message)
        {
            var output = "";
            message.Build();
            output = JsonConvert.SerializeObject(message);

            return output;
        }
    }
}
