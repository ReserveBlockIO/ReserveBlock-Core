namespace ReserveBlockCore.Utilities
{
    public class GenesisBalanceUtility
    {
        public static Dictionary<string, decimal> GenesisBalances()
        {
            
            Dictionary<string, decimal> balanceSheet = new Dictionary<string, decimal> {
                                {"Insert Address", 1.0M },// Address, Amount in Decimal
                                
            };

            if(Globals.IsTestNet == true)
            {
                balanceSheet = new Dictionary<string, decimal> {
                                {"xMpa8DxDLdC9SQPcAFBc2vqwyPsoFtrWyC", 1000000M }, }; // Address, Amount in Decimal
            }

            return balanceSheet;
        }
        

    }
}
