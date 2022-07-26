using ReserveBlockCore.Extensions;
using ReserveBlockCore.Data;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
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
                ErrorLogUtility.LogError(ex.Message, "Peers.GetAll()");
                return null;
            }
            
        }

        public static void UpdatePeerLastReach(Peers incPeer)
        {
            var peers = GetAll();
            var peer = GetAll().FindOne(x => x.PeerIP == incPeer.PeerIP);
            if(peer != null)
            {
                //peer.LastReach = DateTime.UtcNow;
                peers.Update(peer);
            }
            else
            {
                Peers nPeer = new Peers { 
                    //ChainRefId = incPeer.ChainRefId,
                    //LastReach = DateTime.UtcNow,
                    PeerIP = incPeer.PeerIP,
                };

                peers.Insert(nPeer);
            }
        }
    }

}
