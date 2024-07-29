namespace ReserveBlockCore.Bitcoin.ElectrumX.Request
{
    internal class BlockchainScripthashSubscribeRequest : RequestBase<string>
    {
        internal BlockchainScripthashSubscribeRequest(string scriptHash)
        {
            Method = "blockchain.scripthash.subscribe";
            Parameters = new [] { scriptHash };
        }
    }
}
