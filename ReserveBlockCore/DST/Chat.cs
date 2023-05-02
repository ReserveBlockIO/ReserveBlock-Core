using System.Net.Sockets;
using System.Net;
using System.Text;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;
using ReserveBlockCore.Models.DST;
using Newtonsoft.Json;

namespace ReserveBlockCore.DST
{
    public class Chat
    {
        static int Port = 13343;
        static UdpClient udpClient;
        static IPEndPoint RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        private static bool EndChat = false;
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

            while (true && !EndChat)
            {
                Console.Write("> ");
                var message = Console.ReadLine();
                if (message == "/bye")
                {
                    EndChat = true;
                    Console.WriteLine("Bye bye bye.");
                    udpClient.Close();
                    udpClient.Dispose();
                }
                if (!string.IsNullOrEmpty(message) && !EndChat)
                {
                    var messageDataBytes = Encoding.UTF8.GetBytes(message);
                    udpClient.Send(messageDataBytes, peerEndPoint);
                }
            }
        }
        static void Listen()
        {
            while (true && !EndChat)
            {
                try
                {
                    var messageBytes = udpClient.Receive(ref RemoteEndPoint);
                    var message = Encoding.UTF8.GetString(messageBytes);

                    if (string.IsNullOrEmpty(message)) continue;

                    ConsoleHelper.ClearCurrentLine();
                    Console.Write($"peer: {message}\n> ");
                }
                catch { }
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

        public class ChatPayload
        {
            public string ToAddress { get; set; }
            public string FromAddress { get; set; }
            public string Message { get; set; }
            public string? Signature { get; set; }
            public long? TimeStamp { get; set; }
        }
        public class ChatMessage
        {
            public string Id { get; set; }
            public string Message { get; set; }
            public string MessageHash { get; set; }
            public string ToAddress { get; set; }
            public string FromAddress { get; set; } 
            public string Signature { get; set; }
            public long TimeStamp { get; set; }
            public string ShopURL { get; set; }
            public bool MessageReceived { get; set; }
            public bool IsShopSentMessage { get; set; }
            public bool IsSignatureValid { get { return SignatureService.VerifySignature(FromAddress, FromAddress + TimeStamp.ToString(), Signature); } }
            public bool IsMessageHashValid { get { return VerifyMessageHash(Message, MessageHash); } }
            public bool IsMessageTrusted { get { return IsSignatureValid && IsMessageHashValid ? true : false; } }
        }

        public static bool ValidateChatMessage(ChatMessage message)
        {
            if(!message.IsMessageTrusted) return false;

            return true;
        }
        public static bool VerifyMessageHash(string message, string hash)
        {
            var messageHash = message.ToHash();

            if (messageHash == hash)
            {
                return true;
            }

            return false;
        }

        public static byte[]? CreateChatReceivedMessage(ChatMessage chatMessage)
        {
            Message receivedMessage = new Message 
            { 
                Type = MessageType.ChatRec,
                ComType = MessageComType.Chat,
                Data = chatMessage.IsShopSentMessage ? chatMessage.ToAddress + "," + chatMessage.Id : chatMessage.ShopURL + "," + chatMessage.Id,
            };

            var messageJson = JsonConvert.SerializeObject(receivedMessage);
            var successMessage = Encoding.UTF8.GetBytes(messageJson);
            return successMessage;
        }
    }
}
