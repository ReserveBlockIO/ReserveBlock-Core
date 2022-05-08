namespace ReserveBlockCore.Models.SmartContracts
{
    public class RoyaltyFeature
    {
        public RoyaltyType RoyaltyType { get; set; }
        public decimal RoyaltyAmount { get; set; }
        public string RoyaltyPayToAddress { get; set; } 
    }

    public enum RoyaltyType
    {
        Flat,
        Percent
    }
}
