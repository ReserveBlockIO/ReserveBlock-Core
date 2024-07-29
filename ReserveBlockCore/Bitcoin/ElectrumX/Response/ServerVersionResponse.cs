using ReserveBlockCore.Bitcoin.ElectrumX.Results;

namespace ReserveBlockCore.Bitcoin.ElectrumX.Response
{
    public class ServerVersionResponse : ResponseBase<ServerVersionResult>
    {
        [Newtonsoft.Json.JsonProperty("result")]
        public string[] Result{ get; set; }

        public ServerVersionResult GetResultModel()
        {
            Version.TryParse(Result[1], out var ver);
            return new ServerVersionResult()
            {
                ClientName = Result[0],
                ProtocolVersion = ver
            };
        }
    }
}
