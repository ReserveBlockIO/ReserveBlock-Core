namespace ReserveBlockCore.Bitcoin.ElectrumX.Request
{
    internal class BlockchainEstimateFeeRequest : RequestBase<string>
    {
        internal BlockchainEstimateFeeRequest(string blockCount)
        {
            Method = "blockchain.estimatefee";
            Parameters = new[]{ blockCount };
        }
    }
}
