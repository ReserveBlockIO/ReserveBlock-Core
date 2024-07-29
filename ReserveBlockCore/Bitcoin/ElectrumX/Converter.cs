namespace ReserveBlockCore.Bitcoin.ElectrumX
{
    public static class Converter
    {
        public const long SATOSHIS_IN_COIN = 100000000;

        public static long ConvertToSatoshi(string amountInCoin)
        {
            var success = double.TryParse(amountInCoin, out var amountD);
            if (success)
                return (long)(amountD * SATOSHIS_IN_COIN);
            return -1;
        }

        public static decimal ConvertToCoin(string amountInCoin)
        {
            var success = decimal.TryParse(amountInCoin, out var amountD);
            if (success)
                return (amountD / SATOSHIS_IN_COIN);
            return -1;
        }

        public static long CoinToSatoshi(decimal amountInCoin)
        {
            return (long)(amountInCoin * SATOSHIS_IN_COIN);
        }

        public static double SatoshiToCoin(long amountInCoin)
        {
            return amountInCoin / (double)SATOSHIS_IN_COIN;
        }
    }
}
