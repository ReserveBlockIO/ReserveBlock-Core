using System.Collections.Generic;
using ReserveBlockCore.Bitcoin.ElectrumX.Results;

namespace ReserveBlockCore.Bitcoin.ElectrumX.Response
{
    public class BlockchainHeadersSubscribeResponse
    {
        [Newtonsoft.Json.JsonProperty("jsonrpc")]
        public string JsonRpc { get; set; }
        [Newtonsoft.Json.JsonProperty("method")]
        public string Method { get; set; }
        [Newtonsoft.Json.JsonProperty("params")]
        public List<BlockchainHeadersSubscribeResult> Params { get; set; }

        public BlockchainHeadersSubscribeResult GetResultModel()
        {
            return Params.Count > 0 ? Params[0] : new BlockchainHeadersSubscribeResult();
        }
    }

    
}
