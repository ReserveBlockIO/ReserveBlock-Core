namespace ReserveBlockCore.Models.SmartContracts
{
    public class RoyaltyFeature
    {
        public RoyaltyType RoyaltyType { get; set; }
        public decimal RoyaltyAmount { get; set; }
        public string RoyaltyPayToAddress { get; set; }

        public static RoyaltyFeature? GetRoyaltyFeature(string[]? royaltyArray)
        {
            if(royaltyArray != null)
            {
                RoyaltyFeature royaltyFeat = new RoyaltyFeature();
                
                royaltyFeat.RoyaltyType = (RoyaltyType)Convert.ToInt32(royaltyArray[0]);
                royaltyFeat.RoyaltyAmount = Convert.ToDecimal(royaltyArray[1]);
                royaltyFeat.RoyaltyPayToAddress = royaltyArray[2].ToString();

                return royaltyFeat;
            }
            return null;
        }
    }

    public enum RoyaltyType
    {
        Flat,
        Percent
    }
}
