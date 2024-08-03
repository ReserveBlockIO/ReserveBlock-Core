using Newtonsoft.Json;

namespace ReserveBlockCore.Bitcoin.ElectrumX.Results
{
    public class BlockchainTransactionGetMerkleResult
    {
        [JsonProperty("merkle")]
        public List<string> Merkle { get; set; }

        [JsonProperty("block_height")]
        public int Height { get; set; }

        [JsonProperty("pos")]
        public int Pos { get; set; }
    }
}
