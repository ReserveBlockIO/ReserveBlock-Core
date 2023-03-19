using System.Net.Sockets;
using System.Net;
using System.Text;
using ReserveBlockCore.P2P;
using Newtonsoft.Json;
using ReserveBlockCore.Models.DST;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.DST
{
    public class DSTClient
    {
        static int Port = Globals.DSTClientPort;
        static UdpClient udpClient;
        static IPEndPoint RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        public static async Task Run()
        {
            var successful = Encoding.UTF8.GetBytes("successful");
            var remoteEndPoint = RemoteEndPoint;

            //This will eventually pull from a network of IPs Anything will work so long as its port is open and running the DSTServer interface
            var stunEndPoint = IPEndPoint.Parse($"185.199.226.121:{Globals.DSTPort}" ?? "");

            var portNumber = Port;
            udpClient = new UdpClient(portNumber);

            Console.Write("> add ");
            var groupName = Console.ReadLine() ?? "default";
            var addCommandDataBytes = Encoding.UTF8.GetBytes(groupName);
            udpClient.Send(addCommandDataBytes, stunEndPoint);
            var addCommandResponseBytes = udpClient.Receive(ref remoteEndPoint);

            if (!addCommandResponseBytes.SequenceEqual(successful))
            {
                Console.WriteLine("an error occurred while connecting to STUN server");
                //return 1;
            }
            Console.WriteLine("connected to STUN server");

            var peerEndPoint = ReadPeerInfo();
            Console.WriteLine($"peer endpoint: {peerEndPoint}");

            Console.WriteLine("punching UDP hole...");
            udpClient.Send(Array.Empty<byte>(), peerEndPoint);

            var listenerThread = new Thread(Listen);
            listenerThread.Start();

            _ = KeepAlive(10, peerEndPoint);

            bool _exit = false;

            while (!_exit)
            {
                Console.Write("> ");
                var message = Console.ReadLine();
                if(message == "/exit")
                {
                    _exit = true;
                }
                else
                {
                    if (!string.IsNullOrEmpty(message))
                    {
                        var payload = new Message { Type = MessageType.Chat, Data = message, Address = "AaronRBX" };
                        var payloadJson = GenerateMessage(payload);
                        var messageDataBytes = Encoding.UTF8.GetBytes(payloadJson);
                        udpClient.Send(messageDataBytes, peerEndPoint);
                    }
                }
            }
        }

        public static async Task ConnectServerToClient(string ipAddress, int peerPort )
        {
            var portNumber = Port;
            var cudpClient = new UdpClient(portNumber);
            var peerIpAddress = IPAddress.Parse(ipAddress);

            var peerEndPoint = new IPEndPoint(peerIpAddress, peerPort);

            Console.WriteLine($"peer endpoint: {peerEndPoint}");

            Console.WriteLine("punching UDP hole...");
            cudpClient.Send(Array.Empty<byte>(), peerEndPoint);

            var listenerThread = new Thread(Listen);
            listenerThread.Start();

            _ = KeepAlive(10, peerEndPoint, true);

            bool _exit = false;

            while (!_exit)
            {
                Console.Write("> ");
                var message = Console.ReadLine();
                if (message == "/exit")
                {
                    _exit = true;
                }
                else
                {
                    if (!string.IsNullOrEmpty(message))
                    {
                        var payload = new Message { Type = MessageType.Chat, Data = message, Address = "AaronRBX" };
                        var payloadJson = GenerateMessage(payload);
                        var messageDataBytes = Encoding.UTF8.GetBytes(payloadJson);
                        cudpClient.Send(messageDataBytes, peerEndPoint);
                    }
                }
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
        static void Listen()
        {
            while (true)
            {
                var messageBytes = udpClient.Receive(ref RemoteEndPoint);
                var payload = Encoding.UTF8.GetString(messageBytes);

                if (string.IsNullOrEmpty(payload)) continue;

                var message = JsonConvert.DeserializeObject<Message>(payload);

                if(message != null)
                {
                    if (message.Type != MessageType.KeepAlive)
                    {
                        message.ReceivedTimestamp = TimeUtil.GetTime();

                        ConsoleHelper.ClearCurrentLine();
                        if(!string.IsNullOrEmpty(message.Address))
                        {
                            Console.Write($"{message.Address}: {message.Data}\n> ");
                        }
                        else
                        {
                            Console.Write($"peer: {message.Data}\n> ");
                        }
                    }
                    if(message.Type == MessageType.ShopKeepAlive)
                    {
                        Console.Write($"peer: {message.Data}\n> ");
                    }
                }
            }
        }

        static IPEndPoint ReadPeerInfo()
        {
            var stunPeerInfoDataBytes = udpClient.Receive(ref RemoteEndPoint);
            var dataStringArray = Encoding.UTF8.GetString(stunPeerInfoDataBytes).Split(" ");
            var peerIpAddress = IPAddress.Parse(dataStringArray[0]);
            var peerPort = Convert.ToInt32(dataStringArray[1]);

            return new IPEndPoint(peerIpAddress, peerPort);
        }

        internal class ConsoleHelper
        {
            public static void ClearCurrentLine()
            {
                var currentLineCursor = Console.CursorTop;
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.Write(new string(' ', Console.WindowWidth));
                Console.SetCursorPosition(0, currentLineCursor);
            }
        }
        internal static string GenerateMessage(Message message)
        {
            var output = "";
            message.Build();
            output = JsonConvert.SerializeObject(message);

            return output;
        }

        internal static string GenerateMessage(MessageType mType, string message, string address)
        {
            var output = "";

            var nMessage = new Message();
            nMessage.Type = mType;
            nMessage.Data = message;
            nMessage.Address = address;

            nMessage.Build();
            output = JsonConvert.SerializeObject(nMessage);

            return output;
        }

    }
}
