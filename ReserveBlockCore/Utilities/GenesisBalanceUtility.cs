namespace ReserveBlockCore.Utilities
{
    public class GenesisBalanceUtility
    {
        public static Dictionary<string, decimal> GenesisBalances()
        {
            
            Dictionary<string, decimal> balanceSheet = new Dictionary<string, decimal> {
                                {"Input Address", 1M }, // Address, Amount in Decimal
                                

            };



            return balanceSheet;
        }
        

    }
}
