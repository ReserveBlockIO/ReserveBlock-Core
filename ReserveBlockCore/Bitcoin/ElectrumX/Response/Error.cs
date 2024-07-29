namespace ReserveBlockCore.Bitcoin.ElectrumX.Response
{
    public class Error
    {
        [Newtonsoft.Json.JsonProperty("code")]
        public int Code { get; set; }
        [Newtonsoft.Json.JsonProperty("message")]
        public string Message { get; set; }
    }
}
