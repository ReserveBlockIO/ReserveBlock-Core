using LiteDB;
using Microsoft.AspNetCore.SignalR.Client;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Nodes;
using ReserveBlockCore.Services;
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
        /// Below are reserved for validators to open up communications to get the next 2 nodes for block solving.
        /// </summary>

        private static HubConnection? hubConnection7; //reserved for validators
        public static bool IsConnected7 => hubConnection7?.State == HubConnectionState.Connected;

        private static HubConnection? hubConnection8; //reserved for validators
        public static bool IsConnected8 => hubConnection8?.State == HubConnectionState.Connected;

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
            var nodeList = Program.Nodes;
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
            if (hubConnection7 != null)
            {
                await hubConnection7.DisposeAsync();
            }
            if (hubConnection8 != null)
            {
                await hubConnection8.DisposeAsync();
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

        #region Connect to Peers
        public static async Task<bool> ConnectToPeers()
        {
            //List<Peers> peers = new List<Peers>();
            var nodes = Program.Nodes;
            //peers = Peers.PeerList();

            int successCount = 0;

            var peerDB = Peers.GetAll();
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
                    if (peerList.Count() >= 2)
                    {
                        peers = peerList;
                    }
                    else
                    {
                        peers = peers.Where(x => x.FailCount <= 9 && x.IsOutgoing == true).OrderBy(x => rnd.Next()).Take(8).ToList();
                    }

                }
                else
                {
                    await NodeConnector.StartNodeConnecting();
                    peerDB = Peers.GetAll();
                    peers = peerDB.FindAll();
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
                            Console.Write("Peer found, attempting to connect to: " + peer.PeerIP);
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
                        peer.FailCount += 1;
                        peerDB.Update(peer);
                    }

                }
                if (successCount > 0)

                    return true;
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

        #region Get Block
        public static async Task<List<Block>> GetBlock() //base example
        {
            var currentBlock = BlockchainData.GetLastBlock() != null ? BlockchainData.GetLastBlock().Height : -1; //-1 means fresh client with no blocks
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
            var nodeList = Program.Nodes;
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
                var blocks = BlockchainData.GetBlocks();

                if (blocks.Count() == 0)
                    return (true, -1);

                long myHeight = BlockchainData.GetHeight();

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

        #region Get Current Masternodes
        public static async Task<bool> GetMasternodes()
        {
            var peersConnected = await P2PClient.ArePeersConnected();
            var output = false;
            if (peersConnected.Item1 == false)
            {
                //Need peers
                return output;
            }
            else
            {
                var validators = Validators.Validator.GetAll();
                var valCount = validators.FindAll().Count();

                try
                {


                    if (hubConnection1 != null && IsConnected1)
                    {
                        List<Validators>? remoteValidators = await hubConnection1.InvokeCoreAsync<List<Validators>?>("GetMasternodes", args: new object?[] { valCount });
                        if (remoteValidators != null)
                        {
                            if (valCount == 0)
                            {
                                validators.InsertBulk(remoteValidators);
                                output = true;
                            }
                            else
                            {
                                var locValidators = validators.FindAll().ToList();
                                var newValidators = remoteValidators.Where(x => !locValidators.Any(y => x.Address == y.Address) && x.WalletVersion != null).ToList();

                                if (newValidators.Count() > 0)
                                {
                                    validators.InsertBulk(newValidators);
                                    output = true;
                                }
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
                    if (hubConnection2 != null && IsConnected2)
                    {
                        List<Validators>? remoteValidators = await hubConnection2.InvokeCoreAsync<List<Validators>?>("GetMasternodes", args: new object?[] { valCount });
                        if (remoteValidators != null)
                        {
                            if (valCount == 0)
                            {
                                validators.InsertBulk(remoteValidators);
                                output = true;
                            }
                            else
                            {
                                var locValidators = validators.FindAll().ToList();
                                var newValidators = remoteValidators.Where(x => !locValidators.Any(y => x.Address == y.Address) && x.WalletVersion != null).ToList();

                                if (newValidators.Count() > 0)
                                {
                                    validators.InsertBulk(newValidators);
                                    output = true;
                                }
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
                        List<Validators>? remoteValidators = await hubConnection3.InvokeCoreAsync<List<Validators>?>("GetMasternodes", args: new object?[] { valCount });
                        if (remoteValidators != null)
                        {
                            if (valCount == 0)
                            {
                                validators.InsertBulk(remoteValidators);
                                output = true;
                            }
                            else
                            {
                                var locValidators = validators.FindAll().ToList();
                                var newValidators = remoteValidators.Where(x => !locValidators.Any(y => x.Address == y.Address) && x.WalletVersion != null).ToList();

                                if (newValidators.Count() > 0)
                                {
                                    validators.InsertBulk(newValidators);
                                    output = true;
                                }
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
                        List<Validators>? remoteValidators = await hubConnection4.InvokeCoreAsync<List<Validators>?>("GetMasternodes", args: new object?[] { valCount });
                        if (remoteValidators != null)
                        {
                            if (valCount == 0)
                            {
                                validators.InsertBulk(remoteValidators);
                                output = true;
                            }
                            else
                            {
                                var locValidators = validators.FindAll().ToList();
                                var newValidators = remoteValidators.Where(x => !locValidators.Any(y => x.Address == y.Address) && x.WalletVersion != null).ToList();

                                if (newValidators.Count() > 0)
                                {
                                    validators.InsertBulk(newValidators);
                                    output = true;
                                }
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
                        List<Validators>? remoteValidators = await hubConnection5.InvokeCoreAsync<List<Validators>?>("GetMasternodes", args: new object?[] { valCount });
                        if (remoteValidators != null)
                        {
                            if (valCount == 0)
                            {
                                validators.InsertBulk(remoteValidators);
                                output = true;
                            }
                            else
                            {
                                var locValidators = validators.FindAll().ToList();
                                var newValidators = remoteValidators.Where(x => !locValidators.Any(y => x.Address == y.Address) && x.WalletVersion != null).ToList();

                                if (newValidators.Count() > 0)
                                {
                                    validators.InsertBulk(newValidators);
                                    output = true;
                                }
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
                        List<Validators>? remoteValidators = await hubConnection6.InvokeCoreAsync<List<Validators>?>("GetMasternodes", args: new object?[] { valCount });
                        if (remoteValidators != null)
                        {
                            if (valCount == 0)
                            {
                                validators.InsertBulk(remoteValidators);
                                output = true;
                            }
                            else
                            {
                                var locValidators = validators.FindAll().ToList();
                                var newValidators = remoteValidators.Where(x => !locValidators.Any(y => x.Address == y.Address) && x.WalletVersion != null).ToList();

                                if (newValidators.Count() > 0)
                                {
                                    validators.InsertBulk(newValidators);
                                    output = true;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    //possible dead connection, or node is offline
                }

            }

            return output;
        }

        #endregion

        #region Broadcast Validators for Masternodes

        public static async Task<bool> BroadcastValidatorNode(Validators nValidator)
        {
            var validators = Validators.Validator.GetAll();
            var validatorList = validators.FindAll().Where(x => x.IsActive == true);
            int successCount = 0;

            if (validatorList.Count() > 0)
            {
                await Parallel.ForEachAsync(validatorList, async (validator, token) => {
                    try
                    {
                        var hubConnection = new HubConnectionBuilder().WithUrl("http://" + validator.NodeIP + ":" + Program.Port + "/blockchain").Build();
                        var alive = hubConnection.StartAsync().Wait(3000); //give validator 3 secs to connect. Should be plenty
                        if (alive == true)
                        {
                            var message = await hubConnection.InvokeCoreAsync<string>("SendValidator", args: new object?[] { nValidator });

                            if (message == "VATN")
                            {
                                //success
                                successCount += 1;
                                hubConnection.StopAsync().Wait();
                                await hubConnection.DisposeAsync();//close connection when done to avoid any memory build up.
                            }
                            else if (message == "FTAV")
                            {
                                //Console.WriteLine("Failed to add Validator on remote node(s)");
                            }
                            else
                            {
                                //already in validator list
                            }
                        }
                        else
                        {
                            //hubConnection.StopAsync().Wait();
                            validator.IsActive = false;
                            validator.LastChecked = DateTime.UtcNow;
                            validators.Update(validator);
                        }
                    }
                    catch (Exception ex)
                    {

                    }
                });

                if (successCount > 0)
                {
                    await P2PClient.GetMasternodes();
                    return true;
                }
            }

            return false;

        }

        #endregion

        #region Broadcast Masternode *DEPRECATED
        public static async void BroadcastMasterNode(Validators nValidator)
        {
            var peersConnected = await P2PClient.ArePeersConnected();

            if (peersConnected.Item1 == false)
            {
                //Need peers
                Console.WriteLine("Failed to broadcast Masternode. No peers are connected to you.");
            }
            else
            {
                try
                {
                    if (hubConnection1 != null && IsConnected1)
                    {
                        string message = await hubConnection1.InvokeCoreAsync<string>("SendValidator", args: new object?[] { nValidator });

                        if (message == "VATN")
                        {
                            //success
                            Validators.Validator.Initialize();
                        }
                        else if (message == "FTAV")
                        {
                            Console.WriteLine("Failed to add Validator on remote node(s)");
                        }
                        else
                        {
                            //already in validator list
                        }
                    }

                    if (hubConnection2 != null && IsConnected2)
                    {
                        string message = await hubConnection2.InvokeCoreAsync<string>("SendValidator", args: new object?[] { nValidator });

                        if (message == "VATN")
                        {
                            //success
                            Validators.Validator.Initialize();
                        }
                        else if (message == "FTAV")
                        {
                            Console.WriteLine("Failed to add Validator on remote node(s)");
                        }
                        else
                        {
                            //already in validator list
                        }
                    }
                    if (hubConnection3 != null && IsConnected3)
                    {
                        string message = await hubConnection3.InvokeCoreAsync<string>("SendValidator", args: new object?[] { nValidator });

                        if (message == "VATN")
                        {
                            //success
                            Validators.Validator.Initialize();
                        }
                        else if (message == "FTAV")
                        {
                            Console.WriteLine("Failed to add Validator on remote node(s)");
                        }
                        else
                        {
                            //already in validator list
                        }
                    }
                    if (hubConnection4 != null && IsConnected4)
                    {
                        string message = await hubConnection4.InvokeCoreAsync<string>("SendValidator", args: new object?[] { nValidator });

                        if (message == "VATN")
                        {
                            //success
                            Validators.Validator.Initialize();
                        }
                        else if (message == "FTAV")
                        {
                            Console.WriteLine("Failed to add Validator on remote node(s)");
                        }
                        else
                        {
                            //already in validator list
                        }
                    }
                    if (hubConnection5 != null && IsConnected5)
                    {
                        string message = await hubConnection5.InvokeCoreAsync<string>("SendValidator", args: new object?[] { nValidator });

                        if (message == "VATN")
                        {
                            //success
                            Validators.Validator.Initialize();
                        }
                        else if (message == "FTAV")
                        {
                            Console.WriteLine("Failed to add Validator on remote node(s)");
                        }
                        else
                        {
                            //already in validator list
                        }
                    }
                    if (hubConnection6 != null && IsConnected6)
                    {
                        string message = await hubConnection6.InvokeCoreAsync<string>("SendValidator", args: new object?[] { nValidator });

                        if (message == "VATN")
                        {
                            //success
                            Validators.Validator.Initialize();
                        }
                        else if (message == "FTAV")
                        {
                            Console.WriteLine("Failed to add Validator on remote node(s)");
                        }
                        else
                        {
                            //already in validator list
                        }
                    }

                }
                catch (Exception ex)
                {
                    //possible dead connection, or node is offline
                    Console.WriteLine("Error Sending Validator Info.");
                }
            }

        }
        #endregion

        #region Ping Next Validators 
        public static async Task<(bool, bool)> PingNextValidators(Validators mainVal, Validators backupVal)
        {
            bool main = false;
            bool backup = false;
            var validators = Validators.Validator.GetAll();
            var hubConnection = new HubConnectionBuilder().WithUrl("http://" + mainVal.NodeIP + ":" + Program.Port + "/blockchain").Build();
            try
            {
                var alive = hubConnection.StartAsync().Wait(3000); //inside a try as target can actively refuse it.
                if (alive == true)
                {
                    var response = await hubConnection.InvokeAsync<bool>("PingNextValidator");

                    if (response == true)
                    {
                        main = true;
                        mainVal.FailCount = 0;
                        validators.Update(mainVal);
                        hubConnection.StopAsync().Wait();
                        await hubConnection.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                mainVal.FailCount += 1;
                validators.Update(mainVal);
            }


            var hubConnection2 = new HubConnectionBuilder().WithUrl("http://" + backupVal.NodeIP + ":" + Program.Port + "/blockchain").Build();
            try
            {
                var alive2 = hubConnection2.StartAsync().Wait(3000);

                if (alive2 == true)
                {
                    var response2 = await hubConnection2.InvokeAsync<bool>("PingNextValidator");

                    if (response2 == true)
                    {
                        backup = true;
                        backupVal.FailCount = 0;
                        validators.Update(backupVal);
                        hubConnection2.StopAsync().Wait();
                        await hubConnection.DisposeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                backupVal.FailCount += 1;
                validators.Update(backupVal);
            }


            return (main, backup);
        }
        #endregion

        #region Call Crafter for block crafting
        public static async Task<bool> CallCrafter(Validators validator)
        {
            bool result = false;
            var validators = Validators.Validator.GetAll();
            var hubConnection = new HubConnectionBuilder().WithUrl("http://" + validator.NodeIP + ":" + Program.Port + "/blockchain").Build();
            try
            {
                var alive = hubConnection.StartAsync().Wait(3000); //inside a try as target can actively refuse it.
                if (alive == true)
                {
                    var response = await hubConnection.InvokeAsync<bool>("CallCrafter");

                    if (response == true)
                    {
                        result = true;
                        hubConnection.StopAsync().Wait();
                        await hubConnection.DisposeAsync();
                        return result;
                    }
                }
                else
                {
                    validator.FailCount += 1;
                    validator.IsActive = false;
                    validator.LastChecked = DateTime.UtcNow;
                    validators.Update(validator);
                }
            }
            catch (Exception ex)
            {
                validator.FailCount += 1;
                validator.IsActive = false;
                validator.LastChecked = DateTime.UtcNow;
                validators.Update(validator);
            }

            return result;
        }


        #endregion

        #region Get Newly crafted block
        public static async Task<Block?> GetNewlyCraftedBlock(long height, Validators validator)
        {
            Block block = null;
            var validators = Validators.Validator.GetAll();
            var hubConnection = new HubConnectionBuilder().WithUrl("http://" + validator.NodeIP + ":" + Program.Port + "/blockchain").Build();
            try
            {
                var alive = hubConnection.StartAsync().Wait(6000); //inside a try as target can actively refuse it.
                if (alive == true)
                {
                    block = await hubConnection.InvokeCoreAsync<Block>("SendBlock", args: new object?[] { height });
                    if (block != null)
                    {
                        hubConnection.StopAsync().Wait();
                        await hubConnection.DisposeAsync();
                        return block;
                    }
                    else
                    {
                        hubConnection.StopAsync().Wait();
                        await hubConnection.DisposeAsync();
                    }
                }
                else
                {
                    validator.FailCount += 1;
                    validator.IsActive = false;
                    validator.LastChecked = DateTime.UtcNow;
                    validators.Update(validator);
                }
            }
            catch (Exception ex)
            {
                validator.FailCount += 1;
                validator.IsActive = false;
                validator.LastChecked = DateTime.UtcNow;
                validators.Update(validator);
            }

            return block;
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

        #region Send Inactive Nodes

        public static async Task SendInactiveNodes(List<Validators> inactiveValidators)
        {
            var validators = Validators.Validator.GetAll();
            var activeValidators = validators.FindAll().Where(x => x.IsActive == true).ToList();
            var inacValiList = inactiveValidators.ToList();

            try
            {
                foreach (var inacValidator in inacValiList)
                {
                    await Parallel.ForEachAsync(activeValidators, async (validator, token) =>
                    {
                        try
                        {
                            var hubConnection = new HubConnectionBuilder().WithUrl("http://" + validator.NodeIP + ":" + Program.Port + "/blockchain").Build();
                            var alive = hubConnection.StartAsync().Wait(3000); //give validator 3 secs to connect. Should be plenty
                            if (alive == true)
                            {
                                await hubConnection.InvokeAsync("SendInactiveValidator", inacValidator);
                                hubConnection.StopAsync().Wait(3000);
                            }
                        }
                        catch (Exception ex)
                        {

                        }
                    });
                }
            }
            catch(Exception ex)
            {

            }
            
        }

        #endregion

        public static async Task BroadcastValidatorOnline(string address)
        {
            var validators = Validators.Validator.GetAll();
            var activeValidators = validators.FindAll().Where(x => x.IsActive == true).ToList();

            await Parallel.ForEachAsync(activeValidators, async (validator, token) =>
            {
                try
                {
                    var hubConnection = new HubConnectionBuilder().WithUrl("http://" + validator.NodeIP + ":" + Program.Port + "/blockchain").Build();
                    var alive = hubConnection.StartAsync().Wait(3000); //give validator 3 secs to connect. Should be plenty
                    if (alive == true)
                    {
                        await hubConnection.InvokeAsync("SendValidatorOnline", address);
                        hubConnection.StopAsync().Wait(3000);
                    }
                }
                catch (Exception ex)
                {

                }
            });
        }

        /// <summary>
        /// Methods below are obselete and will be removed after testing
        /// ///////////////////////////////////////////////////////////////////////////////////////////////////////////////
        /// </summary>      

        #region Get Validator List
        public static async Task<bool> GetValidatorList(bool isValidator = false)
        {
            //get seed validators
            var validators = Validators.Validator.ValidatorList;
            List<Validators>? validatorList = null;

            if(validators != null)
            {
                foreach(var validator in validators)
                {
                    var url = "http://" + validator.NodeIP + ":" + Program.Port +  "/blockchain";
                    var connection = new HubConnectionBuilder().WithUrl(url).Build();

                    connection.StartAsync().Wait();
                    validatorList = await connection.InvokeAsync<List<Validators>?>("SendValidators");

                    if(validatorList != null)
                    {
                        var dbValidator = Validators.Validator.GetAll();
                        var dbValidatorList = dbValidator.FindAll().ToList();
                        var insertList = validatorList.Except(dbValidatorList).ToList();
                        if(insertList.Count() != 0)
                        {
                            dbValidator.InsertBulk(insertList);
                            Validators.Validator.Initialize();
                            break;
                        }
                            
                    }
                }
                return true;
            }

            return false;
        }

        #endregion

        #region Get Validator Count
        public static async Task<long?> GetValidatorCount()
        {
            //get seed validators
            var validators = Validators.Validator.ValidatorList.Take(10);
            long? validatorCount = null;

            List<long> validatorCountList = new List<long>();
            
            if (validators != null)
            {
                foreach (var validator in validators)
                {
                    var url = "http://" + validator.NodeIP + ":" + Program.Port + "/blockchain";
                    var connection = new HubConnectionBuilder().WithUrl(url).Build();

                    connection.StartAsync().Wait();
                    validatorCount = await connection.InvokeAsync<long?>("SendValidatorCount");

                    if (validatorCount != null)
                    {
                        validatorCountList.Add((long)validatorCount);
                    }
                }

                return validatorCountList.Count() != 0 ? validatorCountList.Max() : null;
            }

            return null;
        }

        #endregion

        #region Get Next Validators
        public static async Task<string> GetNextValidators(Validators currentVal, Block block)
        {
            string output = "";

            //Modify list to not include yourself if there are more than 2 and to check past X amount of blocks for day to attempt to give everyone a chance.
            var validators = Validators.Validator.ValidatorList;

            //This will only really occur during start. Once chain has more validators this really won't occur, but just in case we only have 2 or less.
            if (validators.Count <= 2)
            {
                var nextValidators = "";
                var newVal = validators.Where(x => x.NodeIP != "SELF").FirstOrDefault();

                if(newVal != null)
                {
                    //need to do a request to see if more nodes exist.
                    return nextValidators = newVal.Address + ":" + currentVal.Address;
                }
                else
                {
                    //need to do a request to see if more nodes exist.
                    return nextValidators = currentVal.Address + ":" + currentVal.Address;
                }
            }

            //we take 2880 as that equals roughly the amount of blocks in 1 day.
            //If there are less validators than blocks a day, then this should give everyone a chance to get at least 1 block a day.
            //2 blocks every 1 minute. 120 every 1 hour. 2880 every 1 day
            //This promotes a more validators creation concept than giving more weight to validators in the randomization of selection.
            var blockchain = BlockchainData.GetBlocks();
            
            var validatorsList = validators.Where(x => x.NodeIP != "SELF" && x.EligibleBlockStart <= block.Height);
            List<string> blockValidators = new List<string>();
            if (validatorsList.Count() > 2880) 
            {
                //check time they were started.
                blockValidators = blockchain.Find(Query.All(Query.Descending)).Take(5760).Select(x => x.Validator).ToList();
            }
            else
            {
                blockValidators = blockchain.Find(Query.All(Query.Descending)).Take(2880).Select(x => x.Validator).ToList();
            }

            //Check for validators in blocks above!!!!!!!!!!!!!!! 
            

           

            if (validators != null)
            {
                foreach (var validator in validators)
                {
                    var url = "http://" + validator.NodeIP + ":" + Program.Port + "/blockchain";
                    var connection = new HubConnectionBuilder().WithUrl(url).Build();

                    connection.StartAsync().Wait();
                    string message = await connection.InvokeAsync<string>("RequestNextValidator");

                    if (message == "IVAT")
                    {
                        //success
                        
                    }
                    else if (message == "FTAV")
                    {
                        
                    }
                    else
                    {
                        //already in validator list
                    }
                }
                
            }

            return output;
        }

        #endregion
    }
}
