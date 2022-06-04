namespace ReserveBlockCore.Models.SmartContracts
{
    public class FractionalizeFeature
    {
        public decimal SmallestFractionalizedAmount { get; set; } //smallest amount is 0.01
        public long TotalFractionalizedShares { get; set; }
        public decimal AmountOwned { get; set; }
    }
}
