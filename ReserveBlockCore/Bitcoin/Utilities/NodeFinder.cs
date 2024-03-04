using ReserveBlockCore.Utilities;
using System.Net;
using System.Text.Json.Nodes;

namespace ReserveBlockCore.Bitcoin.Utilities
{
    public class NodeFinder
    {
        public static string[] MainnetDnsSeeds = new string[] 
        { 
            "seed.bitcoin.sipa.be", 
            "dnsseed.bluematt.me", 
            "dnsseed.bitcoin.dashjr.org",
            "seed.bitcoinstats.com",
            "seed.bitcoin.jonasschnelli.ch",
            "seed.btc.petertodd.net",
            "seed.bitcoin.sprovoost.nl",
            "dnsseed.emzy.de",
            "seed.bitcoin.wiz.biz"
        };
        public static string[] TestnetDnsSeeds = new string[] 
        {
            "testnet-seed.bitcoin.jonasschnelli.ch",
            "seed.tbtc.petertodd.net",
            "seed.testnet.bitcoin.sprovoost.nl",
            "testnet-seed.bluematt.me"
        };

        public static string Node = "NA";

        public static async Task<string> GetNode()
        {
            string nodeIp = "NA";
            if (Globals.BTCNetwork == NBitcoin.Network.Main)
            {
                if(Node == "NA")
                    nodeIp = await GetMainnetNode();
                else
                {
                    var testNode = PortUtility.IsPortOpen(Node, 8333);
                    if(testNode)
                    {
                        nodeIp = Node;
                    }
                    else
                    {
                        nodeIp = await GetMainnetNode();
                    }
                }
                Node = nodeIp;
            }
            else 
            {
                if (Node == "NA")
                    nodeIp = await GetTestnetNode();
                else
                {
                    var testNode = PortUtility.IsPortOpen(Node, 18333);
                    if (testNode)
                    {
                        nodeIp = Node;
                    }
                    else
                    {
                        nodeIp = await GetTestnetNode();
                    }
                }
                Node = nodeIp;
            }

            return nodeIp;
        }

        private static async Task<string> GetMainnetNode()
        {
            string nodeIp = "NA";
            int portNumber = 8333;
            bool nodeFound = false;

            while (!nodeFound)
            {
                Random rand = new Random();
                int randomIndex = rand.Next(0, MainnetDnsSeeds.Length);

                string randomSeed = MainnetDnsSeeds[randomIndex];

                try
                {
                    // Perform the DNS query
                    IPAddress[] addresses = Dns.GetHostAddresses(randomSeed);

                    if (addresses.Any())
                    {
                        foreach (IPAddress address in addresses)
                        {
                            var node = $"{address}";
                            var testNode = PortUtility.IsPortOpen(node, portNumber);
                            if (testNode)
                            {
                                nodeFound = true;
                                nodeIp = address.ToString();
                                break;
                            }
                        }
                    }


                }
                catch (Exception ex)
                {
                    ErrorLogUtility.LogError($"Error occurred while resolving {randomSeed}: {ex.Message}", "NodeFinder.GetTestnetNode()");
                }
            }

            if (nodeIp == "NA")
                ErrorLogUtility.LogError($"Could not find ANY nodes for network use.", "NodeFinder.GetTestnetNode()");

            return nodeIp;
        }

        private static async Task<string> GetTestnetNode()
        {
            string nodeIp = "NA";
            int portNumber = 18333;
            bool nodeFound = false;

            while(!nodeFound)
            {
                Random rand = new Random();
                int randomIndex = rand.Next(0, TestnetDnsSeeds.Length);

                string randomSeed = TestnetDnsSeeds[randomIndex];

                try
                {
                    // Perform the DNS query
                    IPAddress[] addresses = Dns.GetHostAddresses(randomSeed);

                    if(addresses.Any())
                    {
                        foreach (IPAddress address in addresses)
                        {
                            var node = $"{address}";
                            var testNode = PortUtility.IsPortOpen(node, portNumber);
                            if(testNode)
                            {
                                nodeFound = true;
                                nodeIp = address.ToString();
                                break;
                            }
                        }
                    }

                    
                }
                catch (Exception ex)
                {
                    ErrorLogUtility.LogError($"Error occurred while resolving {randomSeed}: {ex.Message}", "NodeFinder.GetTestnetNode()");
                }
            }

            if(nodeIp == "NA")
                ErrorLogUtility.LogError($"Could not find ANY nodes for network use.", "NodeFinder.GetTestnetNode()");

            return nodeIp;
        }

        public static async Task<IPAddress[]> GetNodeList()
        {
            IPAddress[] nodeIps = new IPAddress[] { };
            if (Globals.BTCNetwork == NBitcoin.Network.Main)
            {
                nodeIps = await GetMainnetNodes();   
            }
            else
            {
                nodeIps = await GetTestnetNodes();
            }

            return nodeIps;
        }

        private static async Task<IPAddress[]> GetMainnetNodes()
        {
            IPAddress[] nodeIp = new IPAddress[] { };
            int portNumber = 8333;
            bool nodeFound = false;

            while (!nodeFound)
            {
                Random rand = new Random();
                int randomIndex = rand.Next(0, MainnetDnsSeeds.Length);

                string randomSeed = MainnetDnsSeeds[randomIndex];

                try
                {
                    // Perform the DNS query
                    IPAddress[] addresses = Dns.GetHostAddresses(randomSeed);

                    if (addresses.Any())
                    {
                        return addresses;
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogUtility.LogError($"Error occurred while resolving {randomSeed}: {ex.Message}", "NodeFinder.GetTestnetNode()");
                }
            }

            return nodeIp;
        }

        private static async Task<IPAddress[]> GetTestnetNodes()
        {
            IPAddress[] nodeIp = new IPAddress[] { };
            int portNumber = 18333;
            bool nodeFound = false;

            while (!nodeFound)
            {
                Random rand = new Random();
                int randomIndex = rand.Next(0, TestnetDnsSeeds.Length);

                string randomSeed = TestnetDnsSeeds[randomIndex];

                try
                {
                    // Perform the DNS query
                    IPAddress[] addresses = Dns.GetHostAddresses(randomSeed);

                    if (addresses.Any())
                    {
                        return addresses;
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogUtility.LogError($"Error occurred while resolving {randomSeed}: {ex.Message}", "NodeFinder.GetTestnetNode()");
                }
            }

            return nodeIp;
        }
    }
}
