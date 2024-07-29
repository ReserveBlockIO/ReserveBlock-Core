namespace ReserveBlockCore.Bitcoin.ElectrumX.Response
{
    public class BlockchainScriphashSubscribeResponse
    {
        [Newtonsoft.Json.JsonProperty("jsonrpc")]
        public string JsonRpc { get; set; }
        [Newtonsoft.Json.JsonProperty("method")]
        public string Method { get; set; }
        [Newtonsoft.Json.JsonProperty("params")]
        public string[] Params { get; set; }
    }
}
