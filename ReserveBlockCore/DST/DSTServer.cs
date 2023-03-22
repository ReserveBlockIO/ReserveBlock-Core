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
            bool stop = false;
            while (true && !stop)
            {
                if(Globals.ConnectedShops.TryGetValue(peerEndPoint.ToString(), out var shop))
                {
                    if(shop != null)
                    {
                        var delay = Task.Delay(new TimeSpan(0, 0, seconds));
                        var payload = new Message { Type = !isShop ? MessageType.KeepAlive : MessageType.ShopKeepAlive, Data = "" };
                        var message = GenerateMessage(payload);
                        var messageDataBytes = Encoding.UTF8.GetBytes(message);
                        udpClient.Send(messageDataBytes, peerEndPoint);

                        shop.LastSentMessage = TimeUtil.GetTime();

                        Globals.ConnectedShops[peerEndPoint.ToString()] = shop;

                        var currentTime = TimeUtil.GetTime();
                        if(currentTime - shop.LastReceiveMessage < 30)
                        {
                            stop = true;
                            Globals.ConnectedShops.TryRemove(peerEndPoint.ToString(), out _);    
                        }

                        await delay;
                    }
                }               
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
