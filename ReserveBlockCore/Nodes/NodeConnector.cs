using ReserveBlockCore.Models;
using ReserveBlockCore.Data;

namespace ReserveBlockCore.Nodes
{
    public class NodeConnector
    {
        public static async void StartNodeConnecting()
        {
            List<Peers> peers = new List<Peers>();
            peers = Peers.PeerList();
        }
    }
}
