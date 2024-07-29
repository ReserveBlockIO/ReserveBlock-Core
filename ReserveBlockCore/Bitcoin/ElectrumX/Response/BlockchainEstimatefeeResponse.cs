using ReserveBlockCore.Bitcoin.ElectrumX.Results;

namespace ReserveBlockCore.Bitcoin.ElectrumX.Response
{
    public class BlockchainEstimatefeeResponse : ResponseBase<BlockchainEstimatefeeResult>
    {
        [Newtonsoft.Json.JsonProperty("result")]
        public decimal Result { get; set; }

        public BlockchainEstimatefeeResult GetResultModel()
        {
            return new BlockchainEstimatefeeResult()
            {
                Fee = Result
            };
        }
    }
}
