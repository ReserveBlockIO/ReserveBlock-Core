using ReserveBlockCore.Extensions;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using ReserveBlockCore.Beacon;
using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.Models;
using ReserveBlockCore.Nodes;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using System.Xml.Linq;
using System.Net;

namespace ReserveBlockCore.P2P
{
    public class P2PValidatorClient : P2PClient
    {
        #region HubConnection Variables        

        private static long _MaxHeight = -1;

        #endregion

        #region Remove Node from Validator Nodes
        public static async Task RemoveNode(NodeInfo node)
        {
            if(!string.IsNullOrEmpty(Globals.ValidatorAddress) && Globals.ValidatorNodes.TryRemove(node.NodeIP, out _) && node.Connection != null)
                await node.Connection.DisposeAsync();            
        }

        #endregion

        #region Check which HubConnections are actively connected

        public static async Task<bool> ArePeersConnected()
        {
            await DropDisconnectedPeers();
            return Globals.Nodes.Any();
        }
        public static async Task DropDisconnectedPeers()
        {
            foreach (var node in Globals.ValidatorNodes.Values)
            {
                if(!node.IsConnected)                
                    await RemoveNode(node);
            }
        }
        public static string MostLikelyIP()
        {
            return Globals.ReportedIPs.Count != 0 ?
                Globals.ReportedIPs.OrderByDescending(y => y.Value).Select(y => y.Key).First() : "NA";
        }

        //This will need work
        public static async Task DropLowBandwidthPeers()
        {
            if (Globals.AdjudicateAccount != null)
                return;

            await DropDisconnectedPeers();

            var PeersWithSamples = Globals.ValidatorNodes.Where(x => x.Value.SendingBlockTime > 60000)
                .Select(x => new
                {
                    Node = x.Value,
                    BandWidth = x.Value.TotalDataSent / ((double)x.Value.SendingBlockTime)
                })
                .OrderBy(x => x.BandWidth)
                .ToArray();

            var Length = PeersWithSamples.Length;
            if (Length < 3)
                return;

            var MedianBandWidth = Length % 2 == 0 ? .5 * (PeersWithSamples[Length / 2 - 1].BandWidth + PeersWithSamples[Length / 2].BandWidth) :
                PeersWithSamples[Length / 2 - 1].BandWidth;

            foreach (var peer in PeersWithSamples.Where(x => x.BandWidth < .5 * MedianBandWidth))
                await RemoveNode(peer.Node);                        
        }

        public static void UpdateMaxHeight(long height)
        {
            _MaxHeight = height;
        }

        public static long MaxHeight()
        {
            return Math.Max(Globals.LastBlock.Height, _MaxHeight);
        }

        #endregion

        #region Hub Dispose
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);

            Dispose(disposing: false);
            #pragma warning disable CA1816
            GC.SuppressFinalize(this);
            #pragma warning restore CA1816
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach(var node in Globals.ValidatorNodes.Values)
                    if(node.Connection != null)
                        node.Connection.DisposeAsync().ConfigureAwait(false).GetAwaiter().GetResult();                
            }
        }

        protected virtual async ValueTask DisposeAsyncCore()
        {
            foreach (var node in Globals.Nodes.Values)
                if(node.Connection != null)
                    await node.Connection.DisposeAsync();
        }

        #endregion

        #region Hubconnection Connect Methods 1-6

        private static ConcurrentDictionary<string, bool> ConnectLock = new ConcurrentDictionary<string, bool>();
        private static async Task Connect(Peers peer)
        {
            var url = "http://" + peer.PeerIP + ":" + Globals.ValPort + "/validator";
            try
            {
                if (!ConnectLock.TryAdd(url, true))
                    return;
                var hubConnection = new HubConnectionBuilder()
                       .WithUrl(url, options =>
                       {
                           //options.Headers.Add("address", address);
                           //options.Headers.Add("time", time);
                           //options.Headers.Add("uName", uName);
                           //options.Headers.Add("signature", signature);
                           options.Headers.Add("walver", Globals.CLIVersion);
                       })                       
                       .Build();

                var IPAddress = GetPathUtility.IPFromURL(url);
                hubConnection.On<string, string>("GetValMessage", async (message, data) =>
                {            
                    switch(message)
                    {
                        case "1": //connect - receives your IP back.
                            var IP = data.ToString();
                            if (Globals.ReportedIPs.TryGetValue(IP, out int Occurrences))
                                Globals.ReportedIPs[IP]++;
                            else
                                Globals.ReportedIPs[IP] = 1;
                            break;
                        case "2": 
                            break;
                        case "3":
                            break;
                        default:
                            break;
                    }
                                        
                });

                await hubConnection.StartAsync(new CancellationTokenSource(8000).Token);
                if (hubConnection.ConnectionId == null)
                {
                    Globals.SkipPeers.TryAdd(peer.PeerIP, 0);
                    peer.FailCount += 1;
                    if (peer.FailCount > 4)
                        peer.IsOutgoing = false;
                    Peers.GetAll()?.UpdateSafe(peer);
                    return;
                }
                    
                var node = new NodeInfo
                {
                    Connection = hubConnection,
                    NodeIP = IPAddress,
                    NodeHeight = 0,
                    NodeLastChecked = null,
                    NodeLatency = 0,
                    IsSendingBlock = 0,
                    SendingBlockTime = 0,
                    TotalDataSent = 0
                };
                (node.NodeHeight, node.NodeLastChecked, node.NodeLatency) = await GetNodeHeight(hubConnection);

                node.IsValidator = await GetValidatorStatus(node.Connection);
                var walletVersion = await GetWalletVersion(node.Connection);

                if (walletVersion != null)
                {
                    peer.WalletVersion = walletVersion.Substring(0,3);
                    node.WalletVersion = walletVersion.Substring(0,3);

                    Globals.Nodes.TryAdd(IPAddress, node);

                    if (Globals.Nodes.TryGetValue(IPAddress, out var currentNode))
                    {
                        currentNode.Connection = hubConnection;
                        currentNode.NodeIP = IPAddress;
                        currentNode.NodeHeight = node.NodeHeight;
                        currentNode.NodeLastChecked = node.NodeLastChecked;
                        currentNode.NodeLatency = node.NodeLatency;
                    }

                    ConsoleWriterService.OutputSameLine($"Connected to {Globals.Nodes.Count}/14");
                    peer.IsOutgoing = true;
                    peer.FailCount = 0; //peer responded. Reset fail count
                    Peers.GetAll()?.UpdateSafe(peer);
                }
                else
                {
                    peer.WalletVersion = "2.1";
                    Peers.GetAll()?.UpdateSafe(peer);
                    //not on latest version. Disconnecting
                    await node.Connection.DisposeAsync();
                }                                
            }
            catch 
            {
                Globals.SkipPeers.TryAdd(peer.PeerIP, 0);
                peer.FailCount += 1;
                if (peer.FailCount > 4)
                    peer.IsOutgoing = false;
                Peers.GetAll()?.UpdateSafe(peer);
            }
            finally
            {
                ConnectLock.TryRemove(url, out _);
            }
        }

        #endregion

        #region Connect to Validators
        public static async Task<bool> ConnectToValidators()
        {
            await NodeConnector.StartNodeConnecting(); //TODO: update this for validator peers!
            var peerDB = Peers.GetAll();

            await DropDisconnectedPeers();            

            var SkipIPs = new HashSet<string>(Globals.ValidatorNodes.Values.Select(x => x.NodeIP.Replace(":" + Globals.Port, ""))
                .Union(Globals.BannedIPs.Keys)
                .Union(Globals.SkipPeers.Keys)
                .Union(Globals.ReportedIPs.Keys));

            Random rnd = new Random();
            var newPeers = peerDB.Find(x => x.IsValidator).ToArray()
                .Where(x => !SkipIPs.Contains(x.PeerIP))
                .ToArray()
                .OrderBy(x => rnd.Next())
                .ToArray();

            var Diff = Globals.MaxValPeers - Globals.ValidatorNodes.Count;
            newPeers.Take(Diff).ToArray().ParallelLoop(peer =>
            {
                _ = Connect(peer);
            });

            return Globals.MaxPeers != 0;         
        }

        public static async Task ManualConnectToVal(Peers peer)
        {
            _ = Connect(peer);
        }

        #endregion

        #region Get Block
        public static async Task<Block> GetBlock(long height, NodeInfo node) //base example
        {
            //if (Interlocked.Exchange(ref node.IsSendingBlock, 1) != 0)
            //    return null;

            var startTime = DateTime.Now;
            long blockSize = 0;
            Block Block = null;
            try
            {
                var source = new CancellationTokenSource(10000);
                Block = await node.Connection.InvokeCoreAsync<Block>("SendBlock", args: new object?[] { height - 1 }, source.Token);
                if (Block != null)
                {
                    blockSize = Block.Size;
                    if (Block.Height == height)
                        return Block;
                }
            }
            catch { }
            finally
            {
                Interlocked.Exchange(ref node.IsSendingBlock, 0);
                if (node != null)
                {
                    node.TotalDataSent += blockSize;
                    node.SendingBlockTime += (DateTime.Now - startTime).Milliseconds;
                }
            }

            await P2PClient.RemoveNode(node);

            return null;
        }

        #endregion

        #region Get Height of Nodes for Timed Events

        public static async Task<(long, DateTime, int)> GetNodeHeight(HubConnection conn)
        {
            try
            {
                var startTimer = DateTime.UtcNow;
                long remoteNodeHeight = await conn.InvokeAsync<long>("SendBlockHeight");
                var endTimer = DateTime.UtcNow;
                var totalMS = (endTimer - startTimer).Milliseconds;

                return (remoteNodeHeight, startTimer, totalMS); 
            }
            catch { }
            return (-1, DateTime.UtcNow, 0);
        }

        public static async Task UpdateMethodCodes()
        {
            if (Globals.AdjudicateAccount == null)
                return;
            var Address = Globals.AdjudicateAccount.Address;
            var Height = -1L;
            while (true)
            {
                if (Height != Globals.LastBlock.Height)
                {
                    Height = Globals.LastBlock.Height;

                    Globals.Nodes.Values.ToArray().ParallelLoop(node =>
                    {
                        if (node.Address != Address && !UpdateMethodCodeAddresses.ContainsKey(node.NodeIP))
                        {
                            UpdateMethodCodeAddresses[node.NodeIP] = true;
                            _ = UpdateMethodCode(node);
                        }
                    });
                }

                await Task.Delay(1000);
            }
        }

        private static ConcurrentDictionary<string, bool> UpdateMethodCodeAddresses = new ConcurrentDictionary<string, bool>();

        private static async Task UpdateMethodCode(NodeInfo node)
        {
            while (true)
            {                
                try
                {
                    if (!node.IsConnected)
                    {
                        await Task.Delay(20);
                        continue;
                    }
                    
                    var Now = TimeUtil.GetMillisecondTime();
                    var Diff = 1000 - (int)(Now - node.LastMethodCodeTime);
                    if (Diff > 0)
                    {
                        await Task.Delay(Diff);
                        continue;
                    }

                    var state = ConsensusServer.GetState();
                    var Response = await node.Connection.InvokeCoreAsync<string>("RequestMethodCode",
                        new object[] { Globals.LastBlock.Height, state.MethodCode, state.Status == ConsensusStatus.Finalized },
                        new CancellationTokenSource(10000).Token);                         

                    if (Response != null)
                    {
                        var remoteMethodCode = Response.Split(':');
                        if (Now > node.LastMethodCodeTime)
                        {
                            lock (ConsensusServer.UpdateNodeLock)
                            {
                                node.LastMethodCodeTime = Now;
                                node.NodeHeight = long.Parse(remoteMethodCode[0]);
                                node.MethodCode = int.Parse(remoteMethodCode[1]);
                                node.IsFinalized = remoteMethodCode[2] == "1";
                            }

                            ConsensusServer.RemoveStaleCache(node);
                        }
                    }
                }
                catch (OperationCanceledException ex)
                { }
                catch (Exception ex)
                {
                    ErrorLogUtility.LogError(ex.ToString(), "UpdateMethodCodes inner catch");
                }

                await Task.Delay(20);
            }
        }

        public static async Task UpdateNodeHeights()
        {
            if (Globals.AdjudicateAccount == null)            
            {
                foreach (var node in Globals.Nodes.Values)
                    (node.NodeHeight, node.NodeLastChecked, node.NodeLatency) = await GetNodeHeight(node.Connection);
            }
        }

        #endregion

        #region Get Current Height of Nodes
        public static async Task<(bool, long)> GetCurrentHeight()
        {
            bool newHeightFound = false;
            long height = -1;

            while (!await ArePeersConnected())
                await Task.Delay(20);

            var myHeight = Globals.LastBlock.Height;
            await UpdateNodeHeights();

            foreach (var node in Globals.Nodes.Values)
            {
                var remoteNodeHeight = node.NodeHeight;
                if (myHeight < remoteNodeHeight)
                {
                    newHeightFound = true;
                    if (remoteNodeHeight > height)
                    {
                        height = remoteNodeHeight > height ? remoteNodeHeight : height;
                    }
                }
            }

            return (newHeightFound, height);
        }

        #endregion

        #region Send Transactions to mempool 
        public static async Task SendTXMempool(Transaction txSend)
        {
            var peersConnected = await ArePeersConnected();

            if (!peersConnected)
            {
                //Need peers
                Console.WriteLine("Failed to broadcast Transaction. No peers are connected to you.");
                LogUtility.Log("TX failed. No Peers: " + txSend.Hash, "P2PClient.SendTXMempool()");
            }
            else
            {
                var valNodes = Globals.ValidatorNodes.Values.Where(x => x.IsConnected).ToList();
                if (valNodes.Count() > 0)
                {
                    var successCount = 0;
                    foreach (var node in valNodes)
                    {
                        try
                        {
                            string message = Globals.AdjudicateAccount == null ? await node.InvokeAsync<string>("SendTxToMempool", new object?[] { txSend },
                                () => new CancellationTokenSource(3000).Token, "SendTxToMempool") : await node.Connection.InvokeCoreAsync
                                <string>("SendTxToMempool", new object?[] { txSend }, new CancellationTokenSource(3000).Token);

                            if (message == "ATMP")
                            {
                                //success
                                successCount += 1;
                            }
                            else if (message == "TFVP")
                            {
                                if(successCount == 0)
                                    Console.WriteLine("Transaction Failed Verification Process on remote node");
                            }
                            else
                            {
                                //already in mempool
                            }
                        }
                        catch (Exception ex)
                        {

                        }
                    }
                }
                else
                {
                    //need method to drop at least 2-3 peers to find validators in event client is not connected to any.
                    Console.WriteLine("You have no validator peers connected to you. Close wallet and attempt to reconnect.");
                    ErrorLogUtility.LogError("You have no validator peers connected to you. Close wallet and attempt to reconnect.", "P2PClient.SendTXMempool()");
                }
            }
        }

        #endregion

        #region Broadcast Blocks to Peers
        public static async Task BroadcastBlock(Block block)
        {
            var peersConnected = await ArePeersConnected();

            if (!peersConnected)
            {
                //Need peers
                Console.WriteLine("Failed to broadcast Transaction. No peers are connected to you.");
            }
            else
            {                
                foreach (var node in Globals.ValidatorNodes.Values)
                {
                    try
                    {                        
                        _ = node.InvokeAsync<bool>("ReceiveBlock", new object?[] { block }, () => new CancellationTokenSource(5000).Token, "ReceiveBlock");
                    }
                    catch (Exception ex)
                    {
                        //possible dead connection, or node is offline
                        Console.WriteLine("Error Sending Transaction. Please try again!");
                    }
                }
            }
        }
        #endregion

        #region Get Validator Status
        private static async Task<bool> GetValidatorStatus(HubConnection hubConnection)
        {
            var result = false;
            try
            {
                result = await hubConnection.InvokeAsync<bool>("GetValidatorStatus");
            }
            catch { }

            return result;
        }

        #endregion

        #region Get Node Wallet Version
        private static async Task<string?> GetWalletVersion(HubConnection hubConnection)
        {
            try
            {
                var result = await hubConnection.InvokeAsync<string>("GetWalletVersion");
                if (result != null)
                    return result;
            }
            catch { }

            return null;
        }

        #endregion
    }
}
