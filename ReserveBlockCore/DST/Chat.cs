using System.Net.Sockets;
using System.Net;
using System.Text;
using ReserveBlockCore.P2P;

namespace ReserveBlockCore.DST
{
    public class Chat
    {
        static int Port = 13341;
        static UdpClient udpClient;
        static IPEndPoint RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        public static async Task Run()
        {
            var successful = Encoding.UTF8.GetBytes("successful");
            var remoteEndPoint = RemoteEndPoint;

            var stunEndPoint = IPEndPoint.Parse("162.251.121.150:13342" ?? "");

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

            var exit = false;
            while (true && !exit)
            {
                Console.Write("> ");
                var message = Console.ReadLine();
                if(message == "/bye")
                {
                    exit = true;
                    Console.WriteLine("Bye bye bye.");
                }
                if (!string.IsNullOrEmpty(message) && !exit)
                {
                    var messageDataBytes = Encoding.UTF8.GetBytes(message);
                    udpClient.Send(messageDataBytes, peerEndPoint);
                }
            }
        }
        static void Listen()
        {
            while (true)
            {
                var messageBytes = udpClient.Receive(ref RemoteEndPoint);
                var message = Encoding.UTF8.GetString(messageBytes);

                if (string.IsNullOrEmpty(message)) continue;

                ConsoleHelper.ClearCurrentLine();
                Console.Write($"peer: {message}\n> ");
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
    }
}
