namespace ReserveBlockCore.Bitcoin.ElectrumX.Results
{
    public class BlockchainScripthashGetHistoryResult
    {
        /// <summary>
        /// The transaction hash in hexadecimal.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("tx_hash")]
        public string TxHash { get; set; }
        /// <summary>
        /// The integer height of the block the transaction was confirmed in.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("height")]
        public int Height { get; set; }
        /// <summary>
        /// The transaction fee in minimum coin units (satoshis).
        /// </summary>
        [Newtonsoft.Json.JsonProperty("fee")]
        public ulong Fee { get; set; }
    }
}
