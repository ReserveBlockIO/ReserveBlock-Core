using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Utilities;
using Spectre.Console;
using System;
using System.Net;

namespace ReserveBlockCore.Services
{
    public class SeedNodeService
    {
        public static List<SeedNode> SeedNodeList = new List<SeedNode>();
                        
        public static async Task<string> PingSeedNode()
        {
            bool nodeFound = false;
            bool isSeeded = false;
            int count = 0;
            var url = "NA";

            Random rnd = new Random();

            //randomizes seed list so not one is always the one being called.
            if(SeedNodeList.Count() == 0)
                SeedNodes();
            var randomizedSeedNostList = SeedNodeList.Where(x => x != null).OrderBy(x => rnd.Next()).ToList();

            do
            {
                foreach (var node in randomizedSeedNostList)
                {
                    try
                    {
                        using (var client = Globals.HttpClientFactory.CreateClient())
                        {

                            string endpoint = node.NodeUrl + @"/api/V1";
                            using (var Response = await client.GetAsync(endpoint, new CancellationTokenSource(5000).Token))
                            {
                                if (Response.StatusCode == System.Net.HttpStatusCode.OK)
                                {
                                    string data = await Response.Content.ReadAsStringAsync();

                                    var _response = data.TrimStart('[').TrimEnd(']').Replace("\"", "").Split(',');
                                    var status = _response[1];
                                    if (status == "Online")
                                    {
                                        nodeFound = true;
                                        url = node.NodeUrl;
                                        isSeeded = true;
                                        break;
                                    }
                                }
                                else
                                {
                                    count += 1;
                                }
                            }
                        }
                    }
                    catch { count += 1; }
                }

            } while (!isSeeded && count < 3);
                
            return url;
        }

        public static async Task GetSeedNodePeers(string url)
        {
            if (Globals.IsTestNet == false)
            {
                try
                {
                    using (var client = Globals.HttpClientFactory.CreateClient())
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
        }

        public static async Task GetSeedNodePeersTestnet()
        {

            if (Globals.IsTestNet == true)
            {
                List<Peers> peerList = new List<Peers>();
                //manually add testnet IPs
                Peers nPeer = new Peers
                {
                    IsIncoming = false,
                    IsOutgoing = true,
                    PeerIP = "162.248.14.123",
                    FailCount = 0
                };

                Peers n2Peer = new Peers
                {
                    IsIncoming = false,
                    IsOutgoing = true,
                    PeerIP = "164.92.105.169",
                    FailCount = 0
                };

                Peers n3Peer = new Peers
                {
                    IsIncoming = false,
                    IsOutgoing = true,
                    PeerIP = "137.184.158.154",
                    FailCount = 0
                };

                peerList.Add(nPeer);
                peerList.Add(n2Peer);
                peerList.Add(n3Peer);

                var dbPeers = Peers.GetAll();

                foreach(var peer in peerList)
                {
                    var peerExist = dbPeers.FindOne(x => x.PeerIP == peer.PeerIP);
                    if (peerExist == null)
                    {
                        dbPeers.InsertSafe(peer);
                    }
                    else
                    {
                        peerExist.FailCount = 0;
                        dbPeers.UpdateSafe(peerExist);
                    }
                }
            }
        }
        
        internal static async Task CallToSeed()
        {
            if (!Globals.RefuseToCallSeed)
            {
                if (Globals.IsTestNet == false)
                {
                    try
                    {
                        var settingsDB = Settings.GetSettingsDb();
                        var settings = Settings.GetSettings();
                        if(settings?.CalledToSeed == false)
                        {
                            var seedNodes = SeedNodes();
                            int count = 0;
                            foreach (var seedNode in seedNodes)
                            {
                                using (var client = Globals.HttpClientFactory.CreateClient())
                                {
                                    string endpoint = seedNode.NodeUrl + "/api/V1/GetCallToNode";
                                    using (var Response = await client.GetAsync(endpoint, new CancellationTokenSource(5000).Token))
                                    {
                                        if (Response.StatusCode == System.Net.HttpStatusCode.OK)
                                        {
                                            count += 1;
                                        }
                                    }

                                }
                            }

                            if (count == seedNodes.Count())
                            {
                                if (settingsDB != null)
                                {
                                    if (settings != null)
                                    {
                                        settings.CalledToSeed = true;
                                        settingsDB.UpdateSafe(settings);
                                    }
                                }

                            }
                        }
                    }
                    catch { }
                }
            }
        }

        public static void SeedBench()
        {
            var benches = AdjBench.GetBench().FindAll().ToList();
            if(benches?.Count() <= 12)
            {
                if (!Globals.IsTestNet)
                {
                    //main adjs
                    List<AdjBench> mainList = new List<AdjBench>{
                        new AdjBench { IPAddress = "144.126.156.102", PulledFromBench= true, RBXAddress= "RBX1", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        new AdjBench { IPAddress = "144.126.156.101", PulledFromBench = true, RBXAddress = "RBX2", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        new AdjBench { IPAddress = "66.94.124.3", PulledFromBench = true, RBXAddress = "RBX3", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        new AdjBench { IPAddress = "66.94.124.2", PulledFromBench = true, RBXAddress = "RBX4", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        new AdjBench { IPAddress = "66.175.236.113", PulledFromBench = true, RBXAddress = "RBX5", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        new AdjBench { IPAddress = "154.12.251.106", PulledFromBench = true, RBXAddress = "RBX6", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        new AdjBench { IPAddress = "15.204.9.117", PulledFromBench = true, RBXAddress = "RBX7", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" }
                    };

                    //benched ADJS
                    List<AdjBench> benchList = new List<AdjBench>
                    {
                        new AdjBench { IPAddress = "154.12.251.107", PulledFromBench= false, RBXAddress= "RBX8", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        new AdjBench { IPAddress = "207.244.234.76", PulledFromBench= false, RBXAddress= "RBX9", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        new AdjBench { IPAddress = "207.244.230.235", PulledFromBench= false, RBXAddress= "RBX10", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        new AdjBench { IPAddress = "15.204.9.193", PulledFromBench= false, RBXAddress= "RBX11", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        new AdjBench { IPAddress = "135.148.121.99", PulledFromBench= false, RBXAddress= "RBX12", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                    };

                    AdjBench.SaveListToBench(mainList);
                    AdjBench.SaveListToBench(benchList);
                }
                else
                {               
                    List <AdjBench> mainList = new List<AdjBench>{
                        new AdjBench { IPAddress = "144.126.156.102", PulledFromBench= true, RBXAddress= "xBRzJUZiXjE3hkrpzGYMSpYCHU1yPpu8cj", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        new AdjBench { IPAddress = "144.126.156.101", PulledFromBench = true, RBXAddress = "xBRNST9oL8oW6JctcyumcafsnWCVXbzZnr", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        new AdjBench { IPAddress = "66.94.124.3", PulledFromBench = true, RBXAddress = "xBRKXKyYQU5k24Rmoj5uRkqNCqJxxci5tC", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        new AdjBench { IPAddress = "66.94.124.2", PulledFromBench = true, RBXAddress = "xBRqxLS81HrR3bGRpDa4xTfAEvx7skYDGq", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        new AdjBench { IPAddress = "66.175.236.113", PulledFromBench = true, RBXAddress = "xBRS3SxqLQtEtmqZ1BUJiobjUzwufwaAnK", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        new AdjBench { IPAddress = "154.12.251.106", PulledFromBench = true, RBXAddress = "xHBG5xUbjTJ4hdhF5b2aEfo3VtH4qToe8h", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        new AdjBench { IPAddress = "15.204.9.117", PulledFromBench = true, RBXAddress = "xS8CnrDN771UVdoyPn98iKnHwBywy4Jq51", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" }
                    };

                    AdjBench.SaveListToBench(mainList);
                }
            }
            
            foreach (var bench in AdjBench.GetBench().FindAll())
                Globals.AdjBench[bench.RBXAddress] = bench;            
        }

        public static List<SeedNode> SeedNodes()
        {
            List<SeedNode> seedNodes = new List<SeedNode>();

            if (SeedNodeList.Count == 0)
            {
                SeedNodeList = new List<SeedNode>();

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
            }
            else
            {
                seedNodes = SeedNodeList;
            }
            

            return seedNodes;
        }

    }
}
