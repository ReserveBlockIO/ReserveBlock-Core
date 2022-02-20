using LiteDB;
using Microsoft.AspNetCore.SignalR.Client;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Nodes;
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
        public static async Task<bool> ConnectToPeers()
        {
            List<Peers> peers = new List<Peers>();
            peers = Peers.PeerList();

            ActivePeerList = new List<Peers>();

            int successCount = 0;
           
            var peerDB = Peers.GetAll();

            if(peers.Count() == 0)
            {
                NodeConnector.StartNodeConnecting();
                peers = Peers.PeerList();
            }

            if (peers.Count() > 0)
            {
                if(peers.Count() > 8) //if peer db larger than 8 records get only 8 and use those records. we only start with low fail count.
                {
                    Random rnd = new Random();
                    var peerList = peers.Where(x => x.FailCount <= 1 && x.IsOutgoing == true).OrderBy(x => rnd.Next()).Take(8).ToList();
                    if(peerList.Count() >= 2)
                    {
                        peers = peerList;
                    }
                    else
                    {
                        peers = peers.Where(x => x.FailCount <= 4 && x.IsOutgoing == true).OrderBy(x => rnd.Next()).Take(8).ToList();
                    }
                    
                }
                foreach (var peer in peers)
                {
                    try
                    {
                        var url = "http://" + peer.PeerIP + ":3338/blockchain";
                        var connection = new HubConnectionBuilder().WithUrl(url).Build();
                        string response = "";

                        var conResult = connection.StartAsync().Wait(5000);//giving peer 5 seconds to respond.
                        if (conResult == false)
                            return false;

                        response = await connection.InvokeAsync<string>("PingPeers");

                        if(response == "HelloPeer")
                        {
                            successCount += 1;
                            peer.FailCount = 0; //peer responded. Reset fail count
                            peerDB.Update(peer);
                            ActivePeerList.Add(peer);//adds peer to active list.
                        }
                        else
                        {
                            peer.FailCount += 1;
                            peerDB.Update(peer);
                        }
                        
                    }
                    catch (Exception ex)
                    {
                        //peer did not response correctly or at all
                        peer.FailCount += 1;
                        peerDB.Update(peer);
                    }
                    
                }
                if(successCount > 0)
                    return true;
            }

            return false;
        }

        public static async Task<bool> PingBackPeer(string peerIP)
        {
            try
            {
                var url = "http://" + peerIP + ":3338/blockchain";
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

        public static async Task<bool> PeerHealthCheck()
        {
            bool result = false;

            var peers = ActivePeerList;

            int successCount = 0;

            var peerDB = Peers.GetAll();

            if (peers != null)
            {
                if (peers.Count() == 0)
                {
                    NodeConnector.StartNodeConnecting();
                    peers = Peers.PeerList();
                    ActivePeerList = peers;//add new peers to active list

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
                            peers = peers.Where(x => x.FailCount <= 4 && x.IsOutgoing == true).OrderBy(x => rnd.Next()).Take(8).ToList();
                        }

                        foreach (var peer in peers)
                        {
                            try
                            {
                                var url = "http://" + peer.PeerIP + ":3338/blockchain";
                                var connection = new HubConnectionBuilder().WithUrl(url).Build();
                                string response = "";

                                var conResult = connection.StartAsync().Wait(5000);//giving peer 5 seconds to respond.
                                if (conResult == false)
                                    return false;

                                response = await connection.InvokeAsync<string>("PingPeers");

                                if (response == "HelloPeer")
                                {
                                    successCount += 1;
                                    peer.FailCount = 0; //peer responded. Reset fail count
                                    peerDB.Update(peer);
                                    ActivePeerList.Add(peer);//adds peer to active list.
                                }
                                else
                                {
                                    peer.FailCount += 1;
                                    peerDB.Update(peer);
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
                }

            }

            return result;
        }


        #region Get Block
        public static async Task<Block?> GetBlock() //base example
         {
            var currentBlock = BlockchainData.GetLastBlock() != null ? BlockchainData.GetLastBlock().Height : -1; //-1 means fresh client with no blocks
            var nBlock = new Block();
            var peer = ActivePeerList.OrderByDescending(x => x.FailCount).FirstOrDefault();//might need to reverse this

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
                    var url = "http://" + validator.NodeIP + ":3338/blockchain";
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
                    var url = "http://" + validator.NodeIP + ":3338/blockchain";
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

        #region Get Current Height of Nodes
        public static async Task<(bool, long)> GetCurrentHeight()
        {
            bool newHeightFound = false;
            long height = 0;

            var blocks = BlockchainData.GetBlocks();
            blocks.EnsureIndex(x => x.Height);
            if (blocks.Count() == 0)
                return (true, -1);

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
                validators = Validators.Validator.GetAll().FindAll().Where(x => x.NodeIP != "SELF").Take(10).ToList(); //grab 10 validators to send to, those 10 will then send to 10, etc.
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
                    var url = "http://" + validator.NodeIP + ":3338/blockchain";
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
