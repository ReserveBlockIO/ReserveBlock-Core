using Newtonsoft.Json;
using ReserveBlockCore.Models;

namespace ReserveBlockCore.Services
{
    public class FeeCalcService
    {
        public static decimal CalculateTXFee(Transaction tx)
        {
            var txFee = new decimal();
            var baseFeeMultiplier = 0.00001M;
            var kb = 1024.0M;

            var txSize = JsonConvert.SerializeObject(tx).Length;
            
            txFee = (txSize / kb) * baseFeeMultiplier;

            //base fee of 0.000003 must always exist
            if (txFee < 0.000003M)
            {
                txFee = 0.000003M;
            }
            else
            {
                //fees are now capped at 8 decimal places
                txFee = decimal.Round(txFee, 8);
            }
            return txFee;
        }

        public static decimal GetFeeMinimum()
        {
            if(Globals.LastBlock.Height > 9999999)
            {
                return 0.000003M;
            }
            else
            {
                return 0.0M;
            }
        }

        public static void CalculateDataFee()
        {
            //calc for data fee
        }
    }
}
