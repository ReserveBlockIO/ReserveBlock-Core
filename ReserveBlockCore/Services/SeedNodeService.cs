using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using System.Net;

namespace ReserveBlockCore.Services
{
    public class SeedNodeService
    {
        public static List<SeedNode> SeedNodeList { get; set; }
        public static async Task<string> PingSeedNode()
        {
            bool nodeFound = false;
            var url = "NA";

            Random rnd = new Random();

            //randomizes seed list so not one is always the one being called.
            var randomizedSeedNostList = SeedNodeList.OrderBy(x => rnd.Next()).ToList();

            foreach (var node in randomizedSeedNostList)
            {
                try
                {
                    using (HttpClient client = new HttpClient())
                    {

                        string endpoint = node.NodeUrl + @"/api/V1";
                        using (var Response = await client.GetAsync(endpoint))
                        {
                            if (Response.StatusCode == System.Net.HttpStatusCode.OK)
                            {
                                string data = await Response.Content.ReadAsStringAsync();

                                var _response = data.TrimStart('[').TrimEnd(']').Replace("\"", "").Split(',');
                                var status = _response[1];
                                if(status == "Online")
                                {
                                    nodeFound = true;
                                    url = node.NodeUrl;
                                    break;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {

                }
            }

            return url;

        }

        public static async Task GetSeedNodePeers(string url)
        {
            if(Globals.IsTestNet == false)
            {
                try
                {
                    using (HttpClient client = new HttpClient())
                    {
                        string endpoint = url + "/api/V1/GetNodes";
                        using (var Response = await client.GetAsync(endpoint))
                        {
                            if (Response.StatusCode == System.Net.HttpStatusCode.OK)
                            {
                                string data = await Response.Content.ReadAsStringAsync();
                                var peers = data.TrimStart('[').TrimEnd(']').Replace("\"", "").Split(',');
                                var peerCount = peers.Count() - 1;
                                for (var i = 0; i <= peerCount; i++)
                                {
                                    var peer = peers[i];
                                    if (peer != "No Nodes")
                                    {
                                        Peers nPeer = new Peers
                                        {
                                            IsIncoming = false,
                                            IsOutgoing = true,
                                            PeerIP = peer,
                                            FailCount = 0
                                        };

                                        var dbPeers = Peers.GetAll();
                                        var peerExist = dbPeers.FindOne(x => x.PeerIP == peer);
                                        if (peerExist == null)
                                        {
                                            dbPeers.InsertSafe(nPeer);
                                        }
                                        else
                                        {
                                            peerExist.FailCount = 0;
                                            dbPeers.UpdateSafe(peerExist);
                                        }
                                    }

                                }
                            }
                            else
                            {

                            }
                        }
                    }
                }
                catch (Exception ex)
                {

                }
            }
            if(Globals.IsTestNet == true)
            {
                //manually add testnet IPs
                Peers nPeer = new Peers
                {
                    IsIncoming = false,
                    IsOutgoing = true,
                    PeerIP = "162.248.14.123",
                    FailCount = 0
                };

                var dbPeers = Peers.GetAll();
                var peerExist = dbPeers.FindOne(x => x.PeerIP == nPeer.PeerIP);
                if (peerExist == null)
                {
                    dbPeers.InsertSafe(nPeer);
                }
                else
                {
                    peerExist.FailCount = 0;
                    dbPeers.UpdateSafe(peerExist);
                }
            }
        }

        public static async Task GetAdjPoolList(string url)
        {
            if (Globals.IsTestNet == false)
            {
                try
                {
                    using (HttpClient client = new HttpClient())
                    {
                        string endpoint = url + "/api/V1/GetAdjPool";
                        using (var Response = await client.GetAsync(endpoint))
                        {
                            if (Response.StatusCode == System.Net.HttpStatusCode.OK)
                            {
                                string data = await Response.Content.ReadAsStringAsync();

                                var result = JsonConvert.DeserializeObject<List<AdjudicatorPool>>(data);
                                foreach (var pool in result)
                                    Globals.ConsensusNodes[pool.IPAddress] = new ConsensusNodeInfo
                                    {
                                        Address = pool.IPAddress,
                                        IpAddress = pool.IPAddress
                                    };                                
                            }
                            else
                            {

                            }
                        }
                    }
                }
                catch (Exception ex)
                {

                }
            }
            
        }

        public static List<SeedNode> SeedNodes()
        {
            SeedNodeList = new List<SeedNode>();

            List<SeedNode> seedNodes = new List<SeedNode>();

            seedNodes.Add(new SeedNode
            {
                NodeUrl = "https://seed1.rbx.network"
            });
            seedNodes.Add(new SeedNode
            {
                NodeUrl = "https://seed2.rbx.network"
            });

            //seedNodes.Add(new SeedNode
            //{
            //    NodeUrl = "https://marigold.rbx.network"
            //});
            //seedNodes.Add(new SeedNode
            //{
            //    NodeUrl = "https://daisy.rbx.network"
            //});
            //seedNodes.Add(new SeedNode
            //{
            //    NodeUrl = "https://tulip.rbx.network"
            //});
            //seedNodes.Add(new SeedNode
            //{
            //    NodeUrl = "https://peony.rbx.network"
            //});

            SeedNodeList.AddRange(seedNodes);

            return seedNodes;
        }

    }
}
