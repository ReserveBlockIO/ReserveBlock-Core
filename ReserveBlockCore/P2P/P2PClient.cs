using Microsoft.AspNetCore.SignalR.Client;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReserveBlockCore.P2P
{
    public class P2PClient
    {
        public static List<Peers> ActivePeerList { get; set; }

        public static HubConnectionBuilder con = new HubConnectionBuilder();

        #region Local Test
        public static void TestLocal()
        {
            var connection = new HubConnectionBuilder().WithUrl("http://localhost:3338/blockchain").Build();

            connection.StartAsync().Wait();
            connection.InvokeCoreAsync("ConnectPeers", args: new[] { "Local", "Hello", DateTime.UtcNow.Ticks.ToString() });
            connection.On("PeerConnected", (string node, string message, string latency, string chainRef) =>
            {
                Console.WriteLine(node + " - Message: " + message + " latency: " + latency + " ms");
            });
        }

        #endregion

        #region Connect to Peers
        public static void ConnectToPeers()
        {
            ActivePeerList = new List<Peers>();

            List<Peers> peers = new List<Peers>();
            peers = Peers.PeerList();

            List<Peers> tempActivePeerList = new List<Peers>();
            peers.ForEach(x =>
            {
                try
                {
                    var peerIP = x.PeerIP;
                    var url = "http://" + peerIP + ":3338/blockchain";
                    var connection = new HubConnectionBuilder().WithUrl(url).Build();
                    connection.StartAsync().Wait();
                    connection.InvokeCoreAsync("ConnectPeers", args: new[] { "NodeIP", "Hello", DateTime.UtcNow.Ticks.ToString() });
                    connection.On("PeerConnected", (string node, string message, string latency, string chainRef) =>
                    {
                        Console.WriteLine(node + " - Message: " + message + " latency: " + latency + " ms");

                    });

                    if (!ActivePeerList.Contains(x))
                        tempActivePeerList.Add(x);
                    Peers.UpdatePeerLastReach(x);
                }
                catch(Exception ex)
                {

                }
            });
            //Update List
            ActivePeerList.AddRange(tempActivePeerList);
        }

        #endregion

        #region Get Block
        public static async Task<Block?> GetBlock() //base example
         {
            var currentBlock = BlockchainData.GetLastBlock() != null ? BlockchainData.GetLastBlock().Height : -1; //-1 means fresh client with no blocks
            var nBlock = new Block();
            var peer = ActivePeerList.OrderByDescending(x => x.LastReach).FirstOrDefault();

            if(peer == null)
            {
                //Need peers
                return null;
            }
            else
            {
                try
                {
                    var url = "http://" + peer.PeerIP + ":3338/blockchain";
                    var connection = new HubConnectionBuilder().WithUrl(url).Build();

                    connection.StartAsync().Wait();
                    nBlock = await connection.InvokeCoreAsync<Block>("SendBlock", args: new object?[] { currentBlock });

                    return nBlock;
                }
                catch (Exception ex)
                {
                    var tempActivePeerList = new List<Peers>();
                    tempActivePeerList.AddRange(ActivePeerList);

                    //remove dead peer
                    tempActivePeerList.Remove(peer);

                    ActivePeerList.AddRange(tempActivePeerList);

                    return null;
                }
            }
            
        }

        #endregion

        #region Get Current Height of Nodes
        public static async Task<(bool, long)> GetCurrentHeight()
        {
            bool newHeightFound = false;
            long height = 0;
            long myHeight = BlockchainData.GetHeight();
            var peers = ActivePeerList.ToList();
            var validators = Validators.Validator.ValidatorList;

            if (peers == null)
            {
                //can't get height without peers
            }
            else
            {
                foreach (var peer in peers)
                {
                    try
                    {
                        var url = "http://" + peer.PeerIP + ":3338/blockchain";
                        var connection = new HubConnectionBuilder().WithUrl(url).Build();

                        connection.StartAsync().Wait();
                        long remoteNodeHeight = await connection.InvokeAsync<long>("SendBlockHeight");

                        if(myHeight < remoteNodeHeight)
                        {
                            newHeightFound = true;
                            height = remoteNodeHeight;
                            break; // go ahead and stop and get new block.
                        }

                    }
                    catch (Exception ex) //this means no repsosne from node
                    {
                        var tempActivePeerList = new List<Peers>();
                        tempActivePeerList.AddRange(ActivePeerList);

                        //remove dead peer
                        tempActivePeerList.Remove(peer);

                        ActivePeerList.AddRange(tempActivePeerList); //update list with removed node
                        //if list gets below certain amount request more nodes.
                    }
                }
            }

            return (newHeightFound, height);
        }

        #endregion

        #region Send Transactions to mempool 
        public static async void SendTXMempool(Transaction txSend, List<string>? ipList)
        {
            var validators = Validators.Validator.ValidatorList;

            if (ipList != null)
            {
                validators = Validators.Validator.GetAll().FindAll().Where(x => !ipList.Any(y => y == x.NodeIP)).Take(10).ToList();
            }
            else
            {
                //this will only happen when new node is being broadcasted by its crafter.
                validators = Validators.Validator.GetAll().FindAll().Take(10).ToList(); //grab 10 validators to send to, those 10 will then send to 10, etc.
            }

            if (validators == null)
            {
                Console.WriteLine("You have no peers to send transaction too.");
            }
            else
            {
                var vSendList = new List<string>();

                validators.ForEach(x => {
                    vSendList.Add(x.NodeIP);
                });

                if (ipList != null)
                {
                    vSendList.AddRange(ipList);
                }
                foreach (var peer in validators)
                {
                    try
                    {
                        var url = "http://" + peer.NodeIP + ":3338/blockchain";
                        var connection = new HubConnectionBuilder().WithUrl(url).Build();

                        connection.StartAsync().Wait();
                        string message = await connection.InvokeCoreAsync<string>("SendToMempool", args: new object?[] { txSend, vSendList });

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
                         //update list with removed node
                        //if list gets below certain amount request more nodes.
                    }
                }


            }
        }

        #endregion

        #region Broadcast Masternode
        public static async void BroadcastMasterNode(Validators nValidator)
        {
            var peers = ActivePeerList.ToList();
            var validators = Validators.Validator.ValidatorList;
            if (peers == null)
            {
                Console.WriteLine("You have no peers to send node info too.");
            }
            else
            {
                foreach (var peer in peers)
                {
                    var url = "http://" + peer.PeerIP + ":3338/blockchain";
                    var connection = new HubConnectionBuilder().WithUrl(url).Build();

                    connection.StartAsync().Wait();
                    string message = await connection.InvokeCoreAsync<string>("SendValidator", args: new object?[] { nValidator });

                    if (message == "VATN")
                    {
                        //success
                        Validators.Validator.Initialize();
                    }
                    else if (message == "FTAV")
                    {
                        Console.WriteLine("Transaction Failed Verification Process on remote node");
                    }
                    else
                    {
                        //already in validator list
                    }
                }
            }
            if(validators != null)
            {
                Console.WriteLine("Sending your node info to all validators. Please note this may take a moment.");
                foreach (var validator in validators)
                {
                    var url = "http://" + validator.NodeIP + ":3338/blockchain";
                    var connection = new HubConnectionBuilder().WithUrl(url).Build();

                    connection.StartAsync().Wait();
                    string message = await connection.InvokeCoreAsync<string>("SendValidator", args: new object?[] { nValidator });

                    if (message == "VATN")
                    {
                        //success
                        Validators.Validator.Initialize();
                    }
                    else if (message == "FTAV")
                    {
                        Console.WriteLine("Transaction Failed Verification Process on remote node");
                    }
                    else
                    {
                        //already in validator list
                    }
                }
                Console.WriteLine("Done Sending. Thank you for joining the RBX Network!");
            }
        }
        #endregion

        #region Broadcast Blocks to Validators
        public static async void BroadcastBlock(Block block, List<string>? ipList)
        {
            var peers = ActivePeerList.ToList();
            var validators = new List<Validators>();

            if(ipList != null)
            {
                validators = Validators.Validator.GetAll().FindAll().Where(x => !ipList.Any(y => y == x.NodeIP)).Take(10).ToList();
            }
            else
            {
                //this will only happen when new node is being broadcasted by its crafter.
                validators = Validators.Validator.GetAll().FindAll().Take(10).ToList(); //grab 10 validators to send to, those 10 will then send to 10, etc.
            }
            
            var vSendList = new List<string>();

            validators.ForEach(x => {
                vSendList.Add(x.NodeIP);
            });

            //Also add previous list so others do not broadcast to them. If they miss broadcast they can call out for a block at any time.
            if(ipList != null)
            {
                vSendList.AddRange(ipList); 
            }

            foreach(var validator in validators)
            {
                var url = "http://" + validator.NodeIP + ":3338/blockchain";
                var connection = new HubConnectionBuilder().WithUrl(url).Build();

                connection.StartAsync().Wait();
                string message = await connection.InvokeCoreAsync<string>("ReceiveBlock", args: new object?[] { block, vSendList });
            }

        }
        #endregion
    }
}
