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

                //Task taskData = new Task(() => { UpdateShopData(token); }, token);
                //taskData.Start();

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
                Console.WriteLine("connected to SHOP");

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

            var exit = false;
            while (!exit && !token.IsCancellationRequested)
            {
                try
                {
                    var delay = Task.Delay(new TimeSpan(0, 0, 12));
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

                    await delay;
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

        public static async Task GetListingAssetThumbnails(Message message, string scUID)
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
                    await udpAssets.SendAsync(messageBytes, ConnectedShopServer);
                    var messageDataGram = await udpAssets.ReceiveAsync().WaitAsync(new TimeSpan(0, 0, 3)); //wait to receive list
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

                    if(assetListJson == null)
                        continue;

                    var assetList = JsonConvert.DeserializeObject<List<string>?>(assetListJson);

                    if (assetList == null)
                        continue;

                    if (assetList.Count > 0)
                    {
                        int byteSize = 0;
                        NFTLogUtility.Log("NFT asset list process acquired. Loop started.", "DSTClient.GetListingAssetThumbnails()-2");
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
                                    _asset = asset.Replace(extToReplace, "jpg");
                                }
                                //craft message to start process.
                                var uniqueId = RandomStringUtility.GetRandomStringOnlyLetters(10, false);

                                var path = NFTAssetFileUtility.CreateNFTAssetPath(_asset, scUID, true);
                                
                                if (File.Exists(path))
                                {
                                    var fileLength = File.ReadAllBytes(path).Length;
                                    {
                                        if(fileLength == 0)
                                        {
                                            File.Delete(path);
                                        }
                                        else
                                        {
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
                                    bool stopAssetBuild = false;
                                    while (!stopAssetBuild)
                                    {
                                        try
                                        {
                                            var messageAssetBytes = await GenerateAssetAckMessage(uniqueId, _asset, scUID, expectedSequenceNumber);

                                            await udpAssets.SendAsync(messageAssetBytes, ConnectedShopServerAssets);//this starts the first file download. Next receive should be the first set of bytes
                                            
                                            var response = await udpAssets.ReceiveAsync().WaitAsync(new TimeSpan(0, 0, 1));
                                            var packetData = response.Buffer;
                                            await Task.Delay(200);// adding delay to avoid massive overhead on the UDP port. 
                                            // Check if this is the last packet
                                            bool isLastPacket = packetData.Length < 8192;

                                            // Extract the sequence number from the packet
                                            int sequenceNumber = BitConverter.ToInt32(packetData, 0);

                                            if (sequenceNumber != expectedSequenceNumber)
                                            {
                                                // If not, discard the packet and request a retransmission
                                                var expSeqNum = expectedSequenceNumber == 0 ? 0 : expectedSequenceNumber;
                                                var ackPacket = BitConverter.GetBytes(expSeqNum);
                                                messageAssetBytes = await GenerateAssetAckMessage(uniqueId, _asset, scUID, expSeqNum);
                                                await udpAssets.SendAsync(messageAssetBytes, ConnectedShopServerAssets);
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
                                                // If this is the last packet, save the image to disk and exit the loop
                                                await fileStream.WriteAsync(imageData, 0, imageData.Length);
                                                stopAssetBuild = true;
                                                break;
                                            }

                                            expectedSequenceNumber++;
                                        }
                                        catch(Exception ex) 
                                        {
                                            timeouts += 1;
                                            NFTLogUtility.Log($"Error: {ex.ToString()}", "DSTClient.GetListingAssetThumbnails()-ERROR0");
                                            Globals.AssetDownloadLock = false;
                                            if (timeouts > 10)
                                                stopAssetBuild = true;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                var path = NFTAssetFileUtility.CreateNFTAssetPath(asset, scUID, true);
                                if (File.Exists(path))
                                {
                                    File.Delete(path);
                                }
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

        public static async Task SendShopMessageFromClient(Message message, bool responseRequested, bool sendTwice = false)
        {
            var shopMessage = MessageService.GenerateMessage(message, responseRequested);
            var messageBytes = Encoding.UTF8.GetBytes(shopMessage);

            _ = udpClient.SendAsync(messageBytes, ConnectedShopServer);

            if(sendTwice)
            {
                await udpClient.SendAsync(messageBytes, ConnectedShopServer);
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
