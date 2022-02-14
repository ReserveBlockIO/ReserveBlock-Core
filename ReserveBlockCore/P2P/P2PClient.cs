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
        public static void ConnectToPeers()
        {
            List<Peers> peers = new List<Peers>();
            peers = Peers.PeerList();

            List<Peers> tempActivePeerList = new List<Peers>();
            peers.ForEach(x =>
            {
                var peerIP = x.PeerIP != "192.168.1.63" ? x.PeerIP : "127.0.0.1";
                var url = "https://" + peerIP + ":3338/blockchain";
                var connection = new HubConnectionBuilder().WithUrl(url).Build();
                connection.StartAsync().Wait();
                connection.InvokeCoreAsync("ConnectPeers", args: new[] { "NodeIP", "Hello" });
                connection.On("PeerConnected", (string node, string message) =>
                {
                    Console.WriteLine(node + " - Message: " + message);
                    if(!ActivePeerList.Contains(x))
                        tempActivePeerList.Add(x);
                    Peers.UpdatePeerLastReach(x);
                });
            });
            //Update List
            ActivePeerList.AddRange(tempActivePeerList);
        }
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
                    var url = "https://" + peer.PeerIP + ":3338/blockchain";
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
        public static Block? GetBlock() //base example
        {
            var currentBlock = BlockchainData.GetLastBlock().Height;
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
                    var url = "https://" + peer.PeerIP + ":3338/blockchain";
                    var connection = new HubConnectionBuilder().WithUrl(url).Build();

                    connection.StartAsync().Wait();
                    connection.InvokeCoreAsync("SendBlock", args: new object?[] { -1 });
                    connection.On("BlockSent", (string message, Block? block) =>
                    {
                        if (block != null)
                        {
                            nBlock = block;
                        }
                    });

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
        public static void SendBlock(Block block) //base example
        {
            var connection = new HubConnectionBuilder().WithUrl("https://localhost:3338/blockchain").Build();

            connection.StartAsync().Wait();
            connection.InvokeCoreAsync("SendMessage", args: new[] { "NodeIP", "hello this is my message" });
            connection.On("BlockReceived", (string node, string message) => {
                Console.WriteLine(node + " - Message: " + message);
            });
        }
    }
}
