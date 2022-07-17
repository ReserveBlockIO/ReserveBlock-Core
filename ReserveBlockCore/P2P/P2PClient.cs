using LiteDB;
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

namespace ReserveBlockCore.P2P
{
    public class P2PClient
    {
        #region Static Variables
        public static List<Peers>? ActivePeerList { get; set; }
        public static List<string> ReportedIPs = new List<string>();
        public static long LastSentBlockHeight = -1;
        public static DateTime? AdjudicatorConnectDate = null;
        public static DateTime? LastTaskSentTime = null;
        public static DateTime? LastTaskResultTime = null;
        public static long LastTaskBlockHeight = 0;
        public static bool LastTaskError = false;
        public static Dictionary<int, string>? NodeDict { get; set; }

        #endregion

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

                LogUtility.Log("Connecting to Adjudicator", "ConnectAdjudicator()");

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

                AdjudicatorConnectDate = DateTime.UtcNow;

                hubAdjConnection1.On<string, string>("GetAdjMessage", async (message, data) => {
                    if (message == "task" || message == "taskResult" || message == "fortisPool" || message == "status" || message == "tx" || message == "badBlock")
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
                                ValidatorLogUtility.Log("Connected to Validator Pool", "P2PClient.ConnectAdjudicator()", true);
                                LogUtility.Log("Success! Connected to Adjudicator", "ConnectAdjudicator()");
                                break;
                            case "tx":
                                await ValidatorProcessor.ProcessData(message, data);
                                break;
                            case "badBlock":
                                //do something
                                break;
                        }
                    }
                });

                hubAdjConnection1.StartAsync().Wait();

            }
            catch (Exception ex)
            {
                ValidatorLogUtility.Log("Failed! Connecting to Adjudicator: Reason - " + ex.Message, "ConnectAdjudicator()");
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
                    if (peers.Count() > 6) //if peer db larger than 8 records get only 8 and use those records. we only start with low fail count.
                    {
                        Random rnd = new Random();
                        var peerList = peers.Where(x => x.FailCount <= 1 && x.IsOutgoing == true).OrderBy(x => rnd.Next()).Take(6).ToList();
                        if (peerList.Count() >= 6)
                        {
                            peers = peerList;
                        }
                        else
                        {
                            peers = peers.Where(x => x.IsOutgoing == true).OrderBy(x => rnd.Next()).Take(6).ToList();
                            if(peers.Count() < 6)
                            {
                                var count = 6 - peers.Count();
                                var peersAll = peers.Where(x => x.IsOutgoing == false).OrderBy(x => rnd.Next()).Take(count).ToList();
                                peersAll.AddRange(peers);
                                peers = peersAll;
                            }
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
                                    peer.IsOutgoing = true;
                                    peer.FailCount = 0; //peer responded. Reset fail count
                                    peerDB.Update(peer);
                                }
                                else
                                {
                                    //peer.FailCount += 1;
                                    //peerDB.Update(peer);
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
                                LastTaskError = false;
                                LastTaskSentTime = DateTime.Now;
                                LastSentBlockHeight = taskAnswer.Block.Height;
                            }
                            else
                            {
                                LastTaskError = true;
                                ValidatorLogUtility.Log("Block passed validation, but received a false result from adjudicator and failed.", "P2PClient.SendTaskAnswer()");
                            }
                        }
                    }
                }
                catch(Exception ex)
                {
                    LastTaskError = true;

                    ValidatorLogUtility.Log("Unhandled Error Sending Task. Check Error Log for more details.", "P2PClient.SendTaskAnswer()");

                    string errorMsg = string.Format("Error Sending Task - {0}. Error Message : {1}", taskAnswer != null ? 
                        taskAnswer.SubmitTime.ToString() : "No Time", ex.Message);
                    ErrorLogUtility.LogError(errorMsg, "SendTaskAnswer(TaskAnswer taskAnswer)");
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
            if (adjudicatorConnected == true && hubAdjConnection1 != null)
            {
                try
                {
                    var result = await hubAdjConnection1.InvokeCoreAsync<bool>("ReceiveTX", args: new object?[] { tx });
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
                    var url = "http://" + adjudicator.NodeIP + ":" + Program.Port + "/adjudicator";
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
        public static async Task<List<Block>> GetBlock() //base example
        {
            var currentBlock = Program.BlockHeight != -1 ? Program.LastBlock.Height : -1; //-1 means fresh client with no blocks
            var nBlock = new Block();
            List<Block> blocks = new List<Block>();
            var peersConnected = await ArePeersConnected();

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
                    HandleDisconnectedNode(1, hubConnection1);
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
                    HandleDisconnectedNode(2, hubConnection2);
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
                    HandleDisconnectedNode(3, hubConnection3);
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
                    HandleDisconnectedNode(4, hubConnection4);
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
                    HandleDisconnectedNode(5, hubConnection5);
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
                    HandleDisconnectedNode(6, hubConnection6);
                }

            }

            return result;
        }

        #endregion

        #region HandleDisconnectedNode
        private static void HandleDisconnectedNode(int nodeNum, HubConnection? _hubConnection)
        {
            try
            {
                var nodes = Program.Nodes;
                var nodeList = nodes.ToList();

                var nodeInfo = NodeDict[nodeNum];
                if (nodeInfo != null)
                {
                    var node = nodeList.Where(x => x.NodeIP == nodeInfo).FirstOrDefault(); //null error happened here meaning nodeList was null
                    if (node != null)
                    {
                        Program.Nodes.Remove(node);
                    }
                    _hubConnection = null;
                    NodeDict[nodeNum] = null;
                }
            }
            catch (Exception ex)
            {
                string errorMsg = string.Format("Error handling disconnected node: {0}. Error Message: {1}", nodeNum.ToString(), ex.Message);
                ErrorLogUtility.LogError(errorMsg, "P2PClient.HandleDisconnectedNode()");
            }
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

        #region File Upload To Beacon Beacon

        public static async Task<string> BeaconUploadRequest(List<string> locators, List<string> assets, string scUID, string nextOwnerAddress, string preSigned = "NA")
        {
            var result = "Fail";
            string signature = "";
            string locatorRetString = "";
            var scState = SmartContractStateTrei.GetSmartContractState(scUID);
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
                        BigInteger b1 = BigInteger.Parse(account.PrivateKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
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
                Assets = assets,
                SmartContractUID = scUID,
                Signature = signature,
                NextAssetOwnerAddress = nextOwnerAddress
            };
            foreach(var locator in locators)
            {
                try
                {
                    var beaconString = locator.ToStringFromBase64();
                    var beacon = JsonConvert.DeserializeObject<BeaconInfo.BeaconInfoJson>(beaconString);

                    var url = "http://" + beacon.IPAddress + ":" + Program.Port + "/blockchain";
                    var _tempHubConnection = new HubConnectionBuilder().WithUrl(url).Build();
                    var alive = _tempHubConnection.StartAsync();
                    var response = await _tempHubConnection.InvokeCoreAsync<bool>("ReceiveUploadRequest", args: new object?[] { bsd });
                    if (response != true)
                    {
                        var errorMsg = string.Format("Failed to talk to beacon.");
                        ErrorLogUtility.LogError(errorMsg, "P2PClient.BeaconUploadRequest(List<BeaconInfo.BeaconInfoJson> locators, List<string> assets, string scUID) - try");
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
                        if(locatorRetString == "")
                        {
                            foreach(var asset in bsd.Assets)
                            {
                                var path = NFTAssetFileUtility.NFTAssetPath(asset, bsd.SmartContractUID);
                                BeaconResponse rsp = BeaconClient.Send(asset, beacon.IPAddress, beacon.Port);
                                if (rsp.Status == 1)
                                {
                                    //success
                                }
                            }
                            
                            locatorRetString = locator;
                        }
                        else
                        {
                            locatorRetString = locatorRetString + "," + locator;
                        }

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
                        BigInteger b1 = BigInteger.Parse(account.PrivateKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
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
            };

            foreach (var locator in locators)
            {
                try
                {
                    var beaconString = locator.ToStringFromBase64();
                    var beacon = JsonConvert.DeserializeObject<BeaconInfo.BeaconInfoJson>(beaconString);

                    var url = "http://" + beacon.IPAddress + ":" + Program.Port + "/blockchain";
                    var _tempHubConnection = new HubConnectionBuilder().WithUrl(url).Build();
                    var alive = _tempHubConnection.StartAsync();

                    var response = await _tempHubConnection.InvokeCoreAsync<bool>("ReceiveDownloadRequest", args: new object?[] { bdd });
                    if (response != true)
                    {
                        var errorMsg = string.Format("Failed to talk to beacon.");
                        ErrorLogUtility.LogError(errorMsg, "P2PClient.BeaconUploadRequest(List<BeaconInfo.BeaconInfoJson> locators, List<string> assets, string scUID) - try");
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

                        int failCount = 0;
                        foreach (var asset in bdd.Assets)
                        {
                            var path = NFTAssetFileUtility.CreateNFTAssetPath(asset, bdd.SmartContractUID);
                            BeaconResponse rsp = BeaconClient.Receive(asset, beacon.IPAddress, beacon.Port, scUID);
                            if (rsp.Status == 1)
                            {
                                //success
                            }
                            else
                            {
                                failCount += 1;
                            }
                        }

                        if(failCount == 0)
                        {
                            result = true;
                            break;
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

        #region Get Beacon Status of Nodes
        public static async Task<List<string>> GetBeacons()
        {
            List<string> BeaconList = new List<string>();

            var peersConnected = await ArePeersConnected();

            int foundBeaconCount = 0;

            if (peersConnected.Item1 == false)
            {
                //Need peers
                return BeaconList;
            }
            else
            {
                try
                {
                    if(foundBeaconCount < 2)
                    {
                        if (hubConnection1 != null && IsConnected1)
                        {
                            string beaconInfo = await hubConnection1.InvokeAsync<string>("SendBeaconInfo");
                            if (beaconInfo != "NA")
                            {
                                BeaconList.Add(beaconInfo);
                                foundBeaconCount += 1;
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
                    if (hubConnection2 != null && IsConnected2 && foundBeaconCount < 2)
                    {
                        string beaconInfo = await hubConnection2.InvokeAsync<string>("SendBeaconInfo");
                        if (beaconInfo != "NA")
                        {
                            BeaconList.Add(beaconInfo);
                            foundBeaconCount += 1;
                        }

                    }
                }
                catch (Exception ex)
                {
                    //node is offline
                }

                try
                {
                    if (hubConnection3 != null && IsConnected3 && foundBeaconCount < 2)
                    {
                        string beaconInfo = await hubConnection3.InvokeAsync<string>("SendBeaconInfo");
                        if (beaconInfo != "NA")
                        {
                            BeaconList.Add(beaconInfo);
                            foundBeaconCount += 1;
                        }
                    }
                }
                catch (Exception ex)
                {
                    //node is offline
                }

                try
                {
                    if (hubConnection4 != null && IsConnected4 && foundBeaconCount < 2)
                    {
                        string beaconInfo = await hubConnection4.InvokeAsync<string>("SendBeaconInfo");
                        if (beaconInfo != "NA")
                        {
                            BeaconList.Add(beaconInfo);
                            foundBeaconCount += 1;
                        }
                    }
                }
                catch (Exception ex)
                {
                    //node is offline
                }

                try
                {
                    if (hubConnection5 != null && IsConnected5 && foundBeaconCount < 2)
                    {
                        string beaconInfo = await hubConnection5.InvokeAsync<string>("SendBeaconInfo");
                        if (beaconInfo != "NA")
                        {
                            BeaconList.Add(beaconInfo);
                            foundBeaconCount += 1;
                        }
                    }
                }
                catch (Exception ex)
                {
                    //node is offline
                }

                try
                {
                    if (hubConnection6 != null && IsConnected6 && foundBeaconCount < 2)
                    {
                        string beaconInfo = await hubConnection6.InvokeAsync<string>("SendBeaconInfo");
                        if (beaconInfo != "NA")
                        {
                            BeaconList.Add(beaconInfo);
                            foundBeaconCount += 1;
                        }
                    }

                }
                catch (Exception ex)
                {
                    //node is offline
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
            var peersConnected = await ArePeersConnected();

            if (peersConnected.Item1 == false)
            {
                //Need peers
                Console.WriteLine("Failed to broadcast Transaction. No peers are connected to you.");
                LogUtility.Log("TX failed. No Peers: " + txSend.Hash, "P2PClient.SendTXMempool()");
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
