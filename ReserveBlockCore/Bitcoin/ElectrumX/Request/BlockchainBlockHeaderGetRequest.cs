namespace ReserveBlockCore.Bitcoin.ElectrumX.Request
{
    internal class BlockchainBlockHeaderGetRequest : RequestBase<object>
    {
        internal BlockchainBlockHeaderGetRequest(int height, int count)
        {
            Method = "blockchain.block.headers";
            Parameters = new object[] { height, count };
        }
    }
}
