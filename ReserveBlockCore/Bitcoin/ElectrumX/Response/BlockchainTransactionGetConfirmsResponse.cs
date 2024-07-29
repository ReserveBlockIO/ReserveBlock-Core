using ReserveBlockCore.Bitcoin.ElectrumX.Results;

namespace ReserveBlockCore.Bitcoin.ElectrumX.Response
{
    public class BlockchainTransactionGetConfirmsResponse : ResponseBase<BlockchainTransactionGetConfirmsResult>
    {
        [Newtonsoft.Json.JsonProperty("result")]
        public BlockchainTransactionGetConfirmsResult Result { get; set; }
        public int GetResultModel()
        {
            return Result.Confirmations;
        }
    }
}
