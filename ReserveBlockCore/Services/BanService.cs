using LiteDB;
using ReserveBlockCore.Models;
using ReserveBlockCore.Utilities;
using System.Net;

namespace ReserveBlockCore.Services
{
    public class BanService
    {
        static SemaphoreSlim BanServiceLock = new SemaphoreSlim(1, 1);
        public static void BanPeer(string ipAddress, string message, string location)
        {
            if (Globals.AdjudicateAccount == null)
            {
                if (Globals.AdjNodes.ContainsKey(ipAddress))
                    return;
            }
            else
            {
                if (Globals.Nodes.ContainsKey(ipAddress))
                    return;
            }

            var peers = Peers.GetAll();
            var peer = Peers.GetPeer(ipAddress);
            if(peer == null)
            {
                var nPeer = new Peers
                {
                    BanCount = 1,
                    BannedFromAreasList = new List<string> { location },
                    FailCount = 0,
                    InitialBanDate = DateTime.UtcNow,
                    IsBanned = true,
                    IsIncoming = true,
                    IsOutgoing = false,
                    LastBanDate = DateTime.UtcNow,
                    LastBannedFromArea = location,
                    NextUnbanDate = GetNextUnbanDate(1),
                    PeerIP= ipAddress,  
                };

                if(peers != null)
                {
                    peers.InsertSafe(nPeer);
                }
                BanLogUtility.Log($"IP Address Banned: {ipAddress}. Ban count: {1}. Ban reason: {message}", location);
                Globals.BannedIPs[ipAddress] = nPeer;
            }
            else
            {
                if(!peer.IsPermaBanned)
                {
                    if (peer.BannedFromAreasList != null)
                    {
                        peer.BannedFromAreasList.Add(location);
                    }
                    else
                    {
                        peer.BannedFromAreasList = new List<string> { location };
                    }
                    peer.BanCount += 1;
                    peer.NextUnbanDate = GetNextUnbanDate(peer.BanCount);
                    peer.LastBannedFromArea = location;
                    peer.LastBanDate = DateTime.UtcNow;
                    peer.IsBanned = true;

                    if (peers != null)
                    {
                        peers.UpdateSafe(peer);
                    }
                }
                
                Globals.BannedIPs[ipAddress] = peer;
            }

            ReleasePeer(ipAddress);
        }

        public static void UnbanPeer(string ipAddress)
        {
            try
            {
                Globals.BannedIPs.TryRemove(ipAddress, out _);
                Globals.MessageLocks.TryRemove(ipAddress, out _);

                var peerDb = Peers.GetAll();
                var peer = peerDb.FindOne(x => x.PeerIP == ipAddress);
                if (peer != null)
                {
                    peer.IsBanned = false;
                    peer.IsPermaBanned = false;
                    peer.BanCount = 0;
                    peer.InitialBanDate = null;
                    peer.NextUnbanDate = null;
                    peer.LastBanDate = null;
                    peer.BannedFromAreasList = null;
                    peer.LastBannedFromArea = null;
                    peerDb.UpdateSafe(peer);
                }
            }
            catch { }
        }

        private static DateTime GetNextUnbanDate(int banCount)
        {
            if(banCount == 1)
            {
                return DateTime.UtcNow.AddMinutes(1);
            }
            else if(banCount == 2)
            {
                return DateTime.UtcNow.AddMinutes(5);
            }
            else if(banCount == 3)
            {
                return DateTime.UtcNow.AddMinutes(30);
            }
            else if(banCount == 4)
            {
                return DateTime.UtcNow.AddMinutes(60);
            }
            else if(banCount > 4 && banCount < 10)
            {
                return DateTime.UtcNow.AddHours(12);
            }
            else if(banCount == 10)
            {
                return DateTime.UtcNow.AddHours(24); //one last chance. 24 hour ban
            }
            else
            {
                return DateTime.UtcNow.AddYears(99); //perma banned now
            }
        }

        public static async Task PeerBanUnbanService()
        {
            bool RunLock = false;
            while(true)
            {
                var delay = Task.Delay(60000);
                if (Globals.StopAllTimers && !Globals.IsChainSynced)
                {
                    await delay;
                    continue;
                }
                await BanServiceLock.WaitAsync();
                try
                {
                    RunUnban();

                    RunBanReset();

                    RunPermaBan();
                }
                finally
                {
                    BanServiceLock.Release();
                }

                await delay;
            }
        }

        private static void ReleasePeer(string ipAddress)
        {
            try
            {
                if (Globals.FortisPool.TryGetFromKey1(ipAddress, out var pool))
                    pool.Value.Context?.Abort();

                if (Globals.AdjNodes.TryRemove(ipAddress, out var adjnode) && adjnode.Connection != null)
                    adjnode.Connection.DisposeAsync().ConfigureAwait(false).GetAwaiter().GetResult();

                if (Globals.Nodes.TryRemove(ipAddress, out var node) && node.Connection != null)
                    node.Connection.DisposeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch { }
        }

        private static void RunUnban()
        {
            try
            {
                var peers = Peers.GetAll();
                if (peers != null)
                {
                    var bannedPeers = peers.Query().Where(x =>
                            x.IsBanned &&
                            x.NextUnbanDate != null &&
                            x.NextUnbanDate.Value <= DateTime.UtcNow &&
                            !x.IsPermaBanned).ToEnumerable();

                    if (bannedPeers.Count() > 0)
                    {
                        foreach (var bPeer in bannedPeers)
                        {
                            bPeer.IsBanned = false;
                            peers.UpdateSafe(bPeer);
                            Globals.BannedIPs.TryRemove(bPeer.PeerIP, out _);
                            Globals.MessageLocks.TryRemove(bPeer.PeerIP, out _);
                        }
                    }
                }
            }
            catch(Exception ex)
            {

            }
            
        }

        private static void RunBanReset()
        {
            try
            {
                var peers = Peers.GetAll();
                if (peers != null)
                {
                    var unbannedPeersWithCount = peers.Query().Where(x =>
                        x.BanCount > 0 &&
                        !x.IsBanned &&
                        !x.IsPermaBanned &&
                        x.NextUnbanDate != null &&
                        x.NextUnbanDate.Value.AddHours(1) <= DateTime.UtcNow).ToEnumerable();

                    if (unbannedPeersWithCount.Count() > 0)
                    {
                        foreach (var ubPeer in unbannedPeersWithCount)
                        {
                            ubPeer.BanCount = 0;
                            ubPeer.InitialBanDate = null;
                            ubPeer.NextUnbanDate = null;
                            ubPeer.LastBanDate = null;
                            ubPeer.BannedFromAreasList = null;
                            ubPeer.LastBannedFromArea = null;
                            peers.UpdateSafe(ubPeer);
                            Globals.BannedIPs.TryRemove(ubPeer.PeerIP, out _);
                            Globals.MessageLocks.TryRemove(ubPeer.PeerIP, out _);
                        }
                    }
                }
            }
            catch { }
        }

        private static void RunPermaBan()
        {
            try
            {
                var peers = Peers.GetAll();
                if (peers != null)
                {
                    var permaBanList = peers.Query().Where(x =>
                        x.IsBanned &&
                        x.BanCount > 10 &&
                        !x.IsPermaBanned).ToEnumerable();

                    if (permaBanList.Count() > 0)
                    {
                        foreach (var permaBanPeer in permaBanList)
                        {
                            permaBanPeer.IsPermaBanned = true;
                            peers.UpdateSafe(permaBanPeer);
                            Globals.BannedIPs[permaBanPeer.PeerIP] = permaBanPeer;
                        }
                    }
                }
            }
            catch { }
        }
    }
}
