using ReserveBlockCore.Bitcoin.ElectrumX.Results;

namespace ReserveBlockCore.Bitcoin.ElectrumX.Response
{
    public class BlockchainBlockHeaderGetResponse : ResponseBase<BlockchainBlockHeaderGetResult>
    {
        [Newtonsoft.Json.JsonProperty("result")]
        public BlockchainBlockHeaderGetResult Result { get; set; }
        public BlockchainBlockHeaderGetResult GetResultModel() { return Result; }
    }
}
