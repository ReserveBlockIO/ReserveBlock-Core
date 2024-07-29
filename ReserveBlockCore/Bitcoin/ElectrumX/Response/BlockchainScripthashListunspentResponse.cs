using System.Collections.Generic;
using ReserveBlockCore.Bitcoin.ElectrumX.Results;

namespace ReserveBlockCore.Bitcoin.ElectrumX.Response
{
    public class BlockchainScripthashListunspentResponse : ResponseBase<BlockchainScripthashListunspentResult>
    {
        [Newtonsoft.Json.JsonProperty("result")]
        public List<BlockchainScripthashListunspentResult> Result { get; set; }
        public List<BlockchainScripthashListunspentResult> GetResultModel()
        {
            return Result;
        }
    }
}
