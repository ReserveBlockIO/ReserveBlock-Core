using ReserveBlockCore.Data;
using ReserveBlockCore.Models;

namespace ReserveBlockCore.Services
{
    public class SeedNodeService
    {
        public static List<Peers> SeedNodes()
        {
            List<Peers> seedNodes = new List<Peers>();
            seedNodes.Add(new Peers { 
                ChainRefId = BlockchainData.ChainRef,
                PeerIP = "INSERT SEED NODES",
                LastReach = DateTime.UtcNow,
            });

            return seedNodes;
        }
    }
}
