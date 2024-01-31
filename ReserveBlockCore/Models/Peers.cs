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
        public bool IsPermaBanned { get; set; }
        public bool IsValidator { get; set; }
        public int BanCount { get; set; }
        public DateTime? InitialBanDate { get; set; }
        public DateTime? LastBanDate { get; set; }
        public DateTime? NextUnbanDate { get; set; }
        public List<string>? BannedFromAreasList { get; set; }
        public string? LastBannedFromArea { get; set; }
        public string? WalletVersion { get; set; }
        public static IEnumerable<Peers> PeerList(bool isBanned = false)
        {
            var peerList = GetAll();
            if(peerList != null && !isBanned)
            {
                return peerList.Query().Where(x => !x.IsBanned && !x.IsPermaBanned).ToEnumerable();
            }
            else
            {
                return peerList.Query().Where(x => true).ToEnumerable();
            }
        }

        public static LiteDB.ILiteCollection<Peers>? GetAll()
        {
            try
            {
                var peers = DbContext.DB_Peers.GetCollection<Peers>(DbContext.RSRV_PEERS);
                return peers;
            }
            catch(Exception ex)
            {                
                ErrorLogUtility.LogError(ex.ToString(), "Peers.GetAll()");
                return null;
            }
            
        }

        public static Peers? GetPeer(string ip)
        {
            var peers = GetAll();
            if(peers != null)
            {
                var peer = peers.Query().Where(x => x.PeerIP == ip).FirstOrDefault();
                if(peer != null)
                {
                    return peer;
                }
                else
                {
                    return null;
                }
            }

            return null;
        }

        public static int BannedPeers()
        {
            int banned = 0;

            var peers = GetAll();

            var bannedPeers = peers.Find(x => x.IsBanned || x.IsPermaBanned).ToList();

            banned = bannedPeers.Count();

            return banned;
        }

        public static async Task UpdatePeerAsVal(string ip)
        {
            var peers = GetAll();
            var peer = peers?.Query().Where(x => x.PeerIP.Equals(ip)).FirstOrDefault();
            if(peer != null)
            {
                peer.IsValidator = true;
                peers?.Update(peer);
            }
        }

        public static List<Peers> ListBannedPeers()
        {
            var peers = GetAll();

            var bannedPeers = peers.Find(x => x.IsBanned || x.IsPermaBanned).ToList();

            return bannedPeers;
        }

        public static async Task<int> UnbanAllPeers(bool unbanPerma = false)
        {
            Globals.BannedIPs.Clear();
            Globals.MessageLocks.Clear();
            var peers = GetAll();
            var bannedPeers = peers.Query().Where(x => x.IsBanned && !x.IsPermaBanned).ToEnumerable();
            if(unbanPerma)
                bannedPeers = peers.Query().Where(x => x.IsBanned || x.IsPermaBanned).ToEnumerable();
            var count = 0;
            foreach(var peer in bannedPeers)
            {
                peer.IsBanned = false;
                peer.IsPermaBanned = unbanPerma ? false : peer.IsPermaBanned;
                peers.UpdateSafe(peer);
                count += 1;
            }

            return count;
        }

    }

}
