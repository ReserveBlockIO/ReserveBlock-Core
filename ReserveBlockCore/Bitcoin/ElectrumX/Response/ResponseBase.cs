namespace ReserveBlockCore.Bitcoin.ElectrumX.Response
{
    public class ResponseBase<TResultModel> where TResultModel:new()
    {

        [Newtonsoft.Json.JsonProperty("jsonrpc")]
        protected string JsonRpcVersion { get; set; }

        [Newtonsoft.Json.JsonProperty("id")]
        protected int MessageId { get; set; }

        [Newtonsoft.Json.JsonProperty("error")]
        public Error Error { get; set; }
        

        private TResultModel GetResultModel()
        {
            return new TResultModel();
        }
    }
}
