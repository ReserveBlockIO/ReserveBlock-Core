namespace ReserveBlockCore.Bitcoin.ElectrumX.Results
{
    public class BlockchainScripthashListunspentResult
    {
        /// <summary>
        /// The integer height of the block the transaction was confirmed in. 0 if the transaction is in the mempool
        /// </summary>
        [Newtonsoft.Json.JsonProperty("height")]
        public int Height { get; set; }
        /// <summary>
        /// The zero-based index of the output in the transaction’s list of outputs
        /// </summary>
        [Newtonsoft.Json.JsonProperty("tx_pos")]
        public uint TxPos { get; set; }
        /// <summary>
        /// The output’s transaction hash as a hexadecimal string.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("tx_hash")]
        public string TxHash { get; set; }
        /// <summary>
        /// The output’s value in minimum coin units (satoshis).
        /// </summary>
        [Newtonsoft.Json.JsonProperty("value")]
        public ulong Value { get; set; }
    }
}
