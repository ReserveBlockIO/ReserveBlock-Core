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

            List<Peers> peers = new List<Peers>();
            peers = Peers.GetAll().FindAll().ToList();

            if(peers.Count == 0)
            {
                var nodeIp = await SeedNodeService.PingSeedNode();
                SeedNodeService.GetSeedNodePeers(nodeIp);
            }

            peers = Peers.GetAll().FindAll().ToList();
            if(peers.Count <= 8)
            {
                //request peers from other nodes
                //Then request from seeds
            }
            else
            {
                //No peers available. You will have to manually add them.
            }
        }
    }
}
