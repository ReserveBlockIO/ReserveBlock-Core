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
using static ReserveBlockCore.Models.Mother;
using System.Xml;
using System;
using Docnet.Core.Bindings;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Concurrent;
using ReserveBlockCore.Models.SmartContracts;

namespace ReserveBlockCore.DST
{
    public class DSTClient
    {
        static int Port = Globals.DSTClientPort;
        static int LastUsedPort = 0;
        static UdpClient udpClient;
        static UdpClient udpAssets;
        static UdpClient udpShop;
        static IPEndPoint RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        static IPEndPoint RemoteEndPointAssets = new IPEndPoint(IPAddress.Any, 0);
        static IPEndPoint? ConnectedShopServer = null;
        static IPEndPoint? ConnectedShopServerAssets = null;
        public static Thread? ListenerThread = null;
        public static Thread? ListenerThreadAssets = null;
        public static CancellationTokenSource shopToken = new CancellationTokenSource();
        public static CancellationTokenSource shopAssetToken = new CancellationTokenSource();
        public static CancellationTokenSource stunToken = new CancellationTokenSource();
        public static int somecount = 0;
        private static string AssetConnectionId = "";
        public static ConcurrentDictionary<string, bool> AssetDownloadQueue = new ConcurrentDictionary<string, bool>();
        public static bool NewCollectionsFound = false;
        public static bool NewAuctionsFound = false;
        public static bool NewListingsFound = false;
        static SemaphoreSlim AssetDownloadLock = new SemaphoreSlim(1, 1);

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
                    var badList = new List<StunServer>();
                    var portNumber = Port;
                    LastUsedPort = LastUsedPort == 0 ? portNumber : LastUsedPort;
                    udpShop = new UdpClient(portNumber);
                    var delay = Task.Delay(new TimeSpan(0,2,0));

                    if (myDecShop.HostingType == DecShopHostingType.SelfHosted)
                        IsConnected = true;

                    while (!IsConnected)
                    {
                        var stunServer = Globals.STUNServers.Where(x => !badList.Any(y => y == x) && x.Group == myDecShop.STUNServerGroup).FirstOrDefault();

                        if (stunServer == null)
                        {
                            var failOverSTUN = Globals.STUNServers.Where(x => x.Group == 0).FirstOrDefault();
                            if (failOverSTUN != null)
                            {
                                var exist = badList.Exists(x => x.ServerIPPort == failOverSTUN.ServerIPPort);
                                if (!exist)
                                    stunServer = failOverSTUN;
                            }
                        }

                        if (stunServer != null)
                        {
                            var stunEndPoint = IPEndPoint.Parse(stunServer.ServerIPPort);

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
                            await delay;
                            badList = new List<StunServer>();
                        }
                    }

                    if (IsConnected)
                    {
                        Console.WriteLine("connected to STUN server");

                        stunToken = new CancellationTokenSource();
                        CancellationToken token = stunToken.Token;

                        Task task = new Task(async () => { await ShopListen(token); }, token);
                        task.Start();

                        Task taskClear = new Task(async () => { await ClearStaleConnections(token); }, token);
                        taskClear.Start();

                        if (myDecShop.HostingType != DecShopHostingType.SelfHosted)
                        {
                            var kaPayload = new Message { Type = MessageType.ShopKeepAlive, Data = "" };
                            var kaMessage = GenerateMessage(kaPayload);

                            var messageBytes = Encoding.UTF8.GetBytes(kaMessage);

                            DSTConnection dstCon = new DSTConnection
                            {
                                ConnectDate = TimeUtil.GetTime(),
                                IPAddress = ConnectedStunServer != null ? ConnectedStunServer.ToString() : "NA",
                                LastReceiveMessage = TimeUtil.GetTime(),
                                ConnectionId = RandomStringUtility.GetRandomString(12, true),
                                AttemptReconnect = true
                            };

                            Globals.STUNServer = dstCon;

                            udpShop.Send(messageBytes, ConnectedStunServer);
                        }
                    }
                }
            }
        }

        public static async Task<bool> ConnectToShop(IPEndPoint shopEndPoint, string shopServer, string address = "NA", string shopURL = "NA")
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

                await STUN(shopServer);

                //Give shop time to punch
                await Task.Delay(1000);
                udpClient.Send(addCommandDataBytes, shopEndPoint);
                await Task.Delay(200);
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

                Task taskData = new Task(() => { UpdateShopData(token); }, token);
                taskData.Start();

                Task taskDataLoop = new Task(() => { GetShopDataLoop(token, address); }, token);
                taskDataLoop.Start();

                Task taskAssets = new Task(() => { GetShopListingAssets(token, address); }, token);
                taskAssets.Start();


                var kaPayload = new Message { Type = MessageType.KeepAlive, Data = "" };
                var kaMessage = GenerateMessage(kaPayload);

                var messageBytes = Encoding.UTF8.GetBytes(kaMessage);

                Globals.ConnectedClients.TryGetValue(ConnectedStunServer.ToString(), out var client);
                if (client != null)
                {
                    client.LastReceiveMessage = TimeUtil.GetTime();
                    client.ConnectionId = RandomStringUtility.GetRandomString(12, true);
                    client.KeepAliveStarted = false;

                    Globals.ConnectedClients[ConnectedStunServer.ToString()] = client;
                }
                else
                {
                    client = new DSTConnection
                    {
                        LastReceiveMessage = TimeUtil.GetTime(),
                        ConnectDate = TimeUtil.GetTime(),
                        IPAddress = ConnectedStunServer.ToString(),
                        ShopURL = shopURL,
                        ConnectionId = RandomStringUtility.GetRandomString(12, true),
                        AttemptReconnect = true
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
                    if (decShop.IP == "NA")
                        return false;

                    var shopServer = decShop.IP + ":" + decShop.Port;

                    if (shopServer != null)
                    {
                        var shopEndPoint = IPEndPoint.Parse(shopServer);

                        var stopwatch = new Stopwatch();
                        var payload = new Message { Type = MessageType.ShopConnect, Data = "helo", Address = address };
                        var message = GenerateMessage(payload);

                        var addCommandDataBytes = Encoding.UTF8.GetBytes(message);

                        udpClient.Send(addCommandDataBytes, shopEndPoint);

                        if(decShop.HostingType == DecShopHostingType.Network)
                            await STUN(shopServer, decShop.STUNServerGroup);

                        //Give shop time to punch
                        await Task.Delay(1000);
                        udpClient.Send(addCommandDataBytes, shopEndPoint);
                        await Task.Delay(200);
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

                Task taskData = new Task(() => { UpdateShopData(token); }, token);
                taskData.Start();

                Task taskDataLoop = new Task(() => { GetShopDataLoop(token, address); }, token);
                taskDataLoop.Start();

                Task taskAssets = new Task(() => { GetShopListingAssets(token, address); }, token);
                taskAssets.Start();

                var kaPayload = new Message { Type = MessageType.KeepAlive, Data = "" };
                var kaMessage = GenerateMessage(kaPayload);

                var messageBytes = Encoding.UTF8.GetBytes(kaMessage);

                Globals.ConnectedClients.TryGetValue(ConnectedStunServer.ToString(), out var client);
                if (client != null)
                {
                    client.LastReceiveMessage = TimeUtil.GetTime();
                    client.ConnectionId = RandomStringUtility.GetRandomString(12, true);
                    client.KeepAliveStarted = false;

                    Globals.ConnectedClients[ConnectedStunServer.ToString()] = client;
                }
                else
                {
                    client = new DSTConnection
                    {
                        LastReceiveMessage = TimeUtil.GetTime(),
                        ConnectDate = TimeUtil.GetTime(),
                        IPAddress = ConnectedStunServer.ToString(),
                        ShopURL = shopAddress,
                        ConnectionId = RandomStringUtility.GetRandomString(12, true),
                        AttemptReconnect = true
                    };

                    Globals.ConnectedClients[ConnectedStunServer.ToString()] = client;
                }

                udpClient.Send(messageBytes, ConnectedStunServer);

                return connected;
            }
            return connected;
        }

        public static async Task<bool> ConnectToShopForAssets()
        {
            ListenerThreadAssets?.Interrupt();
            bool connected = false;
            var successful = Encoding.UTF8.GetBytes("echo");
            RemoteEndPointAssets = new IPEndPoint(IPAddress.Any, 0);
            ConnectedShopServerAssets = null;
            var remoteEndPoint = RemoteEndPointAssets;
            var IsConnected = false;
            IPEndPoint? ConnectedStunServer = null;
            var FailedToConnect = false;

            var portNumber = PortUtility.FindOpenUDPPort(LastUsedPort); //dynamic port / Port == LastUsedPort ? LastUsedPort + 1 : Port;
            udpAssets = new UdpClient(portNumber);
            LastUsedPort = portNumber;

            while (!IsConnected && !FailedToConnect)
            {
                var connectedClients = Globals.ConnectedClients.Where(x => x.Value.IsConnected).Take(1);
                if (connectedClients.Count() > 0)
                {
                    var connectedStore = connectedClients.FirstOrDefault().Value;
                    var shopServer = connectedStore.IPAddress;

                    if (shopServer != null)
                    {
                        var shopEndPoint = IPEndPoint.Parse(shopServer);

                        var stopwatch = new Stopwatch();
                        var payload = new Message { Type = MessageType.ShopConnect, Data = "helo", Address = "NA" };
                        var message = GenerateMessage(payload);

                        var addCommandDataBytes = Encoding.UTF8.GetBytes(message);

                        udpAssets.Send(addCommandDataBytes, shopEndPoint);
                        //STUN(shopServer);
                        var punchMeMessage = new Message { Type = MessageType.AssetPunchClient, Data = portNumber.ToString(), Address = "NA" };
                        await SendShopMessageFromClient(punchMeMessage, false);

                        stopwatch.Start();
                        while (stopwatch.Elapsed.TotalSeconds < 5 && !IsConnected)
                        {
                            var beginReceive = udpAssets.BeginReceive(null, null);
                            beginReceive.AsyncWaitHandle.WaitOne(new TimeSpan(0, 0, 5));

                            if (beginReceive.IsCompleted)
                            {
                                try
                                {
                                    IPEndPoint remoteEP = null;
                                    byte[] receivedData = udpAssets.EndReceive(beginReceive, ref remoteEP);
                                    if (receivedData.SequenceEqual(successful))
                                    {
                                        ConnectedStunServer = shopEndPoint;
                                        ConnectedShopServerAssets = shopEndPoint;
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
                Console.WriteLine("connected to SHOP - Assets");

                return connected;
            }
            return connected;
        }

        private static async Task STUN(string shopEndPoint, int groupNum = 0)
        {
            var successful = Encoding.UTF8.GetBytes("ack");
            var remoteEndPoint = RemoteEndPoint;
            var IsConnected = false;
            IPEndPoint? ConnectedStunServer = null;
            var FailedToConnect = false;
            var badList = new List<StunServer>();
            var portNumber = Port;
            var delay = Task.Delay(new TimeSpan(0,2,0));

            if (Globals.IsTestNet)
                groupNum = 1;

            while (!IsConnected && !FailedToConnect)
            {
                var stunServer = Globals.STUNServers.Where(x => !badList.Any(y => y == x) && x.Group == groupNum).FirstOrDefault();

                if(stunServer == null)
                {
                    var failOverSTUN = Globals.STUNServers.Where(x => x.Group == 0).FirstOrDefault();
                    if(failOverSTUN != null)
                    {
                        var exist = badList.Exists(x => x.ServerIPPort == failOverSTUN.ServerIPPort);
                        if(!exist)
                            stunServer = failOverSTUN;
                    }
                }

                if (stunServer != null)
                {
                    var stunEndPoint = IPEndPoint.Parse(stunServer.ServerIPPort);

                    var stopwatch = new Stopwatch();
                    var payload = new Message { Type = MessageType.STUN, Data = shopEndPoint };
                    var message = GenerateMessage(payload);

                    var addCommandDataBytes = Encoding.UTF8.GetBytes(message);

                    udpClient.Send(addCommandDataBytes, stunEndPoint);
                    await Task.Delay(200);
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
                            var newPortNumber = PortUtility.FindOpenUDPPort(LastUsedPort); //dynamic port / Port == LastUsedPort ? LastUsedPort + 1 : Port;
                            udpClient = new UdpClient(newPortNumber);
                            LastUsedPort = newPortNumber;
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

        static async Task ClearStaleConnections(CancellationToken token)
        {
            var exit = false;
            while (!exit && !token.IsCancellationRequested)
            {
                var delay = Task.Delay(new TimeSpan(0, 2, 0));
                var isCancelled = token.IsCancellationRequested;
                if (isCancelled)
                {
                    exit = true;
                    continue;
                }

                var clientsToRemoveList = Globals.ConnectedClients.Values.Where(x => !x.IsConnected).ToList();
                if(clientsToRemoveList.Count > 0)
                {
                    foreach (var client in clientsToRemoveList)
                    {
                        Globals.ConnectedClients.TryRemove(client.IPAddress, out _);
                    }
                }
                
                await delay;
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

                    if(!string.IsNullOrEmpty(payload))
                    {
                        _ = Task.Run(async () => await MessageService.ProcessMessage(payload, RemoteEndPoint, udpShop));
                        //_ = MessageService.ProcessMessage(payload, RemoteEndPoint, udpShop);
                    }
                }
                catch { }
            }
        }

        public static async Task PassMessage(UdpReceiveResult dataGram)
        {
            RemoteEndPoint = dataGram.RemoteEndPoint;
            var payload = Encoding.UTF8.GetString(dataGram.Buffer);

            if (!string.IsNullOrEmpty(payload))
            {
                _ = Task.Run(() => MessageService.ProcessMessage(payload, dataGram.RemoteEndPoint, udpShop));
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

                    if (!string.IsNullOrEmpty(payload))
                    {
                        _ = Task.Run(async () => await MessageService.ProcessMessage(payload, RemoteEndPoint, udpClient));
                    }
                }
                catch 
                { 

                }
            }
        }

        static async Task UpdateShopData(CancellationToken token)
        {
            var counter = somecount;

            //wait 1 minute before starting
            await Task.Delay(60000);
            var exit = false;
            while (!exit && !token.IsCancellationRequested)
            {
                try
                {
                    var delay = Task.Delay(new TimeSpan(0, 0, 60));
                    var isCancelled = token.IsCancellationRequested;
                    if (isCancelled)
                    {
                        exit = true;
                        continue;
                    }

                    Message message = new Message {
                        Data = $"{DecShopRequestOptions.Update},0",
                        Type = MessageType.DecShop,
                        ComType = MessageComType.Request
                    };

                    _ = SendShopMessageFromClient(message, true);

                    await Task.Delay(new TimeSpan(0, 0, 60));
                }
                catch
                {

                }
            }
        }

        public static async Task<bool> DisconnectFromAsset(bool keepShopData = false)
        {
            try
            {
                udpAssets.Close();
                udpAssets.Dispose();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> DisconnectFromShop(bool keepShopData = false)
        {
            try
            {
                //var connectedClients = Globals.ConnectedClients.Values.Where(x => x.IsConnected);
                //foreach (var client in connectedClients)
                //{
                //    Globals.ConnectedClients.TryRemove(client.IPAddress, out _);
                //}

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

        public static async Task GetListingAssetThumbnails(Message message, string scUID)
        {
            await AssetDownloadLock.WaitAsync();
            try
            {
                Globals.AssetDownloadLock = true;
                var shopMessage = MessageService.GenerateMessage(message, false);
                var messageBytes = Encoding.UTF8.GetBytes(shopMessage);
                var stopProcess = false;
                int attempts = 0;
                NFTLogUtility.Log("NFT asset thumbnail process start. Requesting Asset List", "DSTClient.GetListingAssetThumbnails()-1");

                while (!stopProcess)
                {
                    try
                    {
                        _ = udpAssets.SendAsync(messageBytes, ConnectedShopServerAssets);
                        var messageDataGram = await udpAssets.ReceiveAsync().WaitAsync(new TimeSpan(0, 0, 5)); //wait to receive list
                        attempts += 1;

                        if (attempts == 5)
                            stopProcess = true;

                        var messageBuffer = Encoding.UTF8.GetString(messageDataGram.Buffer);
                        var messageFromJson = JsonConvert.DeserializeObject<Message>(messageBuffer);

                        if (messageFromJson == null)
                            continue;

                        var messageData = messageFromJson.Data;
                        var messageDataArray = messageData.Split(new string[] { "<|>" }, StringSplitOptions.None);
                        var scuidRet = messageDataArray[0];
                        var assetListJson = messageDataArray[1];

                        if (scuidRet == null)
                            continue;

                        if (scuidRet != scUID)
                            continue;

                        if (assetListJson == null)
                            continue;

                        var assetList = JsonConvert.DeserializeObject<List<string>?>(assetListJson);

                        if (assetList == null)
                            continue;

                        if (assetList.Count > 0)
                        {
                            int byteSize = 0;
                            char delim = ',';
                            var assetListDelim = String.Join(delim, assetList);
                            NFTLogUtility.Log("NFT asset list process acquired. Loop started.", "DSTClient.GetListingAssetThumbnails()-2");
                            await Task.Delay(20);
                            NFTLogUtility.Log($"AssetList: {assetListDelim}", "DSTClient.GetListingAssetThumbnails()-2.1");

                            var assetCount = assetList.Count;
                            var assetSuccessCount = 0;

                            foreach (var asset in assetList)
                            {
                                try
                                {
                                    var _asset = asset;
                                    if (!asset.EndsWith(".jpg"))
                                    {
                                        var assetArray = asset.Split('.');
                                        var extIndex = assetArray.Length - 1;
                                        var extToReplace = assetArray[extIndex];
                                        if (!Globals.ValidExtensions.Contains(extToReplace))
                                        {
                                            //skip as its not a valid extension type.
                                            assetCount -= 1;
                                            continue;
                                        }
                                        _asset = asset.Replace(extToReplace, "jpg");
                                    }
                                    //craft message to start process.
                                    var uniqueId = RandomStringUtility.GetRandomStringOnlyLetters(10, false);

                                    var path = NFTAssetFileUtility.CreateNFTAssetPath(_asset, scUID, true);

                                    if (File.Exists(path))
                                    {
                                        var fileLength = File.ReadAllBytes(path).Length;
                                        {
                                            if (fileLength == 0)
                                            {
                                                File.Delete(path);
                                            }
                                            else
                                            {
                                                assetSuccessCount += 1;
                                                continue;
                                            }
                                        }
                                    }

                                    using (var fileStream = File.Create(path))
                                    {

                                        NFTLogUtility.Log($"Path created: {path}", "DSTClient.GetListingAssetThumbnails()-3");
                                        int expectedSequenceNumber = 0;
                                        byte[]? imageData = null;
                                        int timeouts = 0;
                                        int latencyWait = 0;
                                        bool stopAssetBuild = false;
                                        var stopWatch = new Stopwatch();
                                        while (!stopAssetBuild)
                                        {
                                            try
                                            {
                                                await Task.Delay(latencyWait);
                                                var messageAssetBytes = await GenerateAssetAckMessage(uniqueId, _asset, scUID, expectedSequenceNumber);
                                                stopWatch.Restart();
                                                stopWatch.Start();
                                                _ = udpAssets.SendAsync(messageAssetBytes, messageAssetBytes.Length, ConnectedShopServerAssets); //this starts the first file download. Next receive should be the first set of bytes
                                                                                                                                                 //await Task.Delay(10);
                                                                                                                                                 //_ = udpAssets.SendAsync(messageAssetBytes, ConnectedShopServerAssets); 

                                                var response = await udpAssets.ReceiveAsync().WaitAsync(new TimeSpan(0, 0, 5));
                                                var packetData = response.Buffer;
                                                stopWatch.Stop();
                                                latencyWait = (int)stopWatch.ElapsedMilliseconds;
                                                //NFTLogUtility.Log($"{_asset} | Ping: {stopWatch.ElapsedMilliseconds} ms", "DSTClient.GetListingAssetThumbnails()");
                                                Console.WriteLine($"{_asset} | Ping: {stopWatch.ElapsedMilliseconds} ms");
                                                await Task.Delay(200);// adding delay to avoid massive overhead on the UDP port. 
                                                                      // Check if this is the last packet
                                                bool isLastPacket = packetData.Length < 1024;

                                                //checking to see if byte is -1. If so no image. Delete and move on.
                                                if (isLastPacket && packetData.Length == 1)
                                                {
                                                    try
                                                    {
                                                        byte[] byteArray = new byte[] { 0xFF };
                                                        sbyte signedByte = unchecked((sbyte)byteArray[0]);
                                                        int intValue = Convert.ToInt32(signedByte);

                                                        if (intValue == -1)
                                                        {
                                                            stopAssetBuild = true;
                                                            expectedSequenceNumber = 0;
                                                            imageData = null;
                                                            var pathToDelete = NFTAssetFileUtility.CreateNFTAssetPath(_asset, scUID, true);
                                                            assetSuccessCount += 1;
                                                            if (File.Exists(pathToDelete))
                                                            {
                                                                fileStream.Dispose();
                                                                File.Delete(pathToDelete);
                                                            }
                                                            break;
                                                        }
                                                    }
                                                    catch { }
                                                }
                                                // Extract the sequence number from the packet
                                                int sequenceNumber = BitConverter.ToInt32(packetData, 0);

                                                Console.WriteLine($"Seq: {sequenceNumber} | ExpSeq: {expectedSequenceNumber}");
                                                //NFTLogUtility.Log($"Seq: {sequenceNumber} | ExpSeq: {expectedSequenceNumber}", "DSTClient.GetListingAssetThumbnails()-S");
                                                if (sequenceNumber != expectedSequenceNumber)
                                                {
                                                    // If not, discard the packet and request a retransmission
                                                    var expSeqNum = expectedSequenceNumber == 0 ? 0 : expectedSequenceNumber;
                                                    var ackPacket = BitConverter.GetBytes(expSeqNum);
                                                    messageAssetBytes = await GenerateAssetAckMessage(uniqueId, _asset, scUID, expSeqNum);
                                                    await Task.Delay(100);
                                                    //await udpAssets.SendAsync(messageAssetBytes, ConnectedShopServerAssets);
                                                    continue;
                                                }

                                                // If this is the expected packet, extract the image data and write it to disk
                                                int dataOffset = sizeof(int);
                                                int dataLength = packetData.Length - dataOffset;
                                                byteSize += dataLength;
                                                if (imageData == null)
                                                {
                                                    imageData = new byte[dataLength];
                                                    NFTLogUtility.Log($"Image Data file sequence being populated...", "DSTClient.GetListingAssetThumbnails()-S");
                                                }
                                                else
                                                {
                                                    //NFTLogUtility.Log($"Image Data file sequence resized", "DSTClient.GetListingAssetThumbnails()-R");
                                                    Array.Resize(ref imageData, imageData.Length + dataLength);
                                                }
                                                Array.Copy(packetData, dataOffset, imageData, imageData.Length - dataLength, dataLength);

                                                // Send an acknowledgement packet
                                                //var ackNumber = BitConverter.GetBytes(sequenceNumber);
                                                //await udpAssets.SendAsync(ackNumber, ackNumber.Length, ConnectedShopServer);

                                                if (isLastPacket)
                                                {
                                                    NFTLogUtility.Log($"Last Packet Detected. Saving File...", "DSTClient.GetListingAssetThumbnails()-4");
                                                    // If this is the last   packet, save the image to disk and exit the loop
                                                    await fileStream.WriteAsync(imageData, 0, imageData.Length);
                                                    stopAssetBuild = true;
                                                    expectedSequenceNumber = 0;
                                                    imageData = null;
                                                    assetSuccessCount += 1;
                                                    try
                                                    {
                                                        var txtPath = NFTAssetFileUtility.CreateNFTAssetPath(_asset.Replace("jpg", "txt"), scUID, true);
                                                        File.Create(txtPath);
                                                    }
                                                    catch(Exception ex) { }
                                                    break;
                                                }

                                                expectedSequenceNumber += 1;
                                            }
                                            catch (Exception ex)
                                            {
                                                timeouts += 1;
                                                NFTLogUtility.Log($"Error: {ex.ToString()}", "DSTClient.GetListingAssetThumbnails()-ERROR0");
                                                Globals.AssetDownloadLock = false;
                                                if (timeouts > 5)
                                                {
                                                    stopAssetBuild = true;
                                                    var pathToDelete = NFTAssetFileUtility.CreateNFTAssetPath(_asset, scUID, true);
                                                    if (File.Exists(pathToDelete))
                                                    {
                                                        expectedSequenceNumber = 0;
                                                        imageData = null;
                                                        fileStream.Dispose();
                                                        File.Delete(pathToDelete);
                                                    }
                                                }

                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    var _asset = asset;
                                    if (!asset.EndsWith(".jpg"))
                                    {
                                        var assetArray = asset.Split('.');
                                        var extIndex = assetArray.Length - 1;
                                        var extToReplace = assetArray[extIndex];
                                        _asset = asset.Replace(extToReplace, "jpg");
                                    }
                                    var path = NFTAssetFileUtility.CreateNFTAssetPath(_asset, scUID, true);
                                    if (File.Exists(path))
                                    {
                                        File.Delete(path);
                                    }
                                }
                            }

                            if (assetSuccessCount == assetCount)
                            {
                                if (AssetDownloadQueue.TryGetValue(scUID, out var value))
                                {
                                    AssetDownloadQueue[scUID] = true;
                                }
                                else
                                {
                                    AssetDownloadQueue.TryAdd(scUID, true);
                                }
                            }

                            stopProcess = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        NFTLogUtility.Log($"Unknown Error: {ex.ToString()}", "DSTClient.GetListingAssetThumbnails()-ERROR1");
                        Globals.AssetDownloadLock = false;
                    }
                }

                Globals.AssetDownloadLock = false;

                NFTLogUtility.Log($"Asset method unlocked", "DSTClient.GetListingAssetThumbnails()-7");
                Globals.AssetDownloadLock = false;
            }
            finally
            {
                AssetDownloadLock.Release();
            }

        }

        public static async Task GetShopData(string connectingAddress, bool skip = false)
        {
            bool infoFound = false;
            int failCounter = 0;

            await Task.Delay(1000); //delay needed for UDP client on other end to catch up to request.

            var connectedShop = Globals.ConnectedClients.Where(x => x.Value.IsConnected).Take(1);
            if (connectedShop.Count() > 0)
            {
                Message message = new Message
                {
                    Address = connectingAddress,
                    Data = $"{DecShopRequestOptions.Info}",
                    Type = MessageType.DecShop,
                    ComType = MessageComType.Request
                };

                while(!infoFound && failCounter < 3)
                {
                    await SendShopMessageFromClient(message, true);
                    if (Globals.DecShopData?.DecShop != null)
                        infoFound = true;

                    if(!infoFound)
                        failCounter += 1;

                    await Task.Delay(500);
                }

                if (Globals.DecShopData?.DecShop != null && !skip)
                {
                    //begin data grab

                    //Collections
                    _ = GetShopCollections(connectingAddress);
                    //Listings
                    await Task.Delay(200);
                    _ = GetShopListings(connectingAddress);
                    //Auctions
                    await Task.Delay(200);
                    _ = GetShopAuctions(connectingAddress);
                    //Assets
                }
            }
        }

        public static async Task GetShopCollections(string connectionAddress, bool needsUpdate = false)
        {
            bool collectionsFound = false;
            int failCounter = 0;

            var connectedShop = Globals.ConnectedClients.Where(x => x.Value.IsConnected).Take(1);
            if (connectedShop.Count() > 0)
            {
                Message message = new Message
                {
                    Address = connectionAddress,
                    Data = $"{DecShopRequestOptions.Collections}",
                    Type = MessageType.DecShop,
                    ComType = MessageComType.Request
                };

                await SendShopMessageFromClient(message, true);

                await Task.Delay(200);

                while(!collectionsFound && failCounter < 3)
                {
                    if(Globals.DecShopData?.Collections != null)
                        collectionsFound= true;

                    if(!collectionsFound)
                    {
                        failCounter+= 1;
                        await SendShopMessageFromClient(message, true);
                    }
                    //else
                    //{
                    //    if(Globals.DecShopData?.Collections.Count == Globals.DecShopData?.DecShop.CollectionCount)
                    //    {
                    //        Console.WriteLine("TESTING");
                    //    }
                    //}
                }

            }

            NewCollectionsFound = false;
        }

        public static async Task GetShopListings(string connectionAddress, bool needsUpdate = false)
        {
            var connectedShop = Globals.ConnectedClients.Where(x => x.Value.IsConnected).Take(1);
            if (connectedShop.Count() > 0)
            {
                var listingCount = Globals.DecShopData?.DecShop?.ListingCount;

                if (listingCount == null && !needsUpdate)
                    return;

                if (listingCount == 0 && !needsUpdate)
                    return;

                var shopPageCount = (listingCount / 10) + (listingCount % 10 != 0 ? 1 : 0);

                for (int i = 0; i < shopPageCount; i++)
                {
                    bool listingsFound = false;
                    int failCounter = 0;

                    //iterate of amount of pages to get ALL live  listings
                    Message message = new Message
                    {
                        Address = connectionAddress,
                        Data = $"{DecShopRequestOptions.Listings},{i}",
                        Type = MessageType.DecShop,
                        ComType = MessageComType.Request
                    };

                    await SendShopMessageFromClient(message, true);

                    await Task.Delay(200);

                    while (!listingsFound && failCounter < 3)
                    {
                        if (Globals.DecShopData?.Listings != null)
                        {
                            if (Globals.DecShopData?.Listings?.Count > 0 && Globals.DecShopData?.Listings?.Count <= (i + 1) * 10)
                            {
                                //good
                                listingsFound = true;
                            }
                            else
                            {
                                failCounter += 1;
                                await SendShopMessageFromClient(message, true);
                            }
                        }
                        else
                        {
                            failCounter += 1;
                            await SendShopMessageFromClient(message, true);
                        }
                    }
                }

            }

            NewListingsFound = false;
        }
        public static async Task GetShopAuctions(string connectionAddress, bool needsUpdate = false)
        {
            var connectedShop = Globals.ConnectedClients.Where(x => x.Value.IsConnected).Take(1);
            if (connectedShop.Count() > 0)
            {
                var auctionCount = Globals.DecShopData?.DecShop?.AuctionCount;

                if (auctionCount == null && !needsUpdate)
                    return;

                if (auctionCount == 0 && !needsUpdate)
                    return;

                var shopPageCount = (auctionCount / 10) + (auctionCount % 10 != 0 ? 1 : 0);

                for (int i = 0; i < shopPageCount; i++)
                {
                    bool auctionFound = false;
                    int failCounter = 0;

                    //iterate of amount of pages to get ALL live  listings
                    Message message = new Message
                    {
                        Address = connectionAddress,
                        Data = $"{DecShopRequestOptions.Auctions},{i}",
                        Type = MessageType.DecShop,
                        ComType = MessageComType.Request
                    };

                    await SendShopMessageFromClient(message, true);

                    await Task.Delay(200);

                    while (!auctionFound && failCounter < 3)
                    {
                        if (Globals.DecShopData?.Auctions != null)
                        {
                            if (Globals.DecShopData?.Auctions?.Count > 0 && Globals.DecShopData?.Auctions?.Count <= (i + 1) * 10)
                            {
                                //good
                                auctionFound = true;
                            }
                            else
                            {
                                failCounter += 1;
                                await SendShopMessageFromClient(message, true);
                            }
                        }
                        else
                        {
                            failCounter += 1;
                            await SendShopMessageFromClient(message, true);
                        }
                    }
                }

            }

            NewAuctionsFound = false;
        }

        public static async Task GetShopDataLoop(CancellationToken token, string address)
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

                    var connectedShop = Globals.ConnectedClients.Where(x => x.Value.IsConnected).Take(1);
                    if (connectedShop.Count() > 0)
                    {
                        if (Globals.DecShopData?.DecShop != null)
                        {
                            //begin data grab
                            if (NewAuctionsFound || NewListingsFound || NewAuctionsFound)
                                await GetShopData(address, true);

                            //Collections
                            if (NewCollectionsFound)
                                await GetShopCollections(address);
                            //Listings
                            if(NewListingsFound)
                                await GetShopListings(address, true);
                            //Auctions
                            if(NewAuctionsFound)
                                await GetShopAuctions(address, true);
                        }
                    }

                    await Task.Delay(5000);
                }
                catch { await Task.Delay(30000); }
            }
        }

        public static async Task GetShopListingAssets(CancellationToken token, string address)
        {
            AssetDownloadQueue = new ConcurrentDictionary<string, bool>(); //reset queue assuming a new connection has been made.
            var exit = false;
            var delay = Task.Delay(3000);
            var AssetList = new ConcurrentDictionary<string, List<string>?>();

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
                    var connectedShop = Globals.ConnectedClients.Where(x => x.Value.IsConnected).Take(1);
                    if (connectedShop.Count() > 0)
                    {
                        //begin asset grab loop
                        
                        var listingCount = Globals.DecShopData?.DecShop?.ListingCount;

                        if (listingCount == null)
                        {
                            await Task.Delay(3000);
                            continue;
                        }
                           
                        if (listingCount == 0)
                        {
                            {
                                await Task.Delay(3000);
                                continue;
                            }
                        }

                        if (Globals.DecShopData?.Listings?.Count > 0)
                        {
                            var listings = Globals.DecShopData?.Listings;
                            if(listings != null)
                            {
                                var _assetConnectionId = RandomStringUtility.GetRandomStringOnlyLetters(10, true);

                                foreach (var listing in listings)
                                {
                                    if (!AssetDownloadQueue.TryGetValue(listing.SmartContractUID, out var value))
                                    {
                                        Message message = new Message
                                        {
                                            Address = address,
                                            Data = listing.SmartContractUID,
                                            Type = MessageType.AssetReq,
                                            ComType = MessageComType.Info
                                        };

                                        if (!Globals.AssetDownloadLock)
                                        {
                                            Globals.AssetDownloadLock = true;
                                            //NFTLogUtility.Log($"Asset download unlocked for: {listing.SmartContractUID}", "DSTV1Controller.GetNFTAssets()");
                                            if (_assetConnectionId != AssetConnectionId)
                                            {
                                                AssetConnectionId = _assetConnectionId;
                                                await DisconnectFromAsset();
                                                var connected = await ConnectToShopForAssets();
                                                if (connected)
                                                    await GetListingAssetThumbnails(message, listing.SmartContractUID);
                                            }
                                            else
                                            {
                                                await GetListingAssetThumbnails(message, listing.SmartContractUID);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if(!value)
                                        {
                                            Message message = new Message
                                            {
                                                Address = address,
                                                Data = listing.SmartContractUID,
                                                Type = MessageType.AssetReq,
                                                ComType = MessageComType.Info
                                            };

                                            if (!Globals.AssetDownloadLock)
                                            {
                                                Globals.AssetDownloadLock = true;
                                                //NFTLogUtility.Log($"Asset download unlocked for: {listing.SmartContractUID}", "DSTV1Controller.GetNFTAssets()");
                                                if (_assetConnectionId != AssetConnectionId)
                                                {
                                                    AssetConnectionId = _assetConnectionId;
                                                    await DisconnectFromAsset();
                                                    var connected = await ConnectToShopForAssets();
                                                    if (connected)
                                                        await GetListingAssetThumbnails(message, listing.SmartContractUID);
                                                }
                                                else
                                                {
                                                    await GetListingAssetThumbnails(message, listing.SmartContractUID);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            var scUID = listing.SmartContractUID;
                                            if (scUID != null)
                                            {
                                                var assetList = new List<string>();
                                                if (AssetList.TryGetValue(scUID, out var assetListMem))
                                                {
                                                    assetList = assetListMem;
                                                }
                                                else
                                                {
                                                    var scStateTrei = SmartContractStateTrei.GetSmartContractState(scUID);
                                                    if (scStateTrei != null)
                                                    {
                                                        var sc = SmartContractMain.GenerateSmartContractInMemory(scStateTrei.ContractData);
                                                        if (sc != null)
                                                        {
                                                            assetList = await NFTAssetFileUtility.GetAssetListFromSmartContract(sc);

                                                            AssetList.TryAdd(scUID, assetList);
                                                        }
                                                    }
                                                }

                                                if(assetList?.Count() > 0)
                                                {
                                                    foreach(var asset in assetList)
                                                    {
                                                        var _asset = asset;
                                                        if (!asset.EndsWith(".jpg"))
                                                        {
                                                            var assetArray = asset.Split('.');
                                                            var extIndex = assetArray.Length - 1;
                                                            var extToReplace = assetArray[extIndex];
                                                            if (!Globals.ValidExtensions.Contains(extToReplace))
                                                            {
                                                                //skip as its not a valid extension type.
                                                                continue;
                                                            }
                                                            _asset = asset.Replace(extToReplace, "jpg");
                                                        }

                                                        var assetPath = NFTAssetFileUtility.CreateNFTAssetPath(_asset, scUID, true);
                                                        if(!File.Exists(assetPath))
                                                        {
                                                            Message message = new Message
                                                            {
                                                                Address = address,
                                                                Data = listing.SmartContractUID,
                                                                Type = MessageType.AssetReq,
                                                                ComType = MessageComType.Info
                                                            };
                                                            if (!Globals.AssetDownloadLock)
                                                            {
                                                                Globals.AssetDownloadLock = true;
                                                                if (_assetConnectionId != AssetConnectionId)
                                                                {
                                                                    AssetConnectionId = _assetConnectionId;
                                                                    await DisconnectFromAsset();
                                                                    var connected = await ConnectToShopForAssets();
                                                                    if (connected)
                                                                        await GetListingAssetThumbnails(message, listing.SmartContractUID);
                                                                }
                                                                else
                                                                {
                                                                    await GetListingAssetThumbnails(message, listing.SmartContractUID);
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                                    
                                                
                                            }
                                            //var files = NFTAssetFileUtility.GetAssetListFromSmartContract
                                        }
                                    }
                                    
                                }

                                Globals.AssetDownloadLock = false;
                            }
                        }
                    }

                    await Task.Delay(3000);
                }
                catch
                {

                }
            }
        }

        public static async Task<bool> PingConnection(string pingId)
        {
            var connectedShop = Globals.ConnectedClients.Where(x => x.Value.IsConnected).Take(1);
            if (connectedShop.Count() > 0)
            {
                Message message = new Message
                {
                    Data = pingId,
                    Type = MessageType.Ping,
                    ComType = MessageComType.Request
                };

                Globals.PingResultDict.TryAdd(pingId, (false, 0));

                _ = SendShopMessageFromClient(message, true);

                return true;
            }
            else
            {
                //return bad
                return false;
            }
        }

        public static async Task SendShopMessageFromClient(Message message, bool responseRequested, bool sendTwice = false, int delay = 0)
        {
            var shopMessage = MessageService.GenerateMessage(message, responseRequested);
            var messageBytes = Encoding.UTF8.GetBytes(shopMessage);

            _ = udpClient.SendAsync(messageBytes, ConnectedShopServer);

            if(sendTwice)
            {
                await Task.Delay(delay);
                _ = udpClient.SendAsync(messageBytes, ConnectedShopServer);
            }
        }

        public static async Task SendClientMessageFromShop(Message message, IPEndPoint endPoint, bool responseRequested)
        {
            var shopMessage = MessageService.GenerateMessage(message, responseRequested);
            var messageBytes = Encoding.UTF8.GetBytes(shopMessage);

            _ = udpShop.SendAsync(messageBytes, endPoint);
        }

        public static async Task<byte[]> GenerateAssetAckMessage(string uniqueId, string asset, string scUID, int ackNum)
        {
            var assetDataPayload = $"{uniqueId},{asset},{scUID},{ackNum}";
            Message assetMessage = new Message
            {
                Data = assetDataPayload,
                Type = MessageType.AssetReq,
                ComType = MessageComType.Request
            };

            var shopAssetMessage = MessageService.GenerateMessage(assetMessage, false);
            var messageAssetBytes = Encoding.UTF8.GetBytes(shopAssetMessage);

            return messageAssetBytes;
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
