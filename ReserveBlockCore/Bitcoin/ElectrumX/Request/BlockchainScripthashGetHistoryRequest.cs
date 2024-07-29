namespace ReserveBlockCore.Bitcoin.ElectrumX.Request
{
    internal class BlockchainScripthashGetHistoryRequest : RequestBase<string>
    {
        internal BlockchainScripthashGetHistoryRequest(string scriptHash)
        {
            Method = "blockchain.scripthash.get_history";
            Parameters = new [] { scriptHash };
        }
    }
}
