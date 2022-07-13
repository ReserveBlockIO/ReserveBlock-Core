namespace ReserveBlockCore.Utilities
{
    public class GenesisBalanceUtility
    {
        public static Dictionary<string, decimal> GenesisBalances()
        {
            
            Dictionary<string, decimal> balanceSheet = new Dictionary<string, decimal> {
                                {"Input Address", 1M }, // Address, Amount in Decimal
                                

            };

            if(Program.IsTestNet == true)
            {
                balanceSheet = new Dictionary<string, decimal> {
                                {"xAfPR4w2cBsvmB7Ju5mToBLtJYuv1AZSyo", 1000000M }, }; // Address, Amount in Decimal
            }



            return balanceSheet;
        }
        

    }
}
