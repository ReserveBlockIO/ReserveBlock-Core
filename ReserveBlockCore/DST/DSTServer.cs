using System.Net.Sockets;
using System.Net;
using System.Text;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Models.DST;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.DST
{
    public class DSTServer
    {
        public static UdpClient udpClient;
        private static ConcurrentDictionary<int, UdpClient> UdpClientDict = new ConcurrentDictionary<int, UdpClient>();

        public static async Task Run()
        {
            try
            {
                var port = Globals.MinorVer == 5 && Globals.MajorVer == 3 ? 3440 : Globals.SelfSTUNPort; //temporary fix until next release. Adding it this way in case its forgotten.
                udpClient = new UdpClient(port);

                Globals.STUNServerRunning = true;
                while (true)
                {
                    ReadData();
                }
            }
            catch(Exception ex)
            {
                LogUtility.LogQueue($"Error starting DST STUN server. Error: {ex.ToString()}", "DSTServer.Run()", "rbxlog.txt", true);
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
                    _ = MessageService.ProcessMessage(payload, endPoint, udpClient);
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
