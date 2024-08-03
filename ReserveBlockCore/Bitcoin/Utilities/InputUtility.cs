using NBitcoin;
using ReserveBlockCore.Bitcoin.ElectrumX;

namespace ReserveBlockCore.Bitcoin.Utilities
{
    public class InputUtility
    {
        public static async Task<BitcoinAddress> GetAddressFromInput(Client _client, TxIn input)
        {
            try
            {
                var rawTx = await _client.GetRawTx(input.PrevOut.Hash.ToString());
                var prevTx = NBitcoin.Transaction.Parse(rawTx.RawTx, Globals.BTCNetwork);
                var prevOutput = prevTx.Outputs[input.PrevOut.N];
                return prevOutput?.ScriptPubKey.GetDestinationAddress(Globals.BTCNetwork);
            }
            catch { }

            return null;
        }

        public static async Task<Money> CalculateTotalInputAmount(Client _client, Transaction tx)
        {
            Money totalInputAmount = Money.Zero;

            foreach (var input in tx.Inputs)
            {
                var rawTx = await _client.GetRawTx(input.PrevOut.Hash.ToString());
                var prevTx = Transaction.Parse(rawTx.RawTx, Globals.BTCNetwork);
                var prevOutput = prevTx.Outputs[input.PrevOut.N];
                totalInputAmount += prevOutput.Value;
            }

            return totalInputAmount;
        }
    }
}
