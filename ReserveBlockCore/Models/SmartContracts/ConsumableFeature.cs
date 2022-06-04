namespace ReserveBlockCore.Models.SmartContracts
{
    public class ConsumableFeature
    {
        public string ConsumableAddress { get; set; }
        public string ConsumableSignature { get; set; }
        public DateTime? AutoConsumedByDate { get; set; }
        public long? AutoConsumedByBlockHeight { get; set; }
        public bool IsConsumed { get; set; }
    }
}
