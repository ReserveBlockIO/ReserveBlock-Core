using ImageMagick;
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
        static SemaphoreSlim SeedNodeServiceLock = new SemaphoreSlim(1, 1);
        public static async Task Start()
        {
            if(!Globals.BlockSeedCalls)
            {
                while (true)
                {
                    var delay = Task.Delay(new TimeSpan(0, 15, 0));

                    await SeedNodeServiceLock.WaitAsync();
                    try
                    {
                        await CallToSeed();
                    }
                    finally
                    {
                        SeedNodeServiceLock.Release();
                    }

                    await delay;
                }
            }
        }
                        
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
                        string endpoint = url + "/api/V1/GetPeers";
                        using (var Response = await client.GetAsync(endpoint))
                        {
                            if (Response.StatusCode == System.Net.HttpStatusCode.OK)
                            {
                                string data = await Response.Content.ReadAsStringAsync();
                                var response = JsonConvert.DeserializeObject<SeedResponse>(data); 
                                if(response != null)
                                {
                                    if(response.Success)
                                    {
                                        var peers = response.Nodes;
                                        if(peers.Any())
                                        {
                                            foreach (var peer in peers)
                                            {
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

        public static async Task GetArbiterFromSeed()
        {
            if(Globals.IsTestNet)
            {
                Globals.ArbiterURI = $"http://162.248.14.123:{Globals.ArbiterPort}";
            }
            else
            {
                //TODO: Call to Seed for Arbiter Seed
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
                    FailCount = 0,
                    IsValidator = true
                };

                Peers n2Peer = new Peers
                {
                    IsIncoming = false,
                    IsOutgoing = true,
                    PeerIP = "144.126.156.102",
                    FailCount = 0,
                    IsValidator = true
                };

                peerList.Add(nPeer);
                peerList.Add(n2Peer);

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
                        peerExist.IsValidator = true;
                        dbPeers.UpdateSafe(peerExist);
                    }
                }
            }
        }
        
        internal static async Task CallToSeed()
        {
            if (!Globals.BlockSeedCalls)
            {
                if (Globals.IsTestNet == false)
                {
                    try
                    {
                        var seedNodes = SeedNodes();
                        int count = 0;
                        foreach (var seedNode in seedNodes)
                        {
                            using (var client = Globals.HttpClientFactory.CreateClient())
                            {
                                string endpoint = string.IsNullOrEmpty(Globals.ValidatorAddress) ? seedNode.NodeUrl + "/api/V1/GetCallToNode" :
                                    seedNode.NodeUrl + "/api/V1/GetCallToNode/true";
                                using (var Response = await client.GetAsync(endpoint, new CancellationTokenSource(5000).Token))
                                {
                                    if (Response.StatusCode == System.Net.HttpStatusCode.OK)
                                    {
                                        if(Globals.SeedDict.TryGetValue(seedNode.NodeUrl, out string value))
                                        {
                                            Globals.SeedDict[seedNode.NodeUrl] = "Online";
                                        }
                                        else
                                        {
                                            Globals.SeedDict.TryAdd(seedNode.NodeUrl, "Online");
                                        }
                                    }
                                    else
                                    {
                                        if (Globals.SeedDict.TryGetValue(seedNode.NodeUrl, out string value))
                                        {
                                            Globals.SeedDict[seedNode.NodeUrl] = "Offline";
                                        }
                                        else
                                        {
                                            Globals.SeedDict.TryAdd(seedNode.NodeUrl, "Offline");
                                        }
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
            if(benches?.Count() <= 99)
            {
                if (!Globals.IsTestNet)
                {
                    //main adjs
                    List<AdjBench> mainList = new List<AdjBench>{
                        new AdjBench { IPAddress = "144.126.156.102", PulledFromBench= true, RBXAddress= "RBxy1XGZ72f6YqktseaLJ1sJsE9u5DF3sp", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        new AdjBench { IPAddress = "144.126.156.101", PulledFromBench = true, RBXAddress = "RBxkrs6snuTuHjAfzedXGzRixfeyvQfy7m", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        new AdjBench { IPAddress = "66.94.124.3", PulledFromBench = true, RBXAddress = "RBxz1j5veSPrBg4RSyYD4CZ9BY6LPQ65gM", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        new AdjBench { IPAddress = "66.94.124.2", PulledFromBench = true, RBXAddress = "RBx1FNEvjB97HRdreDg3zHCNCSSEvSyBTE", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        //Working
                        new AdjBench { IPAddress = "66.175.236.113", PulledFromBench = true, RBXAddress = "RBxuRe1PorrpUCSbcmBk4JDHCxeADAkXyX", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        new AdjBench { IPAddress = "15.204.9.117", PulledFromBench = true, RBXAddress = "RBxc2kz67W2zvb3yGxzACEQqgFiiBfYSTY", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        //Original
                        //new AdjBench { IPAddress = "154.12.251.106", PulledFromBench = true, RBXAddress = "RBxfsqZ28nZt9wM9rNeacfxqPFUkKfXWM7", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        //Newly Added
                        new AdjBench { IPAddress = "15.204.9.193", PulledFromBench = true, RBXAddress = "RBxfsqZ28nZt9wM9rNeacfxqPFUkKfXWM7", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },

                    };

                    //benched ADJS
                    List<AdjBench> benchList = new List<AdjBench>
                    {
                        new AdjBench { IPAddress = "154.12.251.107", PulledFromBench= false, RBXAddress= "REM8GPUPGDH2HrqbnipmtCc7qWkghagWDS", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        new AdjBench { IPAddress = "207.244.234.76", PulledFromBench= false, RBXAddress= "RPQcVPnTkaX7pcw5ercpb1sm8gWgFwxAQC", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        new AdjBench { IPAddress = "207.244.230.235", PulledFromBench= false, RBXAddress= "RPrAEQ7a93WDDPmepBeLgVfawDdFFnDYVR", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        //new AdjBench { IPAddress = "15.204.9.193", PulledFromBench= false, RBXAddress= "RHW6PSFE7cP47WvZns8y4ijt8PCmJgNdTA", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        //new AdjBench { IPAddress = "135.148.121.99", PulledFromBench= false, RBXAddress= "RURosLmRx1Ay85hSeXyr5sjJ7gAhz4rFJd", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                    };

                    AdjBench.SaveListToBench(mainList);
                    AdjBench.SaveListToBench(benchList);
                }
                else
                {               
                    List <AdjBench> mainList = new List<AdjBench>{
                        new AdjBench { IPAddress = "144.126.141.56", PulledFromBench = true, RBXAddress = "xBRxhFC2C4qE21ai3cQuBrkyjXnvP1HqZ8", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        new AdjBench { IPAddress = "144.126.156.176", PulledFromBench = true, RBXAddress = "xBRA57xaL612t35aac1WWQxYQ2ipTV5WcF", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        new AdjBench { IPAddress = "144.126.141.210", PulledFromBench = true, RBXAddress = "xBREKz8TcSh7uhs5mNrWttGkrciaq2jy3V", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        new AdjBench { IPAddress = "144.126.156.180", PulledFromBench = true, RBXAddress = "xBRHXgEwJEqZad6USusAXJfz7Pc6KHViix", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
                        new AdjBench { IPAddress = "162.251.121.150", PulledFromBench = true, RBXAddress = "xBRgsdHnRBnpbBNTfWPk2dKdNbfKs9GDWK", TimeEligibleForConsensus = 1674055875, TimeEntered = 1674055875, TopicUID = "Seed" },
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
                seedNodes.Add(new SeedNode
                {
                    NodeUrl = "https://seed3.rbx.network"
                });


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

        private class SeedResponse
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public List<string> Nodes { get; set; }
        }
    }
}
