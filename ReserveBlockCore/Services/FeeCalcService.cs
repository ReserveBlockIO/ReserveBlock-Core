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

            return txFee;
        }

        public static void CalculateDataFee()
        {
            //calc for data fee 
        }
    }
}
