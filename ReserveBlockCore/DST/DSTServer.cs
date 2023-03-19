using System.Net.Sockets;
using System.Net;
using System.Text;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Models.DST;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.Collections.Concurrent;

namespace ReserveBlockCore.DST
{
    public class DSTServer
    {
        static UdpClient udpClient;
        private static ConcurrentDictionary<int, UdpClient> UdpClientDict = new ConcurrentDictionary<int, UdpClient>();

        public static async Task Run()
        {
            var successMessage = Encoding.UTF8.GetBytes("successful");
            udpClient = new UdpClient(Globals.DSTPort);

            Console.WriteLine($"Started listening on port {Globals.DSTPort}...");

            var peersData = new Dictionary<string, IList<IPEndPoint>>();

            while (true)
            {
                var (endPoint, group) = ReadData();
                Console.WriteLine($"received {group} from {endPoint}");

                //need to check ownership. If they are owner of domain. set them as primary. If not set them as secondary.
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

        public static async Task ConnectServerToClient(IPEndPoint endPoint)
        {
            //var portNumber = Port;
            var cudpClient = new UdpClient(13341);
            //var peerIpAddress = IPAddress.Parse(ipAddress);

            var peerEndPoint = endPoint;

            Console.WriteLine($"peer endpoint: {peerEndPoint}");

            Console.WriteLine("punching UDP hole...");
            cudpClient.Send(Array.Empty<byte>(), peerEndPoint);

            //var listenerThread = new Thread(Listen);
            //listenerThread.Start();

            _ = KeepAlive(10, peerEndPoint, true);

            bool _exit = false;

            while (!_exit)
            {
                await Task.Delay(1000);
                //Console.Write("> ");
                //var message = Console.ReadLine();
                //if (message == "/exit")
                //{
                //    _exit = true;
                //}
                //else
                //{
                //    if (!string.IsNullOrEmpty(message))
                //    {
                //        var payload = new Message { Type = MessageType.Chat, Data = message, Address = "ShopKA" };
                //        var payloadJson = GenerateMessage(payload);
                //        var messageDataBytes = Encoding.UTF8.GetBytes(payloadJson);
                //        cudpClient.Send(messageDataBytes, peerEndPoint);
                //    }
                //}
            }
        }

        private static async Task KeepAlive(int seconds, IPEndPoint peerEndPoint, bool isShop = false)
        {
            while (true)
            {
                var delay = Task.Delay(new TimeSpan(0, 0, seconds));
                var payload = new Message { Type = !isShop ? MessageType.KeepAlive : MessageType.ShopKeepAlive, Data = "" };
                var message = GenerateMessage(payload);
                var messageDataBytes = Encoding.UTF8.GetBytes(message);
                udpClient.Send(messageDataBytes, peerEndPoint);

                await delay;
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
