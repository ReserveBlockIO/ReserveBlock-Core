using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;


namespace ReserveBlockCore.P2P
{
    public class P2PServer : Hub
    {
        public async Task GetBlock(Block nextBlock, string message)
        {

            await Clients.All.SendAsync("BlockReceived", message);
        }

        //Send hello status to connecting peers from p2p server
        public async Task ConnectPeers(string node, string message, string time)
        {
            long ticks = Convert.ToInt64(time);
            DateTime timeTicks = new DateTime(ticks);

            var feature = Context.Features.Get<IHttpConnectionFeature>();
            var peerIP = feature.RemoteIpAddress.MapToIPv4().ToString();

            if (message == "Hello")
            {
                var oNode = "Origin Node";
                var oMessage = "Connected to IP: " + peerIP;
                var endTime = DateTime.UtcNow;
                var totalTime = (endTime - timeTicks).TotalMilliseconds;
                await Clients.All.SendAsync("PeerConnected", oNode, oMessage, totalTime.ToString("0"), BlockchainData.ChainRef);
            }
            
        }
        //Send Block to client from p2p server
        public async Task<Block?> SendBlock(long currentBlock)
        {
            var peerIP = GetIP(Context);

            var message = "";
            var nextBlockHeight = currentBlock + 1;
            var nextBlock = BlockchainData.GetBlockByHeight(nextBlockHeight);

            
            if (nextBlock != null)
            {
                return nextBlock;
            }
            else
            {
                return null;
            }

        }
        public async Task SharePeers(string node)
        {
            var peers = P2PClient.ActivePeerList;
            var message = "";

            if(peers == null)
            {
                message = "NoPeers";
                await Clients.All.SendAsync("PeersShared", null, message);
            }
            else
            {
                message = "PeersFound";
                await Clients.All.SendAsync("PeersShared", peers, message);
            }

            
        }

        private static string GetIP(HubCallerContext context)
        {
            var feature = context.Features.Get<IHttpConnectionFeature>();
            var peerIP = feature.RemoteIpAddress.MapToIPv4().ToString();

            return peerIP;
        }
    }
}
