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
using static ReserveBlockCore.Models.DecShop;
using ReserveBlockCore.Models.SmartContracts;

namespace ReserveBlockCore.DST
{
    public class DSTMultiClient
    {
        static int Port = Globals.DSTClientPort;
        static int LastUsedPort = 0;
        //static UdpClient udpClient;
        static UdpClient udpAssets;
        static UdpClient udpShop;
        static IPEndPoint RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        static IPEndPoint RemoteEndPointAssets = new IPEndPoint(IPAddress.Any, 0);
        static IPEndPoint? ConnectedShopServer = null;
        static IPEndPoint? ConnectedShopServerAssets = null;
        static SemaphoreSlim AssetDownloadLock = new SemaphoreSlim(1, 1);

        public static ConcurrentDictionary<string, ShopConnection> ShopConnections = new ConcurrentDictionary<string, ShopConnection>();
        public static CancellationTokenSource shopToken = new CancellationTokenSource();
        public static CancellationTokenSource shopAssetToken = new CancellationTokenSource();
        public static CancellationTokenSource stunToken = new CancellationTokenSource();
        public static int somecount = 0;
        private static string AssetConnectionId = "";
        public static ConcurrentDictionary<string, bool> AssetDownloadQueue = new ConcurrentDictionary<string, bool>();
        
        public static bool NewCollectionsFound = false;
        public static bool NewAuctionsFound = false;
        public static bool NewListingsFound = false;

        public static async Task<(bool, ShopConnection?)> ConnectToShop(string shopAddress, string address = "na")
        {
            bool connected = false;
            var successful = Encoding.UTF8.GetBytes("echo");
            RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            ConnectedShopServer = null;
            var remoteEndPoint = RemoteEndPoint;
            var IsConnected = false;
            IPEndPoint? ConnectedStunServer = null;
            var FailedToConnect = false;

            var portNumber = PortUtility.FindOpenUDPPort(LastUsedPort); //dynamic port / Port == LastUsedPort ? LastUsedPort + 1 : Port;
            var udpClient = new UdpClient(portNumber);
            LastUsedPort = portNumber;
            var decShop = await DecShop.GetDecShopStateTreiLeafByURL(shopAddress);
            while (!IsConnected && !FailedToConnect)
            {
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

                        if(decShop.HostingType == DecShopHostingType.Network)
                            await STUN(shopServer, udpClient, decShop.STUNServerGroup);

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

                if(!ShopConnections.TryGetValue(shopAddress, out var shopConnection))
                {
                    //If not found create new record in memory.
                    var nShopCon = new ShopConnection { 
                        ConnectDate = TimeUtil.GetTime(),
                        EndPoint = ConnectedShopServer,
                        IPAddress = (decShop?.IP + ":" + decShop?.Port),
                        ShopURL = shopAddress,
                        ShopToken = new CancellationTokenSource(),
                        UdpClient = udpClient,
                        ConnectionId = RandomStringUtility.GetRandomString(12, true),
                        RBXAddress = address,
                        AttemptReconnect = true
                    };

                    shopConnection = nShopCon;

                    ShopConnections.TryAdd(shopAddress, shopConnection);    
                }
                else
                {
                    //If found old record must be stale, so send token cancel event and dispose of UDP connection
                    try
                    {
                        shopConnection.ShopToken.Cancel();
                        shopConnection.UdpClient.Dispose();
                    }
                    catch { }
                    
                    await Task.Delay(200);

                    shopConnection.KeepAliveStarted = false;
                    shopConnection.ConnectionId = RandomStringUtility.GetRandomString(12, true);
                    shopConnection.ShopToken = new CancellationTokenSource();
                    shopConnection.UdpClient = udpClient;

                    ShopConnections[shopAddress] = shopConnection;
                }

                CancellationToken token = shopConnection.ShopToken.Token;

                Task task = new Task(() => { Listen(token, shopConnection); }, token);
                task.Start();

                Task taskData = new Task(() => { UpdateShopData(token, shopConnection); }, token);
                taskData.Start();

                Task taskDataLoop = new Task(() => { GetShopDataLoop(token, address, shopConnection); }, token);
                taskDataLoop.Start();

                Task taskAssets = new Task(() => { GetShopListingAssets(token, address, shopConnection); }, token);
                taskAssets.Start();

                var kaPayload = new Message { Type = MessageType.KeepAlive, Data = "" };
                var kaMessage = GenerateMessage(kaPayload);

                var messageBytes = Encoding.UTF8.GetBytes(kaMessage);

                udpClient.Send(messageBytes, shopConnection.EndPoint);

                return (connected, shopConnection);
            }
            return (connected, null);
        }

        public static async Task<bool> ConnectToShopForAssets(ShopConnection shopConnection)
        {
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
                if (ShopConnections.TryGetValue(shopConnection.ShopURL, out _))
                {
                    var shopServer = shopConnection.IPAddress;

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
                        await SendShopMessageFromClient(punchMeMessage, false, shopConnection.UdpClient, shopEndPoint);

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

        private static async Task STUN(string shopEndPoint, UdpClient udpClient, int groupNum = 0)
        {
            var successful = Encoding.UTF8.GetBytes("ack");
            var remoteEndPoint = RemoteEndPoint;
            var IsConnected = false;
            IPEndPoint? ConnectedStunServer = null;
            var FailedToConnect = false;
            var badList = new List<StunServer>();
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

        static async Task Listen(CancellationToken token, ShopConnection shopConnection)
        {
            var counter = somecount;

            var exit = false;
            var udpClient = shopConnection.UdpClient;
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
                        _ = Task.Run(async () => await MessageService.ProcessMessage(payload, shopConnection.EndPoint, udpClient, shopConnection.ShopURL));
                    }
                }
                catch 
                {

                }
            }
        }

        public static async Task<bool> PingConnection(string shopURL, string pingId)
        {
            if(ShopConnections.TryGetValue(shopURL, out var shop))
            {
                Message message = new Message
                {
                    Data = pingId,
                    Type = MessageType.Ping,
                    ComType = MessageComType.Request
                };

                Globals.PingResultDict.TryAdd(pingId, (false, 0));

                _ = SendShopMessageFromClient(message, true, shop.UdpClient, shop.EndPoint);

                return true;
            }
            else
            {
                //return bad
                return false;
            }
        }

        static async Task UpdateShopData(CancellationToken token, ShopConnection shopConnect)
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

                    _ = SendShopMessageFromClient(message, true, shopConnect.UdpClient, shopConnect.EndPoint);

                    await Task.Delay(new TimeSpan(0, 0, 30));
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

        public static async Task<bool> DisconnectFromShop(string shopURL, bool keepShopData = false)
        {
            try
            {
                if(ShopConnections.TryGetValue(shopURL, out var shopConnection))
                {
                    shopConnection.ShopToken.Cancel();

                    if (!keepShopData)
                        Globals.DecShopData = null;
                    shopConnection.UdpClient.Close();
                    shopConnection.UdpClient.Dispose();

                    ShopConnections.TryRemove(shopURL, out _);

                    return true;
                }
                else
                {
                    return false;
                }
                
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
            await AssetDownloadLock.WaitAsync();
            try
            {
                Globals.AssetDownloadLock = true;
                var shopMessage = MessageService.GenerateMessage(message, false);
                var messageBytes = Encoding.UTF8.GetBytes(shopMessage);
                var stopProcess = false;
                int attempts = 0;
                SCLogUtility.Log("NFT asset thumbnail process start. Requesting Asset List", "DSTClient.GetListingAssetThumbnails()-1");

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
                            SCLogUtility.Log("NFT asset list process acquired. Loop started.", "DSTClient.GetListingAssetThumbnails()-2");
                            await Task.Delay(20);
                            SCLogUtility.Log($"AssetList: {assetListDelim}", "DSTClient.GetListingAssetThumbnails()-2.1");

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

                                        SCLogUtility.Log($"Path created: {path}", "DSTClient.GetListingAssetThumbnails()-3");
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
                                                                                                                                                 //await Task.Delay(5);
                                                                                                                                                 //_ = udpAssets.SendAsync(messageAssetBytes, ConnectedShopServerAssets); 

                                                var response = await udpAssets.ReceiveAsync().WaitAsync(new TimeSpan(0, 0, 5));
                                                var packetData = response.Buffer;
                                                stopWatch.Stop();
                                                latencyWait = (int)stopWatch.ElapsedMilliseconds == 0 ? 60 : (int)stopWatch.ElapsedMilliseconds;
                                                //SCLogUtility.Log($"{_asset} | Ping: {stopWatch.ElapsedMilliseconds} ms", "DSTClient.GetListingAssetThumbnails()");
                                                Console.WriteLine($"{_asset} | Ping: {stopWatch.ElapsedMilliseconds} ms");
                                                //await Task.Delay(200);// adding delay to avoid massive overhead on the UDP port. 
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
                                                //SCLogUtility.Log($"Seq: {sequenceNumber} | ExpSeq: {expectedSequenceNumber}", "DSTClient.GetListingAssetThumbnails()-S");
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
                                                    SCLogUtility.Log($"Image Data file sequence being populated...", "DSTClient.GetListingAssetThumbnails()-S");
                                                }
                                                else
                                                {
                                                    //SCLogUtility.Log($"Image Data file sequence resized", "DSTClient.GetListingAssetThumbnails()-R");
                                                    Array.Resize(ref imageData, imageData.Length + dataLength);
                                                }
                                                Array.Copy(packetData, dataOffset, imageData, imageData.Length - dataLength, dataLength);

                                                // Send an acknowledgement packet
                                                //var ackNumber = BitConverter.GetBytes(sequenceNumber);
                                                //await udpAssets.SendAsync(ackNumber, ackNumber.Length, ConnectedShopServer);

                                                if (isLastPacket)
                                                {
                                                    SCLogUtility.Log($"Last Packet Detected. Saving File...", "DSTClient.GetListingAssetThumbnails()-4");
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
                                                    catch (Exception ex) { }

                                                    break;
                                                }

                                                expectedSequenceNumber += 1;
                                            }
                                            catch (Exception ex)
                                            {
                                                timeouts += 1;
                                                SCLogUtility.Log($"Error: {ex.ToString()}", "DSTClient.GetListingAssetThumbnails()-ERROR0");
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
                        stopProcess = true;
                        SCLogUtility.Log($"Unknown Error: {ex.ToString()}", "DSTClient.GetListingAssetThumbnails()-ERROR1");
                        Globals.AssetDownloadLock = false;
                    }
                }

                Globals.AssetDownloadLock = false;

                SCLogUtility.Log($"Asset method unlocked", "DSTClient.GetListingAssetThumbnails()-7");
                Globals.AssetDownloadLock = false;
            }
            finally
            {
                AssetDownloadLock.Release();
            }
        }

        public static async Task GetShopData(string connectingAddress, ShopConnection shopConnection, bool skip = false)
        {
            bool infoFound = false;
            int failCounter = 0;

            await Task.Delay(1000); //delay needed for UDP client on other end to catch up to request.

            if (ShopConnections.TryGetValue(shopConnection.ShopURL, out var _shopConnection))
            {
                Message message = new Message
                {
                    Address = connectingAddress,
                    Data = $"{DecShopRequestOptions.Info}",
                    Type = MessageType.DecShop,
                    ComType = MessageComType.Request
                };

                Globals.MultiDecShopData.TryAdd(shopConnection.ShopURL, new DecShopData());

                while (!infoFound && failCounter < 3)
                {
                    await SendShopMessageFromClient(message, true, shopConnection.UdpClient, shopConnection.EndPoint);
                    Globals.MultiDecShopData.TryGetValue(shopConnection.ShopURL, out var decShopData);
                    if (decShopData?.DecShop != null)
                        infoFound = true;

                    if(!infoFound)
                        failCounter += 1;
                    await Task.Delay(500);
                }

                Globals.MultiDecShopData.TryGetValue(shopConnection.ShopURL, out var _decShopData);
                if (_decShopData?.DecShop != null && !skip)
                {
                    //begin data grab

                    //Collections
                    await GetShopCollections(connectingAddress, shopConnection);
                    //Listings
                    await Task.Delay(200);
                    await GetShopListings(connectingAddress, shopConnection);
                    //Auctions
                    await Task.Delay(200);
                    await GetShopAuctions(connectingAddress, shopConnection);
                    //Assets
                }
            }
        }

        public static async Task GetShopCollections(string connectionAddress, ShopConnection shopConnection)
        {
            bool collectionsFound = false;
            int failCounter = 0;

            if (ShopConnections.TryGetValue(shopConnection.ShopURL, out var _shopConnection))
            {
                Message message = new Message
                {
                    Address = connectionAddress,
                    Data = $"{DecShopRequestOptions.Collections}",
                    Type = MessageType.DecShop,
                    ComType = MessageComType.Request
                };

                Globals.MultiDecShopData.TryGetValue(shopConnection.ShopURL, out var decShopData);
                if(decShopData?.DecShop != null)
                {
                    await SendShopMessageFromClient(message, true, shopConnection.UdpClient, shopConnection.EndPoint);

                    await Task.Delay(200);

                    while (!collectionsFound && failCounter < 3)
                    {
                        Globals.MultiDecShopData.TryGetValue(shopConnection.ShopURL, out var _decShopData);
                        if (_decShopData?.Collections != null)
                            collectionsFound = true;

                        if (!collectionsFound)
                        {
                            failCounter += 1;
                            await SendShopMessageFromClient(message, true, shopConnection.UdpClient, shopConnection.EndPoint);
                        }
                    }
                }
            }

            ShopConnections[shopConnection.ShopURL].NewCollectionsFound = false;
        }

        public static async Task GetShopListings(string connectionAddress, ShopConnection shopConnection, bool needsUpdate = false)
        {
            if (ShopConnections.TryGetValue(shopConnection.ShopURL, out var _shopConnection))
            {
                if(Globals.MultiDecShopData.TryGetValue(shopConnection.ShopURL, out var decShopData))
                {

                    var listingCount = decShopData?.DecShop?.ListingCount;

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

                        await SendShopMessageFromClient(message, true, shopConnection.UdpClient, shopConnection.EndPoint);

                        //while (!listingsFound && Globals.MultiDecShopData[shopConnection.ShopURL]?.Listings?.Count != decShopData?.DecShop?.ListingCount)
                        while (!listingsFound && failCounter < 3)
                        {
                            if (Globals.MultiDecShopData[shopConnection.ShopURL]?.Listings != null)
                            {
                                if (Globals.MultiDecShopData[shopConnection.ShopURL]?.Listings?.Count > 0 && Globals.MultiDecShopData[shopConnection.ShopURL]?.Listings?.Count <= (i + 1) * 10)
                                {
                                    //good
                                    listingsFound = true;
                                    break;
                                }
                                else
                                {
                                    failCounter += 1;
                                    await SendShopMessageFromClient(message, true, shopConnection.UdpClient, shopConnection.EndPoint);
                                }
                            }
                            else
                            {
                                failCounter += 1;
                                await SendShopMessageFromClient(message, true, shopConnection.UdpClient, shopConnection.EndPoint);
                            }
                        }
                    }
                }
            }

            ShopConnections[shopConnection.ShopURL].NewListingsFound = false;
        }
        public static async Task GetShopAuctions(string connectionAddress, ShopConnection shopConnection, bool needsUpdate = false)
        {
            if (ShopConnections.TryGetValue(shopConnection.ShopURL, out _))
            {
                if (Globals.MultiDecShopData.TryGetValue(shopConnection.ShopURL, out var decShopData))
                {
                    var auctionCount = decShopData?.DecShop?.AuctionCount;

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

                        await SendShopMessageFromClient(message, true, shopConnection.UdpClient, shopConnection.EndPoint);

                        await Task.Delay(200);

                        while (!auctionFound && failCounter < 3)
                        {
                            if (Globals.MultiDecShopData[shopConnection.ShopURL]?.Auctions != null)
                            {
                                if (Globals.MultiDecShopData[shopConnection.ShopURL]?.Auctions?.Count > 0 && Globals.MultiDecShopData[shopConnection.ShopURL]?.Auctions?.Count <= (i + 1) * 10)
                                {
                                    //good
                                    auctionFound = true;
                                }
                                else
                                {
                                    failCounter += 1;
                                    await SendShopMessageFromClient(message, true, shopConnection.UdpClient, shopConnection.EndPoint);
                                }
                            }
                            else
                            {
                                failCounter += 1;
                                await SendShopMessageFromClient(message, true, shopConnection.UdpClient, shopConnection.EndPoint);
                            }
                        }
                    }
                }
            }

            ShopConnections[shopConnection.ShopURL].NewAuctionsFound = false;
        }

        public static async Task GetShopDataLoop(CancellationToken token, string address, ShopConnection shopConnection)
        {
            var exit = false;
            //wait 1 minute before starting
            await Task.Delay(60000);
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

                    if (ShopConnections.TryGetValue(shopConnection.ShopURL, out var _shopConnection))
                    {
                        if (Globals.MultiDecShopData.TryGetValue(shopConnection.ShopURL, out var decShopData))
                        {
                            //begin data grab

                            if (_shopConnection.NewAuctionsFound || _shopConnection.NewListingsFound || _shopConnection.NewAuctionsFound)
                                await GetShopData(address, shopConnection, true);

                            //Collections
                            if(_shopConnection.NewCollectionsFound)
                                await GetShopCollections(address, shopConnection);
                            //Listings
                            if(_shopConnection.NewListingsFound)
                                await GetShopListings(address, shopConnection, true);
                            //Auctions
                            if(_shopConnection.NewAuctionsFound)
                                await GetShopAuctions(address, shopConnection, true);
                        }
                    }

                    await Task.Delay(5000);
                }
                catch { await Task.Delay(30000); }
            }
        }

        public static async Task GetShopListingAssets(CancellationToken token, string address, ShopConnection shopConnection)
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

                    if (ShopConnections.TryGetValue(shopConnection.ShopURL, out _))
                    {
                        //begin asset grab loop
                        if(Globals.MultiDecShopData[shopConnection.ShopURL] != null)
                        {
                            var decShopData = Globals.MultiDecShopData[shopConnection.ShopURL];
                            var listingCount = decShopData?.DecShop?.ListingCount;

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

                            if (decShopData?.Listings?.Count > 0)
                            {
                                var listings = decShopData?.Listings;
                                if (listings != null)
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
                                                //SCLogUtility.Log($"Asset download unlocked for: {listing.SmartContractUID}", "DSTV1Controller.GetNFTAssets()");
                                                if (_assetConnectionId != AssetConnectionId)
                                                {
                                                    AssetConnectionId = _assetConnectionId;
                                                    await DisconnectFromAsset();
                                                    var connected = await ConnectToShopForAssets(shopConnection);
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
                                            if (!value)
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
                                                    //SCLogUtility.Log($"Asset download unlocked for: {listing.SmartContractUID}", "DSTV1Controller.GetNFTAssets()");
                                                    if (_assetConnectionId != AssetConnectionId)
                                                    {
                                                        AssetConnectionId = _assetConnectionId;
                                                        await DisconnectFromAsset();
                                                        var connected = await ConnectToShopForAssets(shopConnection);
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
                                                    if(AssetList.TryGetValue(scUID, out var assetListMem))
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
                                                    
                                                    if (assetList?.Count() > 0)
                                                    {
                                                        foreach (var asset in assetList)
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
                                                            if (!File.Exists(assetPath))
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
                                                                        var connected = await ConnectToShopForAssets(shopConnection);
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
                                            }
                                        }

                                    }

                                    Globals.AssetDownloadLock = false;
                                }
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

        public static async Task SendShopMessageFromClient(Message message, bool responseRequested, UdpClient udpClient, IPEndPoint endPoint, bool sendTwice = false, int delay = 0)
        {
            var shopMessage = MessageService.GenerateMessage(message, responseRequested);
            var messageBytes = Encoding.UTF8.GetBytes(shopMessage);

            _ = udpClient.SendAsync(messageBytes, endPoint);

            if (sendTwice)
            {
                await Task.Delay(delay);
                _ = udpClient.SendAsync(messageBytes, endPoint);
            }
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
