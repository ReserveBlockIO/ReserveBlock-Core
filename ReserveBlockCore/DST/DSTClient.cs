using System.Net.Sockets;
using System.Net;
using System.Text;
using ReserveBlockCore.P2P;
using Newtonsoft.Json;
using ReserveBlockCore.Models.DST;
using ReserveBlockCore.Utilities;
using System.Reflection.Metadata;
using System.Diagnostics;
using ReserveBlockCore.Models;
using Microsoft.AspNetCore.Http;

namespace ReserveBlockCore.DST
{
    public class DSTClient
    {
        static int Port = Globals.DSTClientPort;
        static UdpClient udpClient;
        static IPEndPoint RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        public static async Task ConnectToShop(string shopAddress, string address = "na")
        {
            var successful = Encoding.UTF8.GetBytes("echo");
            var remoteEndPoint = RemoteEndPoint;
            var IsConnected = false;
            IPEndPoint? ConnectedStunServer = null;
            var FailedToConnect = false;
            var portNumber = Port;
            udpClient = new UdpClient(portNumber);

            while (!IsConnected && !FailedToConnect)
            {
                var decShop = await DecShop.GetDecShopStateTreiLeafByURL(shopAddress);
                if(decShop != null)
                {
                    var stunServer = decShop.IP + ":" + decShop.Port;

                    if (stunServer != null)
                    {
                        var stunEndPoint = IPEndPoint.Parse(stunServer);

                        var stopwatch = new Stopwatch();
                        var payload = new Message { Type = MessageType.ShopConnect, Data = "helo", Address = address };
                        var message = GenerateMessage(payload);

                        var addCommandDataBytes = Encoding.UTF8.GetBytes(message);

                        udpClient.Send(addCommandDataBytes, stunEndPoint);
                        stopwatch.Start();
                        while (stopwatch.Elapsed.TotalSeconds < 5 && !IsConnected)
                        {
                            var beginReceive = udpClient.BeginReceive(null, null);
                            beginReceive.AsyncWaitHandle.WaitOne(new TimeSpan(0, 0, 5));

                            if (beginReceive.IsCompleted)
                            {
                                try
                                {
                                    IPEndPoint remoteEP = null;
                                    byte[] receivedData = udpClient.EndReceive(beginReceive, ref remoteEP);
                                    if (receivedData.SequenceEqual(successful))
                                    {
                                        ConnectedStunServer = stunEndPoint;
                                        IsConnected = true;
                                    }
                                    else
                                    {
                                        IsConnected = false;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    IsConnected = false;
                                }
                            }
                            else
                            {
                                FailedToConnect = true;
                            }
                        }
                        stopwatch.Stop();
                    }
                }
            }
            if (IsConnected)
            {
                Console.WriteLine("connected to SHOP");


                var listenerThread = new Thread(Listen);
                listenerThread.Start();

                _ = KeepAliveService.KeepAlive(10, ConnectedStunServer, udpClient);

            }
        }

        public static async Task<bool> DisconnectFromShop()
        {
            try
            {
                udpClient.Close();
                udpClient.Dispose();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static async Task Run()
        {
            var successful = Encoding.UTF8.GetBytes("echo");
            var remoteEndPoint = RemoteEndPoint;
            var IsConnected = false;
            IPEndPoint? ConnectedStunServer = null;
            var FailedToConnect = false;
            var badList = new List<string>();
            var portNumber = Port;
            udpClient = new UdpClient(portNumber);

            while (!IsConnected && !FailedToConnect)
            {
                var stunServer = Globals.STUNServers.Where(x => !badList.Any(y => y == x)).FirstOrDefault();

                if(stunServer != null)
                {
                    var stunEndPoint = IPEndPoint.Parse(stunServer);

                    var stopwatch = new Stopwatch();
                    var payload = new Message { Type = MessageType.STUNConnect, Data = "helo" };
                    var message = GenerateMessage(payload);

                    var addCommandDataBytes = Encoding.UTF8.GetBytes(message);

                    udpClient.Send(addCommandDataBytes, stunEndPoint);
                    stopwatch.Start();
                    while (stopwatch.Elapsed.TotalSeconds < 5 && !IsConnected)
                    {
                        var beginReceive = udpClient.BeginReceive(null, null);
                        beginReceive.AsyncWaitHandle.WaitOne(new TimeSpan(0,0,5));

                        if (beginReceive.IsCompleted)
                        {
                            try
                            {
                                IPEndPoint remoteEP = null;
                                byte[] receivedData = udpClient.EndReceive(beginReceive, ref remoteEP);
                                if (receivedData.SequenceEqual(successful))
                                {
                                    ConnectedStunServer = stunEndPoint;
                                    IsConnected = true;
                                }
                                else
                                {
                                    badList.Add(stunServer);
                                    IsConnected = false;
                                }
                            }
                            catch (Exception ex)
                            {
                                // EndReceive failed and we ended up here
                            }
                        }
                        else
                        {
                            badList.Add(stunServer);
                        }
                    }
                    stopwatch.Stop();
                }
                else
                {
                    FailedToConnect = true;
                }
            }

            if(IsConnected)
            {
                Console.WriteLine("connected to STUN server");

                var listenerThread = new Thread(Listen);
                listenerThread.Start();

                _ = KeepAliveService.KeepAlive(10, ConnectedStunServer, udpClient, true);

            }
        }

        static void Listen()
        {
            while (true)
            {
                var messageBytes = udpClient.Receive(ref RemoteEndPoint);
                var payload = Encoding.UTF8.GetString(messageBytes);

                if (string.IsNullOrEmpty(payload)) continue;

                if (!string.IsNullOrEmpty(payload))
                {
                    var message = JsonConvert.DeserializeObject<Message>(payload);

                    if (message != null)
                    {
                        MessageService.ProcessMessage(message, RemoteEndPoint, udpClient);
                    }
                }
            }
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
