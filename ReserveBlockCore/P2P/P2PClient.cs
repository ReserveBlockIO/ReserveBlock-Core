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
            peers.ForEach(async x =>
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


                    //connection.On("BlockSent", (string message, Block nextBlock) =>
                    //{
                    //    Console.WriteLine(message + nextBlock.Validator);
                    //    if (nextBlock != null)
                    //    {
                    //        nBlock = nextBlock;
                    //    }
                    //});

                    

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

        #region Get Current Height of Node *Not working*
        public static long GetCurrentHeight()
        {
            var nBlock = new Block();
            long nHeight = 0;
            var peer = ActivePeerList.OrderByDescending(x => x.LastReach).FirstOrDefault();
            if (peer == null)
            {
                //Need peers
                return 0;
            }
            else
            {
                try
                {
                    var url = "http://" + peer.PeerIP + ":3338/blockchain";
                    var connection = new HubConnectionBuilder().WithUrl(url).Build();

                    connection.StartAsync().Wait();
                    connection.InvokeCoreAsync("GetBlockHeight", args: new object?[] { "Height" });
                    connection.On("BlockHeightSent", (long height, Block? block) =>
                    {
                        if (block != null)
                        {
                            nHeight = height;
                        }
                    });

                    return nHeight;
                }
                catch (Exception ex)
                {
                    var tempActivePeerList = new List<Peers>();
                    tempActivePeerList.AddRange(ActivePeerList);

                    //remove dead peer
                    tempActivePeerList.Remove(peer);

                    ActivePeerList.AddRange(tempActivePeerList);

                    return -1;
                }
            }
        }

        #endregion

        #region Send Transactions to mempool 
        public static async void SendTXMempool(Transaction txSend)
        {
            var peers = ActivePeerList.ToList();
            if(peers == null)
            {
                Console.WriteLine("You have no peers to send transaction too.");
            }
            else
            {
                foreach(var peer in peers)
                {
                    try
                    {
                        var url = "http://" + peer.PeerIP + ":3338/blockchain";
                        var connection = new HubConnectionBuilder().WithUrl(url).Build();

                        connection.StartAsync().Wait();
                        string message = await connection.InvokeCoreAsync<string>("SendToMempool", args: new object?[] { txSend });

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
                        var tempActivePeerList = new List<Peers>();
                        tempActivePeerList.AddRange(ActivePeerList);

                        //remove dead peer
                        tempActivePeerList.Remove(peer);

                        ActivePeerList.AddRange(tempActivePeerList); //update list with removed node
                        //if list gets below certain amount request more nodes.
                    }
                }
            }
        }

        #endregion

        public static async void SendTransactionMemPool(Transaction tx)
        {
            //broad out to all your known nodes.
        }
        
    }
}
