namespace ReserveBlockCore.Bitcoin.ElectrumX.Response
{
    public class ServerPingResponse :ResponseBase<object>
    {
        [Newtonsoft.Json.JsonProperty("result")]
        public string Result { get; set; }

        public string GetResultModel()
        {
            return Result;
        }
    }
}
