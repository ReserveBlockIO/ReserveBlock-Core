using ReserveBlockCore.Models;
using ReserveBlockCore.Data;
using ReserveBlockCore.Services;

namespace ReserveBlockCore.Nodes
{
    public class NodeConnector
    {
        public static async void StartNodeConnecting()
        {
            //var nodeIp = SeedNodeService.PingSeedNode();
            //SeedNodeService.GetSeedNodePeers("");
            int successfulConnect = 0;
            bool alreadyCalled = false;

            List<Peers> peers = new List<Peers>();
            peers = Peers.GetAll().FindAll().ToList();

            if(peers.Count == 0)
            {
                var nodeIp = await SeedNodeService.PingSeedNode();
                SeedNodeService.GetSeedNodePeers(nodeIp);
                alreadyCalled = true;
            }

            peers = Peers.GetAll().FindAll().ToList();
            if(peers.Count <= 8)
            {
                //request peers from other nodes
                //Then request from seeds
                if(!alreadyCalled)
                {
                    var nodeIp = await SeedNodeService.PingSeedNode();
                    SeedNodeService.GetSeedNodePeers(nodeIp);
                }

            }
            else
            {
                //No peers available. You will have to manually add them.
            }
        }
    }
}
