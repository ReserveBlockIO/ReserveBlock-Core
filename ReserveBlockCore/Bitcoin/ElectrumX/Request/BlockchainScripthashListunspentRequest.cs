namespace ReserveBlockCore.Bitcoin.ElectrumX.Request
{
    internal class BlockchainScripthashListunspentRequest : RequestBase<string>
    {
        internal BlockchainScripthashListunspentRequest(string scriptHash)
        {
            Method = "blockchain.scripthash.listunspent";
            Parameters = new [] { scriptHash };
        }
    }
}
