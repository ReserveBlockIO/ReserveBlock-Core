namespace ReserveBlockCore.Bitcoin.ElectrumX.Results
{
    public class BlockchainBlockHeaderGetResult
    {
        [Newtonsoft.Json.JsonProperty("count")]
        public int Count { get; set; }
        [Newtonsoft.Json.JsonProperty("hex")]
        public string Hex { get; set; }
        [Newtonsoft.Json.JsonProperty("max")]
        public int Max { get; set; }
    }
}
