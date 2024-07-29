namespace ReserveBlockCore.Bitcoin.ElectrumX.Request
{
    internal class BlockchainTransactionGetRequest : RequestBase<object>
    {
        internal BlockchainTransactionGetRequest(string txHash, bool verbose = false)
        {
            Method = "blockchain.transaction.get";
            Parameters = new object[]{ txHash, verbose };
        }
    }
}
