using NBitcoin;
using NBitcoin.Protocol;
using ReserveBlockCore.Bitcoin.Utilities;
using System.Net;

namespace ReserveBlockCore.Bitcoin.Services
{
    public class BroadcastService
    {
        public static async Task<bool> BroadcastTx(Transaction tx)
        {
            var broadcastNodes = await NodeFinder.GetNodeList();

            if(!broadcastNodes.Any())
                return false;

            foreach (IPAddress address in broadcastNodes)
            {
                try
                {
                    var node = await Node.ConnectAsync(Globals.BTCNetwork, address.ToString());

                    node.VersionHandshake();
                    node.SendMessage(new InvPayload(InventoryType.MSG_TX, tx.GetHash()));
                    node.SendMessage(new TxPayload(tx));
                    Thread.Sleep(500);

                    node.Disconnect();
                }
                catch { }
            }

            return true;
        }
    }
}
