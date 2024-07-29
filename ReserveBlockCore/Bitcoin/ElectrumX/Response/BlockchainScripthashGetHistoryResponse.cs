using System.Collections.Generic;
using ReserveBlockCore.Bitcoin.ElectrumX.Results;

namespace ReserveBlockCore.Bitcoin.ElectrumX.Response
{
    public class BlockchainScripthashGetHistoryResponse : ResponseBase<List<BlockchainScripthashGetHistoryResult>>
    {
        [Newtonsoft.Json.JsonProperty("result")]
        public List<BlockchainScripthashGetHistoryResult> Result { get; set; }
        public List<BlockchainScripthashGetHistoryResult> GetResultModel()
        {
            return Result;
        }
    }
}
