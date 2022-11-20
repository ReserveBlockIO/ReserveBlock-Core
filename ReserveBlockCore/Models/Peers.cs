using ReserveBlockCore.Extensions;
using ReserveBlockCore.Data;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace ReserveBlockCore.Models
{
    public class Peers
    {
        public long Id { get; set; }
        public string PeerIP { get; set; }
        public bool IsIncoming { get; set; }
        public bool IsOutgoing { get; set; }
        public int FailCount { get; set; }
        public bool IsBanned { get; set; }
        public static List<Peers> PeerList(bool isBanned = false)
        {
            
            var peerList = GetAll();
            if(peerList.Count() == 0)
            {
                return peerList.FindAll().ToList();
            }
            else
            {
                return peerList.FindAll().ToList();
            }

        }

        public static LiteDB.ILiteCollection<Peers> GetAll()
        {
            try
            {
                var peers = DbContext.DB_Peers.GetCollection<Peers>(DbContext.RSRV_PEERS);
                return peers;
            }
            catch(Exception ex)
            {
                DbContext.Rollback();
                ErrorLogUtility.LogError(ex.ToString(), "Peers.GetAll()");
                return null;
            }
            
        }

        public static int BannedPeers()
        {
            int banned = 0;

            var peers = GetAll();

            var bannedPeers = peers.Find(x => x.IsBanned == true).ToList();

            banned = bannedPeers.Count();

            return banned;
        }

        public static List<Peers> ListBannedPeers()
        {
            var peers = GetAll();

            var bannedPeers = peers.Find(x => x.IsBanned == true).ToList();

            return bannedPeers;
        }

        public static async Task<int> UnbanAllPeers()
        {
            var peers = GetAll();
            var bannedPeers = peers.Find(x => x.IsBanned == true).ToList();
            var count = 0;
            foreach(var peer in bannedPeers)
            {
                peer.IsBanned = false;
                Globals.BannedIPs[peer.PeerIP] = false;
                peers.UpdateSafe(peer);
                count += 1;
            }

            return count;
        }

        public static async Task<string> UnbanPeer(string ipAddress)
        {
            try
            {
                Globals.BannedIPs[ipAddress] = false;
                var peerDb = Peers.GetAll();
                var peer = peerDb.FindOne(x => x.PeerIP == ipAddress);
                if (peer != null)
                {
                    peer.IsBanned = false;
                    peerDb.UpdateSafe(peer);

                    return "Peer has been unbanned";
                }

                return "Peer not found";
            }
            catch { }

            return "Peer not found";
        }

        public static void BanPeer(string ipAddress, string message, string location)
        {
            if (Globals.AdjNodes.ContainsKey(ipAddress))
                return;
            //Globals.BannedIPs[ipAddress] = true;
            //var peerDb = Peers.GetAll();
            //var peer = peerDb.FindOne(x => x.PeerIP == ipAddress);
            BanLogUtility.Log(message, location);
            //if (peer != null)
            //{
            //    peer.IsBanned = true;
            //    peerDb.UpdateSafe(peer);                
            //}
            //else
            //    peerDb.InsertSafe(new Peers { PeerIP = ipAddress, IsBanned = true });

            //if (Globals.P2PPeerList.TryRemove(ipAddress, out var context))            
            //    context.Abort();


            //if (Globals.AdjPeerList.TryRemove(ipAddress, out var context2))
            //    context2.Abort();

            //if (Globals.Nodes.TryRemove(ipAddress, out NodeInfo node))
            //    node.Connection.DisposeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public static void UpdatePeerLastReach(Peers incPeer)
        {
            var peers = GetAll();
            var peer = GetAll().FindOne(x => x.PeerIP == incPeer.PeerIP);
            if(peer != null)
            {
                //peer.LastReach = DateTime.UtcNow;
                peers.UpdateSafe(peer);
            }
            else
            {
                Peers nPeer = new Peers { 
                    //ChainRefId = incPeer.ChainRefId,
                    //LastReach = DateTime.UtcNow,
                    PeerIP = incPeer.PeerIP,
                };

                peers.InsertSafe(nPeer);
            }
        }
    }

}
