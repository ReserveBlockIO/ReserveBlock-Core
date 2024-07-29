namespace ReserveBlockCore.Bitcoin.ElectrumX.Results
{
    public class BlockchainHeadersSubscribeResult
    {
        /// <summary>
        /// The binary header as a hexadecimal string.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("hex")]
        public string BlockHeader { get; set; }
        /// <summary>
        /// The height of the header, an integer.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("height")]
        public int Height { get; set; }
    }
}
