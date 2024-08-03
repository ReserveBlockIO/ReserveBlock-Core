namespace ReserveBlockCore.Bitcoin.ElectrumX.Request
{
    internal class BlockchainTransactionGetMerkleRequest : RequestBase<object>
    {
        internal BlockchainTransactionGetMerkleRequest(string txId, int height)
        {
            Method = "blockchain.transaction.get_merkle";
            Parameters = new object[] { txId, height };
        }
    }
}
