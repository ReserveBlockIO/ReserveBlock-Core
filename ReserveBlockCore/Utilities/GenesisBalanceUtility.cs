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
                                {"xMpa8DxDLdC9SQPcAFBc2vqwyPsoFtrWyC", 1000000M },
                                {"xLK7MYcjqJ74suU1wGQ6M7TgpQuevH4wiH", 10000M },
                                {"xLcKCCKPBk94yZm8uzcTHXneEj69XHSshq", 10000M },
                                {"xDCHzEyNfRtsZxKdoGB2ZoZeeSygah91fW", 10000M },
                                {"xAqnv8h6LnU1fkUjxoP1GwEVHTvvPmtrRa", 10000M },
                                {"xC871n3zV2FC5aixN3QTx339yNTkX6VYVR", 10000M },
                                {"xQShKiDE1UaicFf5x89zYSqAAeDhsx73V9", 10000M },
                                {"xCgihDgCmyhvirNsARphAj5BQWNcW5eg9c", 10000M },
                                {"xRD1eQKkL8UCQK1MKGDkn1o7JPvM9fKreu", 10000M },
                }; // Address, Amount in Decimal
            }

            return balanceSheet;
        }
        

    }
}
