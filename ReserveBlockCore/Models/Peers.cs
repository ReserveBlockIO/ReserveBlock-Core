using LiteDB;
using ReserveBlockCore.Data;
using ReserveBlockCore.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReserveBlockCore.Models
{
    public class Peers
    {
        public long Id { get; set; }
        public string PeerIP { get; set; }
        public DateTime LastReach { get; set; }
        public string ChainRefId { get; set; }

        public static List<Peers> PeerList()
        {
            var peerList = GetAll();
            if(peerList.Count() == 0)
            {
                var seedList = SeedNodeService.SeedNodes();
                peerList.InsertBulk(seedList);
                return peerList.FindAll().ToList();
            }
            else
            {
                return peerList.FindAll().ToList();
            }

        }

        public static ILiteCollection<Peers> GetAll()
        {
            var peers = DbContext.DB_Peers.GetCollection<Peers>(DbContext.RSRV_PEERS);
            return peers;
        }

        public static void UpdatePeerLastReach(Peers incPeer)
        {
            var peers = GetAll();
            var peer = GetAll().FindOne(x => x.PeerIP == incPeer.PeerIP);
            if(peer != null)
            {
                peer.LastReach = DateTime.UtcNow;
                peers.Update(peer);
            }
            else
            {
                Peers nPeer = new Peers { 
                    ChainRefId = incPeer.ChainRefId,
                    LastReach = DateTime.UtcNow,
                    PeerIP = incPeer.PeerIP,
                };

                peers.Insert(nPeer);
            }
        }
    }

}
