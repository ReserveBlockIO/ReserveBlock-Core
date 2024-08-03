using Newtonsoft.Json;
using ReserveBlockCore.Bitcoin.ElectrumX.Results;

namespace ReserveBlockCore.Bitcoin.ElectrumX.Response
{
    public class BlockchainTransactionGetMerkleResponse : ResponseBase<BlockchainTransactionGetMerkleResult>
    {
        [JsonProperty("result")]
        public BlockchainTransactionGetMerkleResult Result { get; set; }
        public BlockchainTransactionGetMerkleResult GetResultModel()
        {
            return Result;
        }
    }
}
