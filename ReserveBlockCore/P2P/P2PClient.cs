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

namespace ReserveBlockCore.P2P
{
    public class P2PClient : IAsyncDisposable, IDisposable
    {
        #region HubConnection Variables        
        /// <summary>
        /// Below are reserved for adjudicators to open up communications fortis pool participation and block solving.
        /// </summary>

        private static HubConnection? hubAdjConnection1; //reserved for validators

        private static SemaphoreSlim AdjLock = new SemaphoreSlim(1, 1);
        public static bool IsAdjConnected1 => hubAdjConnection1?.State == HubConnectionState.Connected;

        private static HubConnection? hubAdjConnection2; //reserved for validators
        public static bool IsAdjConnected2 => hubAdjConnection2?.State == HubConnectionState.Connected;

        private static HubConnection? hubBeaconConnection; //reserved for beacon
        public static bool IsBeaconConnected => hubBeaconConnection?.State == HubConnectionState.Connected;

        public static async Task<T> AdjInvokeAsync<T>(string method, object[] args = null, CancellationToken ct = default)
        {
            await AdjLock.WaitAsync();
            var delay = Task.Delay(1000);
            try
            {
                return await hubAdjConnection1.InvokeCoreAsync<T>(method, args ?? Array.Empty<object>(), ct);
            }
            finally
            {
                await delay;
                if (AdjLock.CurrentCount == 0)
                    AdjLock.Release();
            }
        }

        #endregion

        #region Get Available HubConnections for Peers

        public static bool IsConnected(NodeInfo node)
        {            
            return node.Connection.State == HubConnectionState.Connected;            
        }
        private static async Task RemoveNode(NodeInfo node)
        {
            if(Globals.Nodes.TryRemove(node.NodeIP, out NodeInfo test))
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
            foreach (var node in Globals.Nodes.Values)
            {
                if(!IsConnected(node))                
                    await RemoveNode(node);
            }
        }

        public static string MostLikelyIP()
        {
            return Globals.ReportedIPs.Count != 0 ?
                Globals.ReportedIPs.OrderByDescending(y => y.Value).Select(y => y.Key).First() : "NA";
        }

        public static async Task DropLowBandwidthPeers()
        {
            await DropDisconnectedPeers();

            var PeersWithSamples = Globals.Nodes.Where(x => x.Value.SendingBlockTime > 60000)
                .Select(x => new
                {
                    IPAddress = x.Key,
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
            {
                if(Globals.Nodes.TryRemove(peer.IPAddress, out var node))
                    await node.Connection.DisposeAsync();                
            }            
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
                foreach(var node in Globals.Nodes.Values)
                    node.Connection.DisposeAsync().ConfigureAwait(false).GetAwaiter().GetResult();                
            }
        }

        protected virtual async ValueTask DisposeAsyncCore()
        {
            foreach (var node in Globals.Nodes.Values)
                await node.Connection.DisposeAsync();
        }

        #endregion

        #region Hubconnection Connect Methods 1-6
        private static async Task<bool> Connect(string url)
        {
            try
            {
                var hubConnection = new HubConnectionBuilder()
                       .WithUrl(url, options =>
                       {

                       })
                       .WithAutomaticReconnect()
                       .Build();
                
                var IPAddress = url.Replace("http://", "").Replace("/blockchain", "");
                hubConnection.On<string, string>("GetMessage", async (message, data) =>
                {                    
                    if (message == "blk" || message == "IP")
                    {
                        if (data?.Length > 1179648)
                            return;

                        if(Globals.Nodes.TryGetValue(IPAddress, out var node))
                        {
                            var now = TimeUtil.GetMillisecondTime();
                            var prevPrevTime = Interlocked.Exchange(ref node.SecondPreviousReceiveTime, node.PreviousReceiveTime);
                            if (now - prevPrevTime < 5000)
                            {
                                Peers.BanPeer(IPAddress, IPAddress + ": Sent blocks too fast to peer.", "GetMessage");                                
                                return;
                            }
                            Interlocked.Exchange(ref node.PreviousReceiveTime, now);                            
                        }
                        // if someone calls in more often than 2 times in 15 seconds ban them

                        if (message != "IP")
                        {
                            await NodeDataProcessor.ProcessData(message, data, IPAddress);
                        }
                        else
                        {
                            var IP = data.ToString();
                            if (Globals.ReportedIPs.TryGetValue(IP, out int Occurrences))
                                Globals.ReportedIPs[IP]++;
                            else
                                Globals.ReportedIPs[IP] = 1;
                        }
                    }                    
                });

                await hubConnection.StartAsync().WaitAsync(new TimeSpan(0,0,10));
                if (hubConnection.ConnectionId == null)
                    return false;


                var startTimer = DateTime.UtcNow;
                long remoteNodeHeight = await hubConnection.InvokeAsync<long>("SendBlockHeight");
                var endTimer = DateTime.UtcNow;
                var totalMS = (endTimer - startTimer).Milliseconds;

                Globals.Nodes[IPAddress] = new NodeInfo
                {
                    Connection = hubConnection,
                    NodeIP = IPAddress,
                    NodeHeight = remoteNodeHeight,
                    NodeLastChecked = startTimer,
                    NodeLatency = totalMS,
                    IsSendingBlock = 0,
                    SendingBlockTime = 0,
                    TotalDataSent = 0
                };

                return true;
            }
            catch { }

            return false;
        }

        #endregion

        #region Connect Adjudicator
        public static async Task ConnectAdjudicator(string url, string address, string uName, string signature)
        {
            try
            {
                hubAdjConnection1 = new HubConnectionBuilder()
                .WithUrl(url, options => {
                    options.Headers.Add("address", address);
                    options.Headers.Add("uName", uName);
                    options.Headers.Add("signature", signature);
                    options.Headers.Add("walver", Globals.CLIVersion);

                })
                .WithAutomaticReconnect()
                .Build();

                LogUtility.Log("Connecting to Adjudicator", "ConnectAdjudicator()");

                var ipAddress = url.Replace("http://", "").Replace("/blockchain", "");
                hubAdjConnection1.Reconnecting += (sender) =>
                {
                    LogUtility.Log("Reconnecting to Adjudicator", "ConnectAdjudicator()");
                    Console.WriteLine("[" + DateTime.Now.ToString() + "] Connection to adjudicator lost. Attempting to Reconnect.");
                    return Task.CompletedTask;
                };

                hubAdjConnection1.Reconnected += (sender) =>
                {
                    LogUtility.Log("Success! Reconnected to Adjudicator", "ConnectAdjudicator()");
                    Console.WriteLine("[" + DateTime.Now.ToString() + "] Connection to adjudicator has been restored.");
                    return Task.CompletedTask;
                };

                hubAdjConnection1.Closed += (sender) =>
                {
                    LogUtility.Log("Closed to Adjudicator", "ConnectAdjudicator()");
                    Console.WriteLine("[" + DateTime.Now.ToString() + "] Connection to adjudicator has been closed.");
                    return Task.CompletedTask;
                };

                Globals.AdjudicatorConnectDate = DateTime.UtcNow;

                hubAdjConnection1.On<string, string>("GetAdjMessage", async (message, data) => {
                    if (message == "task" || 
                    message == "taskResult" ||
                    message == "fortisPool" || 
                    message == "status" || 
                    message == "tx" || 
                    message == "badBlock" || 
                    message == "sendWinningBlock")
                    {
                        switch(message)
                        {
                            case "task":
                                await ValidatorProcessor.ProcessData(message, data, ipAddress);
                                break;
                            case "taskResult":
                                await ValidatorProcessor.ProcessData(message, data, ipAddress);
                                break;
                            case "sendWinningBlock":
                                await ValidatorProcessor.ProcessData(message, data, ipAddress);
                                break;
                            case "fortisPool":
                                if(Globals.Adjudicate == false)
                                    await ValidatorProcessor.ProcessData(message, data, ipAddress);
                                break;
                            case "status":
                                Console.WriteLine(data);
                                ValidatorLogUtility.Log("Connected to Validator Pool", "P2PClient.ConnectAdjudicator()", true);
                                LogUtility.Log("Success! Connected to Adjudicator", "ConnectAdjudicator()");
                                break;
                            case "tx":
                                await ValidatorProcessor.ProcessData(message, data, ipAddress);
                                break;
                            case "badBlock":
                                //do something
                                break;
                        }
                    }
                });

                await hubAdjConnection1.StartAsync();

            }
            catch (Exception ex)
            {
                ValidatorLogUtility.Log("Failed! Connecting to Adjudicator: Reason - " + ex.Message, "ConnectAdjudicator()");
            }
        }


        #endregion

        #region Disconnect Adjudicator
        public static async Task DisconnectAdjudicator()
        {
            try
            {
                Globals.ValidatorAddress = "";
                if (hubAdjConnection1 != null)
                {
                    if(IsAdjConnected1)
                    {
                        await hubAdjConnection1.DisposeAsync();
                        ConsoleWriterService.Output($"Success! Disconnected from Adjudicator on: {DateTime.Now}");
                        ValidatorLogUtility.Log($"Success! Disconnected from Adjudicator on: {DateTime.Now}", "DisconnectAdjudicator()");
                    }
                }
            }
            catch (Exception ex)
            {
                ValidatorLogUtility.Log("Failed! Did not disconnect from Adjudicator: Reason - " + ex.Message, "DisconnectAdjudicator()");
            }
        }


        #endregion

        #region Connect to Peers
        public static async Task<bool> ConnectToPeers()
        {
            await NodeConnector.StartNodeConnecting();
            var peerDB = Peers.GetAll();

            await DropDisconnectedPeers();
            var SkipIPs = new HashSet<string>(Globals.Nodes.Values.Select(x => x.NodeIP.Replace($":{Globals.Port}", "")))
                .Union(Globals.BannedIPs.Where(x => x.Value).Select(x => x.Key));

            Random rnd = new Random();
            var newPeers = peerDB.Find(x => x.IsOutgoing == true).ToArray()
                .Where(x => !SkipIPs.Contains(x.PeerIP))
                .ToArray()
                .OrderBy(x => rnd.Next())                
                .Concat(peerDB.Find(x => x.IsOutgoing == false).ToArray()
                .Where(x => !SkipIPs.Contains(x.PeerIP))
                .ToArray()
                .OrderBy(x => rnd.Next()))                
                .ToArray();

            var NodeCount = Globals.Nodes.Count;
            foreach(var peer in newPeers)
            {
                if (NodeCount == Globals.MaxPeers)
                    break;

                var url = "http://" + peer.PeerIP + ":" + Globals.Port + "/blockchain";
                var conResult = await Connect(url);
                if (conResult != false)
                {
                    NodeCount++;
                    ConsoleWriterService.OutputSameLine($"Connected to {NodeCount}/8");
                    peer.IsOutgoing = true;
                    peer.FailCount = 0; //peer responded. Reset fail count
                    peerDB.UpdateSafe(peer);
                }
                else
                {
                    //peer.FailCount += 1;
                    //peerDB.UpdateSafe(peer);
                }
            }
                                 
            return NodeCount != 0;
        }

        public static async Task<bool> PingBackPeer(string peerIP)
        {
            try
            {
                var url = "http://" + peerIP + ":" + Globals.Port + "/blockchain";
                var connection = new HubConnectionBuilder().WithUrl(url).Build();
                string response = "";
                await connection.StartAsync();
                response = await connection.InvokeAsync<string>("PingBackPeer");

                if (response == "HelloBackPeer")
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                //peer did not response correctly or at all
                return false;
            }

            return false;
        }



        #endregion

        #region Send Winning Task **NEW

        public static async Task SendWinningTask_New(TaskWinner taskWin)
        {
            var adjudicatorConnected = IsAdjConnected1;
   
            if (adjudicatorConnected)
            {
                for (var i = 1; i < 4; i++)
                {
                    if (i > 1)
                    {
                        await Task.Delay(500); // if failed after first attempt waits 0.5 seconds then tries again.
                    }

                    try
                    {
                        if (taskWin != null)
                        {
                            if (taskWin.WinningBlock.Height == Globals.LastBlock.Height + 1)
                            {
                                if (hubAdjConnection1 != null)
                                {

                                    var result = await AdjInvokeAsync<bool>("ReceiveWinningTaskBlock", new object?[] { taskWin });
                                    if (result)
                                    {
                                        Globals.LastWinningTaskError = false;
                                        Globals.LastWinningTaskSentTime = DateTime.Now;
                                        Globals.LastWinningTaskBlockHeight = taskWin.WinningBlock.Height;
                                        break;
                                    }
                                    else
                                    {
                                        Globals.LastWinningTaskError = true;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Globals.LastTaskError = true;

                        ValidatorLogUtility.Log("Unhandled Error Sending Task. Check Error Log for more details.", "P2PClient.SendTaskAnswer()");

                        string errorMsg = string.Format("Error Sending Task - {0}. Error Message : {1}", taskWin != null ?
                            DateTime.Now.ToString() : "No Time", ex.Message);
                        ErrorLogUtility.LogError(errorMsg, "SendTaskAnswer(TaskAnswer taskAnswer)");
                    }
                }

                if (Globals.LastTaskError == true)
                {
                    ValidatorLogUtility.Log("Failed to send or receive back from Adjudicator 4 times. Please verify node integrity and crafted blocks.", "P2PClient.SendTaskAnswer()");
                }

            }
        }

        #endregion

        #region Send Task Answer **NEW
        public static async Task SendTaskAnswer_New(TaskNumberAnswer taskAnswer)
        {
            var adjudicatorConnected = IsAdjConnected1;
            Random rand = new Random();
            int randNum = (rand.Next(1, 7) * 1000);

            if (adjudicatorConnected)
            {
                for (var i = 1; i < 4; i++)
                {
                    if (i != 1)
                    {
                        await Task.Delay(1000); // if failed on first attempt waits 1 seconds then tries again.
                    }
                    else
                    {
                        await Task.Delay(randNum);//wait random amount between 1-7 to not overload network all at once.
                    }
                    try
                    {
                        if (taskAnswer != null)
                        {
                            if (taskAnswer.NextBlockHeight == Globals.LastBlock.Height + 1)
                            {
                                if (hubAdjConnection1 != null)
                                {

                                    var result = await AdjInvokeAsync<bool>("ReceiveTaskAnswer_New", args: new object?[] { taskAnswer });
                                    if (result)
                                    {
                                        Globals.LastTaskError = false;
                                        Globals.LastTaskSentTime = DateTime.Now;
                                        Globals.LastSentBlockHeight = taskAnswer.NextBlockHeight;
                                        break;
                                    }
                                    else
                                    {
                                        Globals.LastTaskError = true;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Globals.LastTaskError = true;

                        ValidatorLogUtility.Log("Unhandled Error Sending Task. Check Error Log for more details.", "P2PClient.SendTaskAnswer()");

                        string errorMsg = string.Format("Error Sending Task - {0}. Error Message : {1}", taskAnswer != null ?
                            taskAnswer.SubmitTime.ToString() : "No Time", ex.Message);
                        ErrorLogUtility.LogError(errorMsg, "SendTaskAnswer(TaskAnswer taskAnswer)");
                    }
                }

                if (Globals.LastTaskError == true)
                {
                    ValidatorLogUtility.Log("Failed to send or receive back from Adjudicator 4 times. Please verify node integrity and crafted blocks.", "P2PClient.SendTaskAnswer()");
                }

            }
        }

        #endregion

        #region Send Task Answer **Deprecated
        public static async Task SendTaskAnswer_Deprecated(TaskAnswer taskAnswer)
        {
            var adjudicatorConnected = IsAdjConnected1;
            Random rand = new Random();
            int randNum = (rand.Next(1, 7) * 1000);

            if(adjudicatorConnected)
            {
                for(var i = 1; i < 4; i++)
                {
                    if(i != 1)
                    {
                        await Task.Delay(1000); // if failed on first attempt waits 1 seconds then tries again.
                    }
                    else
                    {
                        await Task.Delay(randNum);//wait random amount between 1-7 to not overload network all at once.
                    }
                    try
                    {
                        if(taskAnswer != null)
                        {
                            if (taskAnswer.Block.Height == Globals.LastBlock.Height + 1)
                            {
                                if (hubAdjConnection1 != null)
                                {
                                    
                                    var result = await AdjInvokeAsync<bool>("ReceiveTaskAnswer", args: new object?[] { taskAnswer });
                                    if (result)
                                    {
                                        Globals.LastTaskError = false;
                                        Globals.LastTaskSentTime = DateTime.Now;
                                        Globals.LastSentBlockHeight = taskAnswer.Block.Height;
                                        break;
                                    }
                                    else
                                    {
                                        Globals.LastTaskError = true;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Globals.LastTaskError = true;

                        ValidatorLogUtility.Log("Unhandled Error Sending Task. Check Error Log for more details.", "P2PClient.SendTaskAnswer_Deprecated()");

                        string errorMsg = string.Format("Error Sending Task - {0}. Error Message : {1}", taskAnswer != null ?
                            taskAnswer.SubmitTime.ToString() : "No Time", ex.Message);
                        ErrorLogUtility.LogError(errorMsg, "SendTaskAnswer_Deprecated(TaskAnswer taskAnswer)");
                    }
                }

                if(Globals.LastTaskError == true)
                {
                    ValidatorLogUtility.Log("Failed to send or receive back from Adjudicator 4 times. Please verify node integrity and crafted blocks.", "P2PClient.SendTaskAnswer_Deprecated()");
                }

            }
        }

        #endregion

        #region Send TX To Adjudicators
        public static async Task SendTXToAdjudicator(Transaction tx)
        {
            var adjudicatorConnected = IsAdjConnected1;
            if (adjudicatorConnected == true && hubAdjConnection1 != null)
            {
                try
                {
                    var result = await AdjInvokeAsync<bool>("ReceiveTX", args: new object?[] { tx });
                }
                catch (Exception ex)
                {

                }
            }
            else
            {
                //temporary connection to an adj to send transaction to get broadcasted to global pool
                SendTXToAdj(tx);
            }

            var adjudicator2Connected = IsAdjConnected2;
            if (adjudicator2Connected == true && hubAdjConnection2 != null)
            {
                try
                {
                    var result = await hubAdjConnection2.InvokeCoreAsync<bool>("ReceiveTX", args: new object?[] { tx });
                }
                catch (Exception ex)
                {

                }
            }
            else
            {
                //temporary connection to an adj to send transaction to get broadcasted to global pool
                SendTXToAdj(tx);
            }
        }


        //This method will need to eventually be modified when the adj is a multi-pool and not a singular-pool
        private static async void SendTXToAdj(Transaction trx)
        {
            try
            {
                var adjudicator = Adjudicators.AdjudicatorData.GetLeadAdjudicator();
                if (adjudicator != null)
                {
                    var url = "http://" + adjudicator.NodeIP + ":" + Globals.Port + "/adjudicator";
                    var _tempHubConnection = new HubConnectionBuilder().WithUrl(url).Build();
                    var alive = _tempHubConnection.StartAsync();
                    var response = await _tempHubConnection.InvokeCoreAsync<bool>("ReceiveTX", args: new object?[] { trx });
                    if(response != true)
                    {
                        var errorMsg = string.Format("Failed to send TX to Adjudicator.");
                        ErrorLogUtility.LogError(errorMsg, "P2PClient.SendTXToAdj(Transaction trx) - try");
                        try { await _tempHubConnection.StopAsync(); }
                        finally
                        {
                            await _tempHubConnection.DisposeAsync();
                        }
                    }
                    else
                    {
                        try { await _tempHubConnection.StopAsync(); }
                        finally
                        {
                            await _tempHubConnection.DisposeAsync();
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                var errorMsg = string.Format("Failed to send TX to Adjudicator. Error Message : {0}", ex.Message);
                ErrorLogUtility.LogError(errorMsg, "P2PClient.SendTXToAdj(Transaction trx) - catch");
            }
        }

        #endregion

        #region Get Block

        public static async Task<Block> GetBlock(long height, NodeInfo node) //base example
        {
            if (Interlocked.Exchange(ref node.IsSendingBlock, 1) != 0)
                return null;
            var startTime = DateTime.Now;
            long blockSize = 0;
            Block Block = null;
            try
            {
                var source = new CancellationTokenSource(30000);
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

            return null;
        }

        #endregion

        #region Get Height of Nodes for Timed Events

        public static async Task<(long, DateTime, int)> GetNodeHeight(NodeInfo node)
        {
            try
            {
                var startTimer = DateTime.UtcNow;
                long remoteNodeHeight = await node.InvokeAsync<long>("SendBlockHeight");
                var endTimer = DateTime.UtcNow;
                var totalMS = (endTimer - startTimer).Milliseconds;

                return (remoteNodeHeight, startTimer, totalMS); ;
            }
            catch { }
            return default;
        }
        public static async Task UpdateNodeHeights()
        {
            foreach (var node in Globals.Nodes.Values)                
                (node.NodeHeight, node.NodeLastChecked, node.NodeLatency) = await GetNodeHeight(node);           
        }

        #endregion

        #region Get Current Height of Nodes
        public static async Task<(bool, long)> GetCurrentHeight()
        {
            bool newHeightFound = false;
            long height = 0;

            var peersConnected = await P2PClient.ArePeersConnected();

            if (!peersConnected)
            {                
                return (newHeightFound, height);
            }
            else
            {
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
            }
            return (newHeightFound, height);
        }

        #endregion

        #region Connect Beacon
        public static async Task ConnectBeacon(string url, string uplReq = "n", string dwnlReq = "n")
        {
            try
            {
                var beaconRef = Globals.BeaconReference.Reference;
                if(beaconRef == null)
                {
                    throw new HubException("Cannot connect without a Beacon Reference");
                }

                hubBeaconConnection = new HubConnectionBuilder()
                .WithUrl(url, options => {
                    options.Headers.Add("beaconRef", beaconRef);
                    options.Headers.Add("walver", Globals.CLIVersion);
                    options.Headers.Add("uplReq", uplReq);
                    options.Headers.Add("dwnlReq", dwnlReq);
                })
                .WithAutomaticReconnect()
                .Build();

                LogUtility.Log("Connecting to Beacon", "ConnectBeacon()");

                var ipAddress = url.Replace("http://", "").Replace("/beacon", "");
                hubBeaconConnection.Reconnecting += (sender) =>
                {
                    LogUtility.Log("Reconnecting to Beacon", "ConnectBeacon()");
                    Console.WriteLine("[" + DateTime.Now.ToString() + "] Connection to Beacon lost. Attempting to Reconnect.");
                    return Task.CompletedTask;
                };

                hubBeaconConnection.Reconnected += (sender) =>
                {
                    LogUtility.Log("Success! Reconnected to Beacon", "ConnectBeacon()");
                    Console.WriteLine("[" + DateTime.Now.ToString() + "] Connection to Beacon has been restored.");
                    return Task.CompletedTask;
                };

                hubBeaconConnection.Closed += (sender) =>
                {
                    LogUtility.Log("Closed to Beacon", "ConnectBeacon()");
                    Console.WriteLine("[" + DateTime.Now.ToString() + "] Connection to Beacon has been closed.");
                    return Task.CompletedTask;
                };

                //Globals.AdjudicatorConnectDate = DateTime.UtcNow;

                hubBeaconConnection.On<string, string>("GetBeaconData", async (message, data) => {
                    if (message == "send" ||
                    message == "receive" ||
                    message == "status" ||
                    message == "disconnect")
                    {
                        switch (message)
                        {
                            case "status":
                                Console.WriteLine(data);
                                LogUtility.Log("Success! Connected to Beacon", "ConnectBeacon()");
                                break;
                            case "send":
                                await BeaconProcessor.ProcessData(message, data);
                                break;
                            case "receive":
                                await BeaconProcessor.ProcessData(message, data);
                                break;
                            case "disconnect":
                                await DisconnectBeacon();
                                break;
                        }
                    }
                });

                await hubBeaconConnection.StartAsync();

            }
            catch (Exception ex)
            {
                ValidatorLogUtility.Log("Failed! Connecting to Adjudicator: Reason - " + ex.Message, "ConnectAdjudicator()");
            }
        }


        #endregion

        #region Disconnect Beacon
        public static async Task DisconnectBeacon()
        {
            try
            {
                if (hubBeaconConnection != null)
                {
                    if (IsBeaconConnected)
                    {
                        await hubBeaconConnection.DisposeAsync();
                        ConsoleWriterService.Output($"Success! Disconnected from Beacon on: {DateTime.Now}");
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError("Failed! Did not disconnect from Beacon: Reason - " + ex.Message, "DisconnectBeacon()");
            }
        }


        #endregion

        #region File Upload To Beacon Beacon

        public static async Task<string> BeaconUploadRequest(List<string> locators, List<string> assets, string scUID, string nextOwnerAddress, string md5List, string preSigned = "NA")
        {
            var result = "Fail";
            string signature = "";
            string locatorRetString = "";
            var scState = SmartContractStateTrei.GetSmartContractState(scUID);
            var beaconRef = await BeaconReference.GetReference();

            if(beaconRef == null)
            {
                return "Fail";
            }

            if(scState == null)
            {
                return "Fail"; // SC does not exist
            }
            else
            {
                if(preSigned != "NA")
                {
                    signature = preSigned;
                }
                else
                {
                    var account = AccountData.GetSingleAccount(scState.OwnerAddress);
                    if (account != null)
                    {
                        var accPrivateKey = GetPrivateKeyUtility.GetPrivateKey(account.PrivateKey, account.Address);

                        BigInteger b1 = BigInteger.Parse(accPrivateKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
                        PrivateKey privateKey = new PrivateKey("secp256k1", b1);

                        signature = SignatureService.CreateSignature(scUID, privateKey, account.PublicKey);
                    }
                    else
                    {
                        return "Fail";
                    }
                }
                
            }

            //send file size, beacon will reply if it is ok to send.
            var bsd = new BeaconData.BeaconSendData {
                CurrentOwnerAddress = scState.OwnerAddress,
                Assets = assets,
                SmartContractUID = scUID,
                Signature = signature,
                NextAssetOwnerAddress = nextOwnerAddress,
                Reference = beaconRef,
                MD5List = md5List,
            };
            foreach(var locator in locators)
            {
                try
                {
                    var beaconString = locator.ToStringFromBase64();
                    var beacon = JsonConvert.DeserializeObject<BeaconInfo.BeaconInfoJson>(beaconString);
                    if(beacon != null)
                    {
                        var url = "http://" + beacon.IPAddress + ":" + Globals.Port + "/beacon";
                        if(!IsBeaconConnected)
                            await ConnectBeacon(url, "y");
                    }
                    
                    if(IsBeaconConnected)
                    {
                        if(hubBeaconConnection != null)
                        {                            
                            var response = await hubBeaconConnection.InvokeCoreAsync<bool>("ReceiveUploadRequest", args: new object?[] { bsd });
                            if (response != true)
                            {
                                var errorMsg = string.Format("Failed to talk to beacon.");
                                ErrorLogUtility.LogError(errorMsg, "P2PClient.BeaconUploadRequest(List<BeaconInfo.BeaconInfoJson> locators, List<string> assets, string scUID) - try");
                                try { await hubBeaconConnection.StopAsync(); }
                                finally
                                {
                                    await hubBeaconConnection.DisposeAsync();
                                }
                            }
                            //else
                            //{
                            //    NFTLogUtility.Log($"Beacon response was true.", "P2PClient.BeaconUploadRequest()");
                            //    if (locatorRetString == "")
                            //    {
                            //        foreach (var asset in bsd.Assets)
                            //        {
                            //            NFTLogUtility.Log($"Preparing file to send. Sending {asset} for smart contract {bsd.SmartContractUID}", "P2PClient.BeaconUploadRequest()");
                            //            var path = NFTAssetFileUtility.NFTAssetPath(asset, bsd.SmartContractUID);
                            //            NFTLogUtility.Log($"Path for asset {assets} : {path}", "P2PClient.BeaconUploadRequest()");
                            //            NFTLogUtility.Log($"Beacon IP {beacon.IPAddress} : Beacon Port {beacon.Port}", "P2PClient.BeaconUploadRequest()");
                            //            //BeaconResponse rsp = BeaconClient.Send(path, beacon.IPAddress, beacon.Port);
                            //            //if (rsp.Status == 1)
                            //            //{
                            //            //    //success
                            //            //    NFTLogUtility.Log($"Success sending asset: {asset}", "P2PClient.BeaconUploadRequest()");
                            //            //}
                            //            //else
                            //            //{
                            //            //    NFTLogUtility.Log($"NFT Send for assets -> {asset} <- failed.", "SCV1Controller.TransferNFT()");
                            //            //}
                            //        }

                            //        locatorRetString = locator;
                            //    }
                            //    else
                            //    {
                            //        locatorRetString = locatorRetString + "," + locator;
                            //    }

                            //}
                        }
                    }
                    else
                    {
                        //failed to connect. Cancel TX
                        return "Fail";
                    }
                    
                }
                catch (Exception ex)
                {
                    var errorMsg = string.Format("Failed to send bsd to Beacon. Error Message : {0}", ex.Message);
                    ErrorLogUtility.LogError(errorMsg, "P2PClient.BeaconUploadRequest(List<BeaconInfo.BeaconInfoJson> locators, List<string> assets, string scUID) - catch");
                }
            }
            result = locatorRetString;
            return result;
        }

        #endregion

        #region File Download from Beacon - BeaconAccessRequest

        public static async Task<bool> BeaconDownloadRequest(List<string> locators, List<string> assets, string scUID, string preSigned = "NA")
        {
            var result = false;
            string signature = "";
            string locatorRetString = "";
            var beaconRef = await BeaconReference.GetReference();

            if (beaconRef == null)
            {
                return false;
            }

            var scState = SmartContractStateTrei.GetSmartContractState(scUID);
            if (scState == null)
            {
                return false; // SC does not exist
            }
            else
            {
                if(preSigned != "NA")
                {
                    signature = preSigned;
                }
                else
                {
                    var account = AccountData.GetSingleAccount(scState.OwnerAddress);
                    if (account != null)
                    {
                        var accPrivateKey = GetPrivateKeyUtility.GetPrivateKey(account.PrivateKey, account.Address);

                        BigInteger b1 = BigInteger.Parse(accPrivateKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
                        PrivateKey privateKey = new PrivateKey("secp256k1", b1);

                        signature = SignatureService.CreateSignature(scUID, privateKey, account.PublicKey);
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            var bdd = new BeaconData.BeaconDownloadData
            {
                Assets = assets,
                SmartContractUID = scUID,
                Signature = signature,
                Reference = beaconRef
            };

            foreach (var locator in locators)
            {
                try
                {
                    var beaconString = locator.ToStringFromBase64();
                    var beacon = JsonConvert.DeserializeObject<BeaconInfo.BeaconInfoJson>(beaconString);
                    if (beacon != null)
                    {
                        var url = "http://" + beacon.IPAddress + ":" + Globals.Port + "/beacon";
                        if(!IsBeaconConnected)
                            await ConnectBeacon(url, "n", "y");
                    }

                    if (hubBeaconConnection != null)
                    {
                        var response = await hubBeaconConnection.InvokeCoreAsync<bool>("ReceiveDownloadRequest", args: new object?[] { bdd });
                        if (response != true)
                        {
                            var errorMsg = string.Format("Failed to talk to beacon.");
                            ErrorLogUtility.LogError(errorMsg, "P2PClient.BeaconDownloadRequest() - try");
                            try { await hubBeaconConnection.StopAsync(); }
                            finally
                            {
                                await hubBeaconConnection.DisposeAsync();
                            }
                        }
                        else
                        {


                            int failCount = 0;
                            foreach (var asset in bdd.Assets)
                            {
                                var path = NFTAssetFileUtility.CreateNFTAssetPath(asset, bdd.SmartContractUID);

                                //BeaconResponse rsp = BeaconClient.Receive(asset, beacon.IPAddress, beacon.Port, scUID);
                                //if (rsp.Status == 1)
                                //{
                                //    //success
                                //}
                                //else
                                //{
                                //    failCount += 1;
                                //}
                            }

                            if (failCount == 0)
                            {
                                result = true;
                                break;
                            }
                        }
                    }
                }
                catch(Exception ex)
                {
                    var errorMsg = string.Format("Failed to send bdd to Beacon. Error Message : {0}", ex.Message);
                    ErrorLogUtility.LogError(errorMsg, "P2PClient.BeaconDownloadRequest() - catch");
                }
            }

            return result;
        }

        #endregion

        #region Beacon IsReady Flag send
        public static async Task BeaconFileIsReady(string scUID, string assetName)
        {
            try
            {
                string[] payload = { scUID, assetName };
                var payloadJson = JsonConvert.SerializeObject(payload);

                var beaconString = Globals.Locators.FirstOrDefault().ToStringFromBase64();
                var beacon = JsonConvert.DeserializeObject<BeaconInfo.BeaconInfoJson>(beaconString);
                if (beacon != null)
                {
                    var url = "http://" + beacon.IPAddress + ":" + Globals.Port + "/beacon";
                    if (!IsBeaconConnected)
                        await ConnectBeacon(url, "y");

                    if (hubBeaconConnection != null)
                    {
                        var response = await hubBeaconConnection.InvokeCoreAsync<bool>("BeaconDataIsReady", args: new object?[] { payloadJson });
                        if (response != true)
                        {
                            var errorMsg = string.Format("Failed to talk to beacon.");
                            NFTLogUtility.Log(errorMsg, "P2PClient.BeaconFileIsReady() - try");
                            try { await hubBeaconConnection.StopAsync(); }
                            finally
                            {
                                await hubBeaconConnection.DisposeAsync();
                            }
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                ErrorLogUtility.LogError($"Unknown Error: {ex.Message}", "P2PClient.BeaconFileIsReady() - catch");
            }

        }

        #endregion

        #region Get Beacon Status of Nodes
        public static async Task<List<string>> GetBeacons()
        {
            List<string> BeaconList = new List<string>();

            var peersConnected = await ArePeersConnected();

            int foundBeaconCount = 0;

            if (!peersConnected)
            {
                //Need peers
                ErrorLogUtility.LogError("You are not connected to any nodes", "P2PClient.GetBeacons()");
                NFTLogUtility.Log("You are not connected to any nodes", "P2PClient.GetBeacons()");
                return BeaconList;
            }
            else
            {
                foreach(var node in Globals.Nodes.Values)
                {
                    string beaconInfo = await node.InvokeAsync<string>("SendBeaconInfo");
                    if (beaconInfo != "NA")
                    {
                        NFTLogUtility.Log("Beacon Found on hub " + node.NodeIP, "P2PClient.GetBeacons()");
                        BeaconList.Add(beaconInfo);
                        foundBeaconCount++;
                    }
                }

                if(foundBeaconCount == 0)
                {
                    NFTLogUtility.Log("Zero beacons found. Adding bootstrap.", "SCV1Controller.TransferNFT()");
                    BeaconList = Globals.Locators;
                    BeaconList.ForEach(x => { NFTLogUtility.Log($"Bootstrap Beacons {x}", "P2PClient.GetBeacons()"); });
                }

            }
            return BeaconList;
        }

        #endregion

        #region Get Lead Adjudicators
        public static async Task<Adjudicators?> GetLeadAdjudicator()
        {
            Adjudicators? LeadAdj = null;

            var peersConnected = await P2PClient.ArePeersConnected();

            if (!peersConnected)
            {
                //Need peers
                return null;
            }
            else
            {
                var myHeight = Globals.LastBlock.Height;

                foreach(var node in Globals.Nodes.Values)
                {
                    try
                    {
                        var leadAdjudictor = await node.InvokeAsync<Adjudicators?>("SendLeadAdjudicator");

                        if (leadAdjudictor != null)
                        {
                            var adjudicators = Adjudicators.AdjudicatorData.GetAll();
                            if (adjudicators != null)
                            {
                                var lAdj = adjudicators.FindOne(x => x.IsLeadAdjuidcator == true);
                                if (lAdj == null)
                                {
                                    adjudicators.InsertSafe(leadAdjudictor);
                                    LeadAdj = leadAdjudictor;
                                }
                            }
                        }                        
                    }
                    catch (Exception ex)
                    {
                        //node is offline
                    }
                }
            }
            return LeadAdj;
        }

        #endregion

        #region Send Transactions to mempool 
        public static async void SendTXMempool(Transaction txSend)
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
                foreach (var node in Globals.Nodes.Values)
                {
                    try
                    {
                        string message = await node.InvokeAsync<string>("SendTxToMempool", args: new object?[] { txSend });

                        if (message == "ATMP")
                        {
                            //success
                        }
                        else if (message == "TFVP")
                        {
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
        }

        #endregion

        #region Broadcast Blocks to Peers
        public static async Task BroadcastBlock(Block block)
        {
            var peersConnected = await P2PClient.ArePeersConnected();

            if (!peersConnected)
            {
                //Need peers
                Console.WriteLine("Failed to broadcast Transaction. No peers are connected to you.");
            }
            else
            {
                foreach(var node in Globals.Nodes.Values)
                {
                    try
                    {                        
                        await node.InvokeAsync<string>("ReceiveBlock", args: new object?[] { block });
                        
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
    }
}
