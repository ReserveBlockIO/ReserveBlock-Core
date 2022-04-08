using LiteDB;
using Microsoft.AspNetCore.SignalR.Client;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Nodes;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReserveBlockCore.P2P
{
    public class P2PClient
    {
        public static List<Peers>? ActivePeerList { get; set; }
        public static List<string> ReportedIPs = new List<string>();
        public static long LastSentBlockHeight = -1;
        public static Dictionary<int, string>? NodeDict { get; set; }

        #region HubConnection Variables
        /// <summary>
        /// Below are reserved for peers to download blocks and share mempool tx's.
        /// </summary>
        /// 
        private static Dictionary<string, bool> HubList = new Dictionary<string, bool>();

        private static HubConnection? hubConnection1;
        public static bool IsConnected1 => hubConnection1?.State == HubConnectionState.Connected;

        private static HubConnection? hubConnection2;
        public static bool IsConnected2 => hubConnection2?.State == HubConnectionState.Connected;

        private static HubConnection? hubConnection3;
        public static bool IsConnected3 => hubConnection3?.State == HubConnectionState.Connected;

        private static HubConnection? hubConnection4;
        public static bool IsConnected4 => hubConnection4?.State == HubConnectionState.Connected;

        private static HubConnection? hubConnection5;
        public static bool IsConnected5 => hubConnection5?.State == HubConnectionState.Connected;

        private static HubConnection? hubConnection6;
        public static bool IsConnected6 => hubConnection6?.State == HubConnectionState.Connected;

        /// <summary>
        /// Below are reserved for adjudicators to open up communications fortis pool participation and block solving.
        /// </summary>

        private static HubConnection? hubAdjConnection1; //reserved for validators
        public static bool IsAdjConnected1 => hubAdjConnection1?.State == HubConnectionState.Connected;

        private static HubConnection? hubAdjConnection2; //reserved for validators
        public static bool IsAdjConnected2 => hubAdjConnection2?.State == HubConnectionState.Connected;

        #endregion

        #region Get Available HubConnections for Peers
        private static async Task<int> GetAvailablePeerHubs()
        {
            if (hubConnection1 == null || !IsConnected1)
            {
                hubConnection1 = null;
                return (1);
            }
            if (hubConnection2 == null || !IsConnected2)
            {
                hubConnection2 = null;
                return (2);
            }
            if (hubConnection3 == null || !IsConnected3)
            {
                hubConnection3 = null;
                return (3);
            }
            if (hubConnection4 == null || !IsConnected4)
            {
                hubConnection4 = null;
                return (4);
            }
            if (hubConnection5 == null || !IsConnected5)
            {
                hubConnection5 = null;
                return (5);
            }
            if (hubConnection6 == null || !IsConnected6)
            {
                hubConnection6 = null;
                return (6);
            }

            return (0);
        }

        #endregion

        #region Check which HubConnections are actively connected
        public static async Task<(bool, int)> ArePeersConnected()
        {
            var nodes = Program.Nodes;
            var nodeList = nodes.ToList();
            var result = false;
            var resultCount = 0;
            if (hubConnection1 != null && IsConnected1)
            {
                result = true;
                resultCount += 1;
            }
            else
            {
                hubConnection1 = null;
                var nodeInfo = NodeDict[1];
                if (nodeInfo != null)
                {
                    var node = nodeList.Where(x => x.NodeIP == nodeInfo).FirstOrDefault();
                    if (node != null)
                    {
                        Program.Nodes.Remove(node);
                    }
                    NodeDict[1] = null;
                }
            }
            if (hubConnection2 != null && IsConnected2)
            {
                result = true;
                resultCount += 1;
            }
            else
            {
                hubConnection2 = null;
                var nodeInfo = NodeDict[2];
                if (nodeInfo != null)
                {
                    var node = nodeList.Where(x => x.NodeIP == nodeInfo).FirstOrDefault();
                    if (node != null)
                    {
                        Program.Nodes.Remove(node);
                    }
                    NodeDict[2] = null;
                }
            }
            if (hubConnection3 != null && IsConnected3)
            {
                result = true;
                resultCount += 1;
            }
            else
            {
                hubConnection3 = null;
                var nodeInfo = NodeDict[3];
                if (nodeInfo != null)
                {
                    var node = nodeList.Where(x => x.NodeIP == nodeInfo).FirstOrDefault();
                    if (node != null)
                    {
                        Program.Nodes.Remove(node);
                    }
                    NodeDict[3] = null;
                }
            }
            if (hubConnection4 != null && IsConnected4)
            {
                result = true;
                resultCount += 1;
            }
            else
            {
                hubConnection4 = null;
                var nodeInfo = NodeDict[4];
                if (nodeInfo != null)
                {
                    var node = nodeList.Where(x => x.NodeIP == nodeInfo).FirstOrDefault();
                    if (node != null)
                    {
                        Program.Nodes.Remove(node);
                    }
                    NodeDict[4] = null;
                }
            }
            if (hubConnection5 != null && IsConnected5)
            {
                result = true;
                resultCount += 1;
            }
            else
            {
                hubConnection5 = null;
                var nodeInfo = NodeDict[5];
                if (nodeInfo != null)
                {
                    var node = nodeList.Where(x => x.NodeIP == nodeInfo).FirstOrDefault();
                    if (node != null)
                    {
                        Program.Nodes.Remove(node);
                    }
                    NodeDict[5] = null;
                }
            }
            if (hubConnection6 != null && IsConnected6)
            {
                result = true;
                resultCount += 1;
            }
            else
            {
                hubConnection6 = null;
                var nodeInfo = NodeDict[6];
                if (nodeInfo != null)
                {
                    var node = nodeList.Where(x => x.NodeIP == nodeInfo).FirstOrDefault();
                    if (node != null)
                    {
                        Program.Nodes.Remove(node);
                    }
                    NodeDict[6] = null;
                }
            }

            //if(result == false)
            //{
            //    //attempt to reconnect to peers.
            //    await StartupService.StartupPeers();
            //}

            return (result, resultCount);
        }

        #endregion

        #region Hub Dispose
        public async ValueTask DisposeAsync()
        {
            if (hubConnection1 != null)
            {
                await hubConnection1.DisposeAsync();
            }
            if (hubConnection2 != null)
            {
                await hubConnection2.DisposeAsync();
            }
            if (hubConnection3 != null)
            {
                await hubConnection3.DisposeAsync();
            }
            if (hubConnection4 != null)
            {
                await hubConnection4.DisposeAsync();
            }
            if (hubConnection5 != null)
            {
                await hubConnection5.DisposeAsync();
            }
            if (hubConnection6 != null)
            {
                await hubConnection6.DisposeAsync();
            }
        }

        #endregion

        #region Hubconnection Connect Methods 1-6
        private static async Task<bool> Connect(int HubNum, string url)
        {
            List<string> ipList = new List<string>();

            ipList = ReportedIPs;
            if (HubNum == 1)
            {
                try
                {
                    hubConnection1 = new HubConnectionBuilder()
                    .WithUrl(url, options => {

                    })
                    .WithAutomaticReconnect()
                    .Build();

                    hubConnection1.On<string, string>("GetMessage", async (message, data) => {
                        if (message == "tx" || message == "blk" || message == "val" || message == "IP")
                        {
                            if (message != "IP")
                            {
                                await NodeDataProcessor.ProcessData(message, data);
                            }
                            else
                            {
                                ipList.Add(data.ToString());
                                ReportedIPs = ipList;
                            }
                        }

                    });


                    hubConnection1.StartAsync().Wait();


                    NodeDict[1] = url.Replace("http://", "").Replace("/blockchain", "");


                    return true;
                }
                catch (Exception ex)
                {
                    return false;
                }
            }
            else if (HubNum == 2)
            {
                try
                {
                    hubConnection2 = new HubConnectionBuilder()
                    .WithUrl(url, options => {

                    })
                    .WithAutomaticReconnect()
                    .Build();

                    hubConnection2.On<string, string>("GetMessage", async (message, data) => {
                        if (message == "tx" || message == "blk" || message == "val" || message == "IP")
                        {
                            if (message != "IP")
                            {
                                await NodeDataProcessor.ProcessData(message, data);
                            }
                            else
                            {
                                ipList.Add(data.ToString());
                                ReportedIPs = ipList;
                            }
                        }
                    });


                    hubConnection2.StartAsync().Wait();

                    NodeDict[2] = url.Replace("http://", "").Replace("/blockchain", "");

                    return true;
                }
                catch (Exception ex)
                {
                    var capExcept = ex;
                    return false;
                }

            }
            else if (HubNum == 3)
            {
                try
                {
                    hubConnection3 = new HubConnectionBuilder()
                    .WithUrl(url, options => {

                    })
                    .WithAutomaticReconnect()
                    .Build();

                    hubConnection3.On<string, string>("GetMessage", async (message, data) => {
                        if (message == "tx" || message == "blk" || message == "val" || message == "IP")
                        {
                            if (message != "IP")
                            {
                                await NodeDataProcessor.ProcessData(message, data);
                            }
                            else
                            {
                                ipList.Add(data.ToString());
                                ReportedIPs = ipList;
                            }
                        }
                    });


                    hubConnection3.StartAsync().Wait();

                    NodeDict[3] = url.Replace("http://", "").Replace("/blockchain", "");

                    return true;
                }
                catch (Exception ex)
                {
                    return false;
                }
            }
            else if (HubNum == 4)
            {
                try
                {
                    hubConnection4 = new HubConnectionBuilder()
                    .WithUrl(url, options => {

                    })
                    .WithAutomaticReconnect()
                    .Build();

                    hubConnection4.On<string, string>("GetMessage", async (message, data) => {
                        if (message == "tx" || message == "blk" || message == "val" || message == "IP")
                        {
                            if (message != "IP")
                            {
                                await NodeDataProcessor.ProcessData(message, data);
                            }
                            else
                            {
                                ipList.Add(data.ToString());
                                ReportedIPs = ipList;
                            }
                        }
                    });


                    hubConnection4.StartAsync().Wait();

                    NodeDict[4] = url.Replace("http://", "").Replace("/blockchain", "");

                    return true;
                }
                catch (Exception ex)
                {
                    return false;
                }
            }
            else if (HubNum == 5)
            {
                try
                {
                    hubConnection5 = new HubConnectionBuilder()
                    .WithUrl(url, options => {

                    })
                    .WithAutomaticReconnect()
                    .Build();

                    hubConnection5.On<string, string>("GetMessage", async (message, data) => {
                        if (message == "tx" || message == "blk" || message == "val" || message == "IP")
                        {
                            if (message != "IP")
                            {
                                await NodeDataProcessor.ProcessData(message, data);
                            }
                            else
                            {
                                ipList.Add(data.ToString());
                                ReportedIPs = ipList;
                            }
                        }
                    });


                    hubConnection5.StartAsync().Wait();

                    NodeDict[5] = url.Replace("http://", "").Replace("/blockchain", "");

                    return true;
                }
                catch (Exception ex)
                {
                    return false;
                }
            }
            else if (HubNum == 6)
            {
                try
                {
                    hubConnection6 = new HubConnectionBuilder()
                    .WithUrl(url, options => {

                    })
                    .WithAutomaticReconnect()
                    .Build();

                    hubConnection6.On<string, string>("GetMessage", async (message, data) => {
                        if (message == "tx" || message == "blk" || message == "val" || message == "IP")
                        {
                            if (message != "IP")
                            {
                                await NodeDataProcessor.ProcessData(message, data);
                            }
                            else
                            {
                                ipList.Add(data.ToString());
                                ReportedIPs = ipList;
                            }
                        }
                    });


                    hubConnection6.StartAsync().Wait();

                    NodeDict[6] = url.Replace("http://", "").Replace("/blockchain", "");

                    return true;
                }
                catch (Exception ex)
                {
                    return false;
                }
            }

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
                    options.Headers.Add("walver", Program.CLIVersion);
                })
                .WithAutomaticReconnect()
                .Build();

                hubAdjConnection1.On<string, string>("GetAdjMessage", async (message, data) => {
                    if (message == "task" || message == "taskResult" || message == "fortisPool" || message == "status" || message == "tx")
                    {
                        switch(message)
                        {
                            case "task":
                                await ValidatorProcessor.ProcessData(message, data);
                                break;
                            case "taskResult":
                                await ValidatorProcessor.ProcessData(message, data);
                                break;
                            case "fortisPool":
                                await ValidatorProcessor.ProcessData(message, data);
                                break;
                            case "status":
                                Console.WriteLine(data);
                                break;
                            case "tx":
                                await ValidatorProcessor.ProcessData(message, data);
                                break;
                        }
                    }
                });

                hubAdjConnection1.StartAsync().Wait();

            }
            catch (Exception ex)
            {
                
            }
        }


        #endregion

        #region Connect to Peers
        public static async Task<bool> ConnectToPeers()
        {
            //List<Peers> peers = new List<Peers>();
            var nodes = Program.Nodes.ToList();
            //peers = Peers.PeerList();

            int successCount = 0;

            var peerDB = Peers.GetAll();
            if(peerDB != null)
            {
                var peers = peerDB.FindAll();

                if (peers.Count() == 0)
                {
                    await NodeConnector.StartNodeConnecting();
                    peerDB = Peers.GetAll();
                    peers = peerDB.FindAll();
                }

                if (peers.Count() > 0)
                {
                    if (peers.Count() > 8) //if peer db larger than 8 records get only 8 and use those records. we only start with low fail count.
                    {
                        Random rnd = new Random();
                        var peerList = peers.Where(x => x.FailCount <= 1 && x.IsOutgoing == true).OrderBy(x => rnd.Next()).Take(8).ToList();
                        if (peerList.Count() >= 4)
                        {
                            peers = peerList;
                        }
                        else
                        {
                            peers = peers.Where(x => x.IsOutgoing == true).OrderBy(x => rnd.Next()).Take(8).ToList();
                        }

                    }
                    else
                    {

                    }
                    foreach (var peer in peers)
                    {

                        var hubCon = await GetAvailablePeerHubs();
                        if (hubCon == 0)
                            return false;
                        try
                        {
                            var urlCheck = peer.PeerIP + ":" + Program.Port;
                            var nodeExist = nodes.Exists(x => x.NodeIP == urlCheck);

                            if (!nodeExist)
                            {
                                //Console.Write("Peer found, attempting to connect to: " + peer.PeerIP);
                                var url = "http://" + peer.PeerIP + ":" + Program.Port + "/blockchain";
                                var conResult = await Connect(hubCon, url);
                                if (conResult != false)
                                {
                                    successCount += 1;
                                    peer.FailCount = 0; //peer responded. Reset fail count
                                    peerDB.Update(peer);
                                }
                                else
                                {
                                    peer.FailCount += 1;
                                    peerDB.Update(peer);
                                }
                            }


                        }
                        catch (Exception ex)
                        {
                            //peer did not response correctly or at all
                            //Need to track peers that fail, but in memory.
                        }

                    }
                    if (successCount > 0)

                        return true;
                }
            }
            

            return false;
        }

        public static async Task<bool> PingBackPeer(string peerIP)
        {
            try
            {
                var url = "http://" + peerIP + ":" + Program.Port + "/blockchain";
                var connection = new HubConnectionBuilder().WithUrl(url).Build();
                string response = "";
                connection.StartAsync().Wait();
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

        #region Send Task Answer
        public static async Task SendTaskAnswer(TaskAnswer taskAnswer)
        {
            var adjudicatorConnected = IsAdjConnected1;
            if(adjudicatorConnected)
            {
                try
                {
                    if(taskAnswer.Block.Height == Program.BlockHeight + 1)
                    {
                        if (hubAdjConnection1 != null)
                        {
                            var result = await hubAdjConnection1.InvokeCoreAsync<bool>("ReceiveTaskAnswer", args: new object?[] { taskAnswer });
                            if (result)
                            {
                                LastSentBlockHeight = taskAnswer.Block.Height;
                            }
                        }
                    }
                }
                catch(Exception ex)
                {

                }
            }
            else
            {
                //reconnect and then send
            }
        }

        #endregion

        #region Send TX To Adjudicators
        public static async Task SendTXToAdjudicator(Transaction tx)
        {
            var adjudicatorConnected = IsAdjConnected1;
            if (adjudicatorConnected && hubAdjConnection1 != null)
            {
                try
                {
                    await hubAdjConnection1.InvokeAsync("ReceiveTX", tx);
                }
                catch (Exception ex)
                {

                }
            }
            else
            {
                //reconnect and then send
            }
        }
        #endregion

        #region Get Block
        public static async Task<List<Block>> GetBlock() //base example
        {
            var currentBlock = Program.BlockHeight != -1 ? Program.LastBlock.Height : -1; //-1 means fresh client with no blocks
            var nBlock = new Block();
            List<Block> blocks = new List<Block>();
            var peersConnected = await P2PClient.ArePeersConnected();

            if (peersConnected.Item1 == false)
            {
                //Need peers
                return blocks;
            }
            else
            {
                try
                {
                    if (hubConnection1 != null && IsConnected1)
                    {
                        nBlock = await hubConnection1.InvokeCoreAsync<Block>("SendBlock", args: new object?[] { currentBlock });
                        if (nBlock != null)
                        {
                            blocks.Add(nBlock);
                            currentBlock += 1;
                        }

                    }
                }
                catch (Exception ex)
                {
                    //possible dead connection, or node is offline
                }
                try
                {
                    if (hubConnection2 != null && IsConnected2)
                    {
                        nBlock = await hubConnection2.InvokeCoreAsync<Block>("SendBlock", args: new object?[] { currentBlock });
                        if (nBlock != null)
                        {
                            if (!blocks.Exists(x => x.Height == nBlock.Height))
                            {
                                blocks.Add(nBlock);
                                currentBlock += 1;
                            }
                        }

                    }
                }
                catch (Exception ex)
                {
                    //possible dead connection, or node is offline
                }
                try
                {
                    if (hubConnection3 != null && IsConnected3)
                    {
                        nBlock = await hubConnection3.InvokeCoreAsync<Block>("SendBlock", args: new object?[] { currentBlock });
                        if (nBlock != null)
                        {
                            if (!blocks.Exists(x => x.Height == nBlock.Height))
                            {
                                blocks.Add(nBlock);
                                currentBlock += 1;
                            }

                        }
                    }
                }
                catch (Exception ex)
                {
                    //possible dead connection, or node is offline
                }
                try
                {
                    if (hubConnection4 != null && IsConnected4)
                    {
                        nBlock = await hubConnection4.InvokeCoreAsync<Block>("SendBlock", args: new object?[] { currentBlock });
                        if (nBlock != null)
                        {
                            if (!blocks.Exists(x => x.Height == nBlock.Height))
                            {
                                blocks.Add(nBlock);
                                currentBlock += 1;
                            }

                        }
                    }
                }
                catch (Exception ex)
                {
                    //possible dead connection, or node is offline
                }
                try
                {
                    if (hubConnection5 != null && IsConnected5)
                    {
                        nBlock = await hubConnection5.InvokeCoreAsync<Block>("SendBlock", args: new object?[] { currentBlock });
                        if (nBlock != null)
                        {
                            if (!blocks.Exists(x => x.Height == nBlock.Height))
                            {
                                blocks.Add(nBlock);
                                currentBlock += 1;
                            }

                        }
                    }
                }
                catch (Exception ex)
                {
                    //possible dead connection, or node is offline
                }
                try
                {
                    if (hubConnection6 != null && IsConnected6)
                    {
                        nBlock = await hubConnection6.InvokeCoreAsync<Block>("SendBlock", args: new object?[] { currentBlock });
                        if (nBlock != null)
                        {
                            if (!blocks.Exists(x => x.Height == nBlock.Height))
                            {
                                blocks.Add(nBlock);
                                currentBlock += 1;
                            }

                        }
                    }
                }
                catch (Exception ex)
                {
                    //possible dead connection, or node is offline
                }

                return blocks;

            }

        }

        #endregion

        #region Get Height of Nodes for Timed Events
        public static async Task<bool> GetNodeHeight()
        {
            Dictionary<string, long> nodeHeightDict = new Dictionary<string, long>();
            var nodes = Program.Nodes;
            var nodeList = nodes.ToList();
            var peersConnected = await P2PClient.ArePeersConnected();
            bool result = false;
            if (peersConnected.Item1 == false)
            {
                //Need peers
                return result;
            }
            else
            {
                try
                {
                    if (hubConnection1 != null && IsConnected1)
                    {
                        var startTimer = DateTime.UtcNow;
                        long remoteNodeHeight = await hubConnection1.InvokeAsync<long>("SendBlockHeight");
                        var endTimer = DateTime.UtcNow;
                        var totalMS = (endTimer - startTimer).Milliseconds;

                        var nodeInfo = NodeDict[1];

                        nodeHeightDict.Add(nodeInfo, remoteNodeHeight);

                        var node = nodeList.Where(x => x.NodeIP == nodeInfo).FirstOrDefault();
                        if (node != null)
                        {
                            node.NodeLastChecked = DateTime.UtcNow;
                            node.NodeLatency = totalMS;
                            node.NodeHeight = remoteNodeHeight;
                        }
                        else
                        {
                            NodeInfo nNodeInfo = new NodeInfo {
                                NodeHeight = remoteNodeHeight,
                                NodeLatency = totalMS,
                                NodeIP = nodeInfo,
                                NodeLastChecked = DateTime.UtcNow
                            };

                            Program.Nodes.Add(nNodeInfo);
                        }

                        result = true;
                    }
                }
                catch (Exception ex)
                {
                    //node is offline
                    var nodeInfo = NodeDict[1];
                    if (nodeInfo != null)
                    {
                        var node = nodeList.Where(x => x.NodeIP == nodeInfo).FirstOrDefault();
                        if (node != null)
                        {
                            Program.Nodes.Remove(node);
                        }
                        hubConnection1 = null;
                        NodeDict[1] = null;
                    }
                }

                try
                {
                    if (hubConnection2 != null && IsConnected2)
                    {
                        var startTimer = DateTime.UtcNow;
                        long remoteNodeHeight = await hubConnection2.InvokeAsync<long>("SendBlockHeight");
                        var endTimer = DateTime.UtcNow;
                        var totalMS = (endTimer - startTimer).Milliseconds;

                        var nodeInfo = NodeDict[2];

                        nodeHeightDict.Add(nodeInfo, remoteNodeHeight);

                        var node = nodeList.Where(x => x.NodeIP == nodeInfo).FirstOrDefault();
                        if (node != null)
                        {
                            node.NodeLastChecked = DateTime.UtcNow;
                            node.NodeLatency = totalMS;
                            node.NodeHeight = remoteNodeHeight;
                        }
                        else
                        {
                            NodeInfo nNodeInfo = new NodeInfo
                            {
                                NodeHeight = remoteNodeHeight,
                                NodeLatency = totalMS,
                                NodeIP = nodeInfo,
                                NodeLastChecked = DateTime.UtcNow
                            };

                            Program.Nodes.Add(nNodeInfo);
                        }

                        result = true;
                    }
                }
                catch (Exception ex)
                {
                    //node is offline
                    var nodeInfo = NodeDict[2];
                    if (nodeInfo != null)
                    {
                        var node = nodeList.Where(x => x.NodeIP == nodeInfo).FirstOrDefault();
                        if (node != null)
                        {
                            Program.Nodes.Remove(node);
                        }
                        hubConnection2 = null;
                        NodeDict[2] = null;
                    }

                }

                try
                {
                    if (hubConnection3 != null && IsConnected3)
                    {
                        var startTimer = DateTime.UtcNow;
                        long remoteNodeHeight = await hubConnection3.InvokeAsync<long>("SendBlockHeight");
                        var endTimer = DateTime.UtcNow;
                        var totalMS = (endTimer - startTimer).Milliseconds;

                        var nodeInfo = NodeDict[3];

                        nodeHeightDict.Add(nodeInfo, remoteNodeHeight);

                        var node = nodeList.Where(x => x.NodeIP == nodeInfo).FirstOrDefault();
                        if (node != null)
                        {
                            node.NodeLastChecked = DateTime.UtcNow;
                            node.NodeLatency = totalMS;
                            node.NodeHeight = remoteNodeHeight;
                        }
                        else
                        {
                            NodeInfo nNodeInfo = new NodeInfo
                            {
                                NodeHeight = remoteNodeHeight,
                                NodeLatency = totalMS,
                                NodeIP = nodeInfo,
                                NodeLastChecked = DateTime.UtcNow
                            };

                            Program.Nodes.Add(nNodeInfo);
                        }

                        result = true;
                    }
                }
                catch (Exception ex)
                {
                    //node is offline
                    var nodeInfo = NodeDict[3];
                    if (nodeInfo != null)
                    {
                        var node = nodeList.Where(x => x.NodeIP == nodeInfo).FirstOrDefault();
                        if (node != null)
                        {
                            Program.Nodes.Remove(node);
                        }
                        hubConnection3 = null;
                        NodeDict[3] = null;
                    }
                }

                try
                {
                    if (hubConnection4 != null && IsConnected4)
                    {
                        var startTimer = DateTime.UtcNow;
                        long remoteNodeHeight = await hubConnection4.InvokeAsync<long>("SendBlockHeight");
                        var endTimer = DateTime.UtcNow;
                        var totalMS = (endTimer - startTimer).Milliseconds;

                        var nodeInfo = NodeDict[4];

                        nodeHeightDict.Add(nodeInfo, remoteNodeHeight);

                        var node = nodeList.Where(x => x.NodeIP == nodeInfo).FirstOrDefault();
                        if (node != null)
                        {
                            node.NodeLastChecked = DateTime.UtcNow;
                            node.NodeLatency = totalMS;
                            node.NodeHeight = remoteNodeHeight;
                        }
                        else
                        {
                            NodeInfo nNodeInfo = new NodeInfo
                            {
                                NodeHeight = remoteNodeHeight,
                                NodeLatency = totalMS,
                                NodeIP = nodeInfo,
                                NodeLastChecked = DateTime.UtcNow
                            };

                            Program.Nodes.Add(nNodeInfo);
                        }

                        result = true;
                    }
                }
                catch (Exception ex)
                {
                    //node is offline
                    var nodeInfo = NodeDict[4];
                    if (nodeInfo != null)
                    {
                        var node = nodeList.Where(x => x.NodeIP == nodeInfo).FirstOrDefault();
                        if (node != null)
                        {
                            Program.Nodes.Remove(node);
                        }
                        hubConnection4 = null;
                        NodeDict[4] = null;
                    }
                }

                try
                {
                    if (hubConnection5 != null && IsConnected5)
                    {
                        var startTimer = DateTime.UtcNow;
                        long remoteNodeHeight = await hubConnection5.InvokeAsync<long>("SendBlockHeight");
                        var endTimer = DateTime.UtcNow;
                        var totalMS = (endTimer - startTimer).Milliseconds;

                        var nodeInfo = NodeDict[5];

                        nodeHeightDict.Add(nodeInfo, remoteNodeHeight);

                        var node = nodeList.Where(x => x.NodeIP == nodeInfo).FirstOrDefault();
                        if (node != null)
                        {
                            node.NodeLastChecked = DateTime.UtcNow;
                            node.NodeLatency = totalMS;
                            node.NodeHeight = remoteNodeHeight;
                        }
                        else
                        {
                            NodeInfo nNodeInfo = new NodeInfo
                            {
                                NodeHeight = remoteNodeHeight,
                                NodeLatency = totalMS,
                                NodeIP = nodeInfo,
                                NodeLastChecked = DateTime.UtcNow
                            };

                            Program.Nodes.Add(nNodeInfo);
                        }

                        result = true;
                    }
                }
                catch (Exception ex)
                {
                    //node is offline
                    var nodeInfo = NodeDict[5];
                    if (nodeInfo != null)
                    {
                        var node = nodeList.Where(x => x.NodeIP == nodeInfo).FirstOrDefault();
                        if (node != null)
                        {
                            Program.Nodes.Remove(node);
                        }
                        hubConnection5 = null;
                        NodeDict[5] = null;
                    }
                }

                try
                {
                    if (hubConnection6 != null && IsConnected6)
                    {
                        var startTimer = DateTime.UtcNow;
                        long remoteNodeHeight = await hubConnection6.InvokeAsync<long>("SendBlockHeight");
                        var endTimer = DateTime.UtcNow;
                        var totalMS = (endTimer - startTimer).Milliseconds;

                        var nodeInfo = NodeDict[6];

                        nodeHeightDict.Add(nodeInfo, remoteNodeHeight);

                        var node = nodeList.Where(x => x.NodeIP == nodeInfo).FirstOrDefault();
                        if (node != null)
                        {
                            node.NodeLastChecked = DateTime.UtcNow;
                            node.NodeLatency = totalMS;
                            node.NodeHeight = remoteNodeHeight;
                        }
                        else
                        {
                            NodeInfo nNodeInfo = new NodeInfo
                            {
                                NodeHeight = remoteNodeHeight,
                                NodeLatency = totalMS,
                                NodeIP = nodeInfo,
                                NodeLastChecked = DateTime.UtcNow
                            };

                            Program.Nodes.Add(nNodeInfo);
                        }

                        result = true;
                    }
                }
                catch (Exception ex)
                {
                    //node is offline
                    var nodeInfo = NodeDict[6];
                    if (nodeInfo != null)
                    {
                        var node = nodeList.Where(x => x.NodeIP == nodeInfo).FirstOrDefault();
                        if (node != null)
                        {
                            Program.Nodes.Remove(node);
                        }
                        hubConnection6 = null;
                        NodeDict[6] = null;
                    }
                }

            }

            return result;
        }

        #endregion

        #region Get Current Height of Nodes
        public static async Task<(bool, long)> GetCurrentHeight()
        {
            bool newHeightFound = false;
            long height = 0;

            var peersConnected = await P2PClient.ArePeersConnected();

            if (peersConnected.Item1 == false)
            {
                //Need peers
                return (newHeightFound, height);
            }
            else
            {
                if(Program.BlockHeight == -1)
                {
                    return (true, -1);
                }

                long myHeight = Program.BlockHeight;

                try
                {
                    if (hubConnection1 != null && IsConnected1)
                    {
                        long remoteNodeHeight = await hubConnection1.InvokeAsync<long>("SendBlockHeight");

                        if (myHeight < remoteNodeHeight)
                        {
                            newHeightFound = true;
                            height = remoteNodeHeight;
                        }

                    }
                }
                catch (Exception ex)
                {
                    //node is offline
                }
                try
                {
                    if (hubConnection2 != null && IsConnected2)
                    {
                        long remoteNodeHeight = await hubConnection2.InvokeAsync<long>("SendBlockHeight");

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
                catch (Exception ex)
                {
                    //node is offline
                }

                try
                {
                    if (hubConnection3 != null && IsConnected3)
                    {
                        long remoteNodeHeight = await hubConnection3.InvokeAsync<long>("SendBlockHeight");

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
                catch (Exception ex)
                {
                    //node is offline
                }

                try
                {
                    if (hubConnection4 != null && IsConnected4)
                    {
                        long remoteNodeHeight = await hubConnection4.InvokeAsync<long>("SendBlockHeight");

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
                catch (Exception ex)
                {
                    //node is offline
                }

                try
                {
                    if (hubConnection5 != null && IsConnected5)
                    {
                        long remoteNodeHeight = await hubConnection5.InvokeAsync<long>("SendBlockHeight");

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
                catch (Exception ex)
                {
                    //node is offline
                }

                try
                {
                    if (hubConnection6 != null && IsConnected6)
                    {
                        long remoteNodeHeight = await hubConnection6.InvokeAsync<long>("SendBlockHeight");

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
                catch (Exception ex)
                {
                    //node is offline
                }

            }
            return (newHeightFound, height);
        }

        #endregion

        #region Get Lead Adjudicators
        public static async Task<Adjudicators?> GetLeadAdjudicator()
        {
            Adjudicators? LeadAdj = null;

            var peersConnected = await P2PClient.ArePeersConnected();

            if (peersConnected.Item1 == false)
            {
                //Need peers
                return null;
            }
            else
            {
                if (Program.BlockHeight == -1)
                {
                    return null;
                }

                long myHeight = Program.BlockHeight;

                try
                {
                    if (hubConnection1 != null && IsConnected1)
                    {
                        var leadAdjudictor = await hubConnection1.InvokeAsync<Adjudicators?>("SendLeadAdjudicator");

                        if (leadAdjudictor != null)
                        {
                            var adjudicators = Adjudicators.AdjudicatorData.GetAll();
                            if(adjudicators != null)
                            {
                                var lAdj = adjudicators.FindOne(x => x.IsLeadAdjuidcator == true);
                                if(lAdj == null)
                                {
                                    adjudicators.Insert(leadAdjudictor);
                                    LeadAdj = leadAdjudictor;
                                }
                            }
                        }

                    }
                }
                catch (Exception ex)
                {
                    //node is offline
                }
                try
                {
                    if (hubConnection2 != null && IsConnected2)
                    {
                        var leadAdjudictor = await hubConnection2.InvokeAsync<Adjudicators?>("SendLeadAdjudicator");

                        if (leadAdjudictor != null)
                        {
                            var adjudicators = Adjudicators.AdjudicatorData.GetAll();
                            if (adjudicators != null)
                            {
                                var lAdj = adjudicators.FindOne(x => x.IsLeadAdjuidcator == true);
                                if (lAdj == null)
                                {
                                    adjudicators.Insert(leadAdjudictor);
                                    LeadAdj = leadAdjudictor;
                                }
                            }
                        }

                    }
                }
                catch (Exception ex)
                {
                    //node is offline
                }

                try
                {
                    if (hubConnection3 != null && IsConnected3)
                    {
                        var leadAdjudictor = await hubConnection3.InvokeAsync<Adjudicators?>("SendLeadAdjudicator");

                        if (leadAdjudictor != null)
                        {
                            var adjudicators = Adjudicators.AdjudicatorData.GetAll();
                            if (adjudicators != null)
                            {
                                var lAdj = adjudicators.FindOne(x => x.IsLeadAdjuidcator == true);
                                if (lAdj == null)
                                {
                                    adjudicators.Insert(leadAdjudictor);
                                    LeadAdj = leadAdjudictor;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    //node is offline
                }

                try
                {
                    if (hubConnection4 != null && IsConnected4)
                    {
                        var leadAdjudictor = await hubConnection4.InvokeAsync<Adjudicators?>("SendLeadAdjudicator");

                        if (leadAdjudictor != null)
                        {
                            var adjudicators = Adjudicators.AdjudicatorData.GetAll();
                            if (adjudicators != null)
                            {
                                var lAdj = adjudicators.FindOne(x => x.IsLeadAdjuidcator == true);
                                if (lAdj == null)
                                {
                                    adjudicators.Insert(leadAdjudictor);
                                    LeadAdj = leadAdjudictor;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    //node is offline
                }

                try
                {
                    if (hubConnection5 != null && IsConnected5)
                    {
                        var leadAdjudictor = await hubConnection5.InvokeAsync<Adjudicators?>("SendLeadAdjudicator");

                        if (leadAdjudictor != null)
                        {
                            var adjudicators = Adjudicators.AdjudicatorData.GetAll();
                            if (adjudicators != null)
                            {
                                var lAdj = adjudicators.FindOne(x => x.IsLeadAdjuidcator == true);
                                if (lAdj == null)
                                {
                                    adjudicators.Insert(leadAdjudictor);
                                    LeadAdj = leadAdjudictor;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    //node is offline
                }

                try
                {
                    if (hubConnection6 != null && IsConnected6)
                    {
                        var leadAdjudictor = await hubConnection6.InvokeAsync<Adjudicators?>("SendLeadAdjudicator");

                        if (leadAdjudictor != null)
                        {
                            var adjudicators = Adjudicators.AdjudicatorData.GetAll();
                            if (adjudicators != null)
                            {
                                var lAdj = adjudicators.FindOne(x => x.IsLeadAdjuidcator == true);
                                if (lAdj == null)
                                {
                                    adjudicators.Insert(leadAdjudictor);
                                    LeadAdj = leadAdjudictor;
                                }
                            }
                        }
                    }

                }
                catch (Exception ex)
                {
                    //node is offline
                }

            }
            return LeadAdj;
        }

        #endregion

        #region Send Transactions to mempool 
        public static async void SendTXMempool(Transaction txSend)
        {
            var peersConnected = await P2PClient.ArePeersConnected();

            if (peersConnected.Item1 == false)
            {
                //Need peers
                Console.WriteLine("Failed to broadcast Transaction. No peers are connected to you.");
            }
            else
            {
                try
                {
                    if (hubConnection1 != null && IsConnected1)
                    {
                        string message = await hubConnection1.InvokeCoreAsync<string>("SendTxToMempool", args: new object?[] { txSend });

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
                }
                catch (Exception ex)
                {

                }

                try
                {
                    if (hubConnection2 != null && IsConnected2)
                    {
                        string message = await hubConnection2.InvokeCoreAsync<string>("SendTxToMempool", args: new object?[] { txSend });

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
                }
                catch (Exception ex)
                {

                }

                try
                {
                    if (hubConnection3 != null && IsConnected3)
                    {
                        string message = await hubConnection3.InvokeCoreAsync<string>("SendTxToMempool", args: new object?[] { txSend });

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
                }
                catch (Exception ex)
                {

                }

                try
                {
                    if (hubConnection4 != null && IsConnected4)
                    {
                        string message = await hubConnection4.InvokeCoreAsync<string>("SendTxToMempool", args: new object?[] { txSend });

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
                }
                catch (Exception ex)
                {

                }

                try
                {
                    if (hubConnection5 != null && IsConnected5)
                    {
                        string message = await hubConnection5.InvokeCoreAsync<string>("SendTxToMempool", args: new object?[] { txSend });

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
                }
                catch (Exception ex)
                {

                }

                try
                {
                    if (hubConnection6 != null && IsConnected6)
                    {
                        string message = await hubConnection6.InvokeCoreAsync<string>("SendTxToMempool", args: new object?[] { txSend });

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
                }
                catch (Exception ex)
                {

                }

            }



        }

        #endregion

        #region Broadcast Blocks to Peers
        public static async Task BroadcastBlock(Block block)
        {
            var peersConnected = await P2PClient.ArePeersConnected();

            if (peersConnected.Item1 == false)
            {
                //Need peers
                Console.WriteLine("Failed to broadcast Transaction. No peers are connected to you.");
            }
            else
            {
                try
                {
                    if (hubConnection1 != null && IsConnected1)
                    {
                        await hubConnection1.InvokeCoreAsync<string>("ReceiveBlock", args: new object?[] { block });
                    }
                }
                catch (Exception ex)
                {
                    //possible dead connection, or node is offline
                    Console.WriteLine("Error Sending Transaction. Please try again!");
                }
                try
                {

                    if (hubConnection2 != null && IsConnected2)
                    {
                        await hubConnection2.InvokeCoreAsync<string>("ReceiveBlock", args: new object?[] { block });
                    }
                }
                catch (Exception ex)
                {

                }

                try
                {
                    if (hubConnection3 != null && IsConnected3)
                    {
                        await hubConnection3.InvokeCoreAsync<string>("ReceiveBlock", args: new object?[] { block });
                    }
                }
                catch (Exception ex)
                {

                }
                try
                {
                    if (hubConnection4 != null && IsConnected4)
                    {
                        await hubConnection4.InvokeCoreAsync<string>("ReceiveBlock", args: new object?[] { block });
                    }
                }
                catch (Exception ex)
                {

                }
                try
                {
                    if (hubConnection5 != null && IsConnected5)
                    {
                        await hubConnection5.InvokeCoreAsync<string>("ReceiveBlock", args: new object?[] { block });
                    }
                }
                catch (Exception ex)
                {

                }
                try
                {
                    if (hubConnection6 != null && IsConnected6)
                    {
                        await hubConnection6.InvokeCoreAsync<string>("ReceiveBlock", args: new object?[] { block });
                    }
                }
                catch (Exception ex)
                {

                }

            }

        }
        #endregion

    }
}
