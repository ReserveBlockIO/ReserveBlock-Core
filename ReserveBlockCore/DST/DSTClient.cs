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
using System.Linq.Expressions;

namespace ReserveBlockCore.DST
{
    public class DSTClient
    {
        static int Port = Globals.DSTClientPort;
        static int LastUsedPort = 0;
        static UdpClient udpClient;
        static UdpClient udpShop;
        static IPEndPoint RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        static IPEndPoint? ConnectedShopServer = null;
        public static Thread? ListenerThread = null;
        public static CancellationTokenSource shopToken = new CancellationTokenSource();
        public static CancellationTokenSource stunToken = new CancellationTokenSource();
        public static int somecount = 0;

        public static async Task Run(bool bypass = false)
        {
            var myDecShop = DecShop.GetMyDecShopInfo();
            if (myDecShop != null && !bypass)
            {
                if (!myDecShop.IsOffline && !bypass)
                {
                    var successful = Encoding.UTF8.GetBytes("echo");
                    var remoteEndPoint = RemoteEndPoint;
                    var IsConnected = false;
                    IPEndPoint? ConnectedStunServer = null;
                    var FailedToConnect = false;
                    var badList = new List<string>();
                    var portNumber = Port;
                    LastUsedPort = portNumber;
                    udpShop = new UdpClient(portNumber);

                    while (!IsConnected && !FailedToConnect)
                    {
                        var stunServer = Globals.STUNServers.Where(x => !badList.Any(y => y == x)).FirstOrDefault();

                        if (stunServer != null)
                        {
                            var stunEndPoint = IPEndPoint.Parse(stunServer);

                            var stopwatch = new Stopwatch();
                            var payload = new Message { Type = MessageType.STUNConnect, Data = "helo" };
                            var message = GenerateMessage(payload);

                            var addCommandDataBytes = Encoding.UTF8.GetBytes(message);

                            udpShop.Send(addCommandDataBytes, stunEndPoint);
                            stopwatch.Start();
                            while (stopwatch.Elapsed.TotalSeconds < 5 && !IsConnected)
                            {
                                var beginReceive = udpShop.BeginReceive(null, null);
                                beginReceive.AsyncWaitHandle.WaitOne(new TimeSpan(0, 0, 5));

                                if (beginReceive.IsCompleted)
                                {
                                    try
                                    {
                                        IPEndPoint remoteEP = null;
                                        byte[] receivedData = udpShop.EndReceive(beginReceive, ref remoteEP);
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

                    if (IsConnected)
                    {
                        Console.WriteLine("connected to STUN server");

                        stunToken = new CancellationTokenSource();
                        CancellationToken token = stunToken.Token;

                        Task task = new Task(() => { ShopListen(token); }, token);
                        task.Start();

                        var kaPayload = new Message { Type = MessageType.ShopKeepAlive, Data = "" };
                        var kaMessage = GenerateMessage(kaPayload);

                        var messageBytes = Encoding.UTF8.GetBytes(kaMessage);

                        DSTConnection dstCon = new DSTConnection
                        {
                            ConnectDate = TimeUtil.GetTime(),
                            IPAddress = ConnectedStunServer.ToString(),
                            LastReceiveMessage = TimeUtil.GetTime(),
                        };

                        Globals.STUNServer = dstCon;

                        udpShop.Send(messageBytes, ConnectedStunServer);


                    }
                }
            }
        }

        public static async Task<bool> ConnectToShop(IPEndPoint shopEndPoint, string shopServer, string address = "NA")
        {
            ListenerThread?.Interrupt();
            bool connected = false;
            var successful = Encoding.UTF8.GetBytes("echo");
            RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            ConnectedShopServer = null;
            var remoteEndPoint = RemoteEndPoint;
            var IsConnected = false;
            IPEndPoint? ConnectedStunServer = null;
            var FailedToConnect = false;

            var portNumber = PortUtility.FindOpenUDPPort(LastUsedPort); //dynamic port / Port == LastUsedPort ? LastUsedPort + 1 : Port;
            udpClient = new UdpClient(portNumber);
            LastUsedPort = portNumber;

            while (!IsConnected && !FailedToConnect)
            {
                var stopwatch = new Stopwatch();
                var payload = new Message { Type = MessageType.ShopConnect, Data = "helo", Address = address };
                var message = GenerateMessage(payload);

                var addCommandDataBytes = Encoding.UTF8.GetBytes(message);

                udpClient.Send(addCommandDataBytes, shopEndPoint);
                STUN(shopServer);

                //Give shop time to punch
                await Task.Delay(1000);
                udpClient.Send(addCommandDataBytes, shopEndPoint);

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
                                ConnectedStunServer = shopEndPoint;
                                ConnectedShopServer = shopEndPoint;
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
            if (IsConnected)
            {
                connected = true;
                Console.WriteLine("connected to SHOP");

                shopToken = new CancellationTokenSource();
                CancellationToken token = shopToken.Token;

                Task task = new Task(() => { Listen(token); }, token);
                task.Start();

                var kaPayload = new Message { Type = MessageType.KeepAlive, Data = "" };
                var kaMessage = GenerateMessage(kaPayload);

                var messageBytes = Encoding.UTF8.GetBytes(kaMessage);

                Globals.ConnectedClients.TryGetValue(ConnectedStunServer.ToString(), out var client);
                if (client != null)
                {
                    client.LastReceiveMessage = TimeUtil.GetTime();
                    client.IsConnected = false;
                    Globals.ConnectedClients[ConnectedStunServer.ToString()] = client;
                }
                else
                {
                    client = new DSTConnection
                    {
                        LastReceiveMessage = TimeUtil.GetTime(),
                        ConnectDate = TimeUtil.GetTime(),
                        IPAddress = ConnectedStunServer.ToString(),
                    };

                    Globals.ConnectedClients[ConnectedStunServer.ToString()] = client;
                }

                udpClient.Send(messageBytes, ConnectedStunServer);

                return connected;
            }
            return connected;
        }

        public static async Task<bool> ConnectToShop(string shopAddress, string address = "na")
        {
            ListenerThread?.Interrupt();
            bool connected = false;
            var successful = Encoding.UTF8.GetBytes("echo");
            RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            ConnectedShopServer = null;
            var remoteEndPoint = RemoteEndPoint;
            var IsConnected = false;
            IPEndPoint? ConnectedStunServer = null;
            var FailedToConnect = false;

            var portNumber = PortUtility.FindOpenUDPPort(LastUsedPort); //dynamic port / Port == LastUsedPort ? LastUsedPort + 1 : Port;
            udpClient = new UdpClient(portNumber);
            LastUsedPort = portNumber;

            while (!IsConnected && !FailedToConnect)
            {
                var decShop = await DecShop.GetDecShopStateTreiLeafByURL(shopAddress);
                if(decShop != null)
                {
                    var shopServer = decShop.IP + ":" + decShop.Port;

                    if (shopServer != null)
                    {
                        var shopEndPoint = IPEndPoint.Parse(shopServer);

                        var stopwatch = new Stopwatch();
                        var payload = new Message { Type = MessageType.ShopConnect, Data = "helo", Address = address };
                        var message = GenerateMessage(payload);

                        var addCommandDataBytes = Encoding.UTF8.GetBytes(message);

                        udpClient.Send(addCommandDataBytes, shopEndPoint);
                        STUN(shopServer);

                        //Give shop time to punch
                        await Task.Delay(1000);
                        udpClient.Send(addCommandDataBytes, shopEndPoint);

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
                                        ConnectedStunServer = shopEndPoint;
                                        ConnectedShopServer = shopEndPoint;
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
                connected = true;
                Console.WriteLine("connected to SHOP");

                somecount += 1;
                shopToken = new CancellationTokenSource();
                CancellationToken token = shopToken.Token;

                Task task = new Task(() => { Listen(token); }, token);
                task.Start();

                //ListenerThread = new Thread(Listen);
                //ListenerThread.Start();

                var kaPayload = new Message { Type = MessageType.KeepAlive, Data = "" };
                var kaMessage = GenerateMessage(kaPayload);

                var messageBytes = Encoding.UTF8.GetBytes(kaMessage);

                Globals.ConnectedClients.TryGetValue(ConnectedStunServer.ToString(), out var client);
                if (client != null)
                {
                    client.LastReceiveMessage = TimeUtil.GetTime();
                    client.IsConnected = false;
                    Globals.ConnectedClients[ConnectedStunServer.ToString()] = client;
                }
                else
                {
                    client = new DSTConnection
                    {
                        LastReceiveMessage = TimeUtil.GetTime(),
                        ConnectDate = TimeUtil.GetTime(),
                        IPAddress = ConnectedStunServer.ToString(),
                    };

                    Globals.ConnectedClients[ConnectedStunServer.ToString()] = client;
                }

                udpClient.Send(messageBytes, ConnectedStunServer);

                return connected;
            }
            return connected;
        }

        private static void STUN(string shopEndPoint)
        {
            var successful = Encoding.UTF8.GetBytes("ack");
            var remoteEndPoint = RemoteEndPoint;
            var IsConnected = false;
            IPEndPoint? ConnectedStunServer = null;
            var FailedToConnect = false;
            var badList = new List<string>();
            var portNumber = Port;

            while (!IsConnected && !FailedToConnect)
            {
                var stunServer = Globals.STUNServers.Where(x => !badList.Any(y => y == x)).FirstOrDefault();

                if (stunServer != null)
                {
                    var stunEndPoint = IPEndPoint.Parse(stunServer);

                    var stopwatch = new Stopwatch();
                    var payload = new Message { Type = MessageType.STUN, Data = shopEndPoint };
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

            if (IsConnected)
            {
                Console.WriteLine("Connected to STUN server...");
                Console.WriteLine("Waiting to be UDP punched...");

            }
        }

        static async Task ShopListen(CancellationToken token)
        {
            var exit = false;
            while (!exit && !token.IsCancellationRequested)
            {
                try
                {
                    var isCancelled = token.IsCancellationRequested;
                    if (isCancelled)
                    {
                        exit = true;
                        continue;
                    }
                    var dataGram = await udpShop.ReceiveAsync(token);
                    RemoteEndPoint = dataGram.RemoteEndPoint;
                    var payload = Encoding.UTF8.GetString(dataGram.Buffer);

                    if (string.IsNullOrEmpty(payload) || payload == "ack" || payload == "nack" || payload == "fail" || payload == "dc" || payload == "echo")
                    {
                        if (Globals.ShowSTUNMessagesInConsole)
                            Console.WriteLine(payload);
                        continue;
                    }

                    if (!string.IsNullOrEmpty(payload))
                    {
                        if (Globals.ShowSTUNMessagesInConsole)
                            Console.WriteLine(payload + "\n");

                        var message = JsonConvert.DeserializeObject<Message>(payload);

                        if (message != null)
                        {
                            _ = MessageService.ProcessMessage(message, RemoteEndPoint, udpShop);
                        }
                    }
                }
                catch { }
            }
        }

        static async Task Listen(CancellationToken token)
        {
            var counter = somecount;

            var exit = false;
            while (!exit && !token.IsCancellationRequested)
            {
                try
                {
                    var isCancelled = token.IsCancellationRequested;
                    if(isCancelled)
                    {
                        exit = true;
                        continue;
                    }
                    var dataGram = await udpClient.ReceiveAsync(token);
                    RemoteEndPoint = dataGram.RemoteEndPoint;
                    var payload = Encoding.UTF8.GetString(dataGram.Buffer);

                    if (string.IsNullOrEmpty(payload) || payload == "ack" || payload == "nack" || payload == "fail" || payload == "dc" || payload == "echo") 
                    {
                        if (Globals.ShowSTUNMessagesInConsole)
                            Console.WriteLine(payload);
                        continue;
                    }

                    if (!string.IsNullOrEmpty(payload))
                    {
                        if(Globals.ShowSTUNMessagesInConsole)
                            Console.WriteLine(payload + "\n");  

                        var message = JsonConvert.DeserializeObject<Message>(payload);

                        if (message != null)
                        {
                            _ = MessageService.ProcessMessage(message, RemoteEndPoint, udpClient);
                        }
                    }
                }
                catch 
                { 

                }
            }
        }


        public static async Task<bool> DisconnectFromShop(bool keepShopData = false)
        {
            try
            {
                var connectedClients = Globals.ConnectedClients.Values.Where(x => x.IsConnected);
                foreach (var client in connectedClients)
                {
                    Globals.ConnectedClients.TryRemove(client.IPAddress, out _);
                }

                shopToken.Cancel();

                if(!keepShopData)
                    Globals.DecShopData = null;
                udpClient.Close();
                udpClient.Dispose();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> DisconnectFromSTUNServer(bool keepShopData = false, bool keepSTUNData = false)
        {
            try
            {
                if (Globals.STUNServer != null)
                {
                    stunToken.Cancel();

                    if (!keepShopData)
                        Globals.DecShopData = null;

                    if(!keepSTUNData)
                        Globals.STUNServer = null;

                    udpShop.Close();
                    udpShop.Dispose();

                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public static async Task SendShopMessageFromClient(Message message, bool responseRequested)
        {
            var shopMessage = MessageService.GenerateMessage(message, responseRequested);
            var messageBytes = Encoding.UTF8.GetBytes(shopMessage);

            _ = udpClient.SendAsync(messageBytes, ConnectedShopServer);
        }

        public static async Task SendClientMessageFromShop(Message message, IPEndPoint endPoint, bool responseRequested)
        {
            var shopMessage = MessageService.GenerateMessage(message, responseRequested);
            var messageBytes = Encoding.UTF8.GetBytes(shopMessage);

            _ = udpShop.SendAsync(messageBytes, endPoint);
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
