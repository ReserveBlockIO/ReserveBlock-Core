namespace ReserveBlockCore.Bitcoin.ElectrumX.Request
{
    internal class BlockchainScripthashGetBalance : RequestBase<string>
    {
        internal BlockchainScripthashGetBalance(string scriptHash)
        {
            Method = "blockchain.scripthash.get_balance";
            Parameters = new [] { scriptHash};
        }
    }
}
