using System.Text;
using Newtonsoft.Json;

namespace ReserveBlockCore.Bitcoin.ElectrumX.Request
{
    internal class RequestBase<T>
    {
        [JsonProperty("jsonrpc")] public string JsonRpc { get; set; } = "2.0";
        [JsonProperty("id")] public int MessageId { get; set; } = IdCounter.UseId();

        [JsonProperty("method")]
        public string Method { get; set; }
        [JsonProperty("params")]
        public T[] Parameters { get; set; }

        internal byte[] GetRequestData()
        {
            return Encoding.ASCII.GetBytes(ToJson());
        }
        internal string ToJson()
        {
            return JsonConvert.SerializeObject(this, _settings) + "\n";
        }

        private readonly JsonSerializerSettings _settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None
        };
    }
}
