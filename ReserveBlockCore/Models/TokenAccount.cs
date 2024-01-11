namespace ReserveBlockCore.Models
{
    public class TokenAccount
    {
        public string SmartContractUID { get; set; }
        public string TokenName { get; set; }
        public string TokenTicker { get; set; }
        public decimal Balance { get; set; }
        public decimal LockedBalance { get; set; }
        public int DecimalPlaces { get; set; }

        public static TokenAccount CreateTokenAccount(string scUID, string tokenName, string tokenTicker, decimal balance, int decimalPlaces)
        {
            TokenAccount tokenAccount = new TokenAccount { 
                SmartContractUID = scUID,
                TokenName = tokenName,
                Balance = balance,
                LockedBalance = 0.0M,
                TokenTicker = tokenTicker,
                DecimalPlaces = decimalPlaces
            };

            return tokenAccount;
        }
    }
}
