using Newtonsoft.Json;
using ReserveBlockCore.Models;

namespace ReserveBlockCore.Utilities
{
    public class MempoolSizeUtility
    {
        public static List<Transaction> SizeMempoolDown(List<Transaction> mempoolTxs)
        {
            var txList = new List<Transaction>();
            long sizeCount = 0;

            var mempoolSorted = mempoolTxs.OrderBy(x => x.Timestamp);

            foreach (var mempoolTx in mempoolSorted)
            {
                var result = VerifyTXSize(mempoolTx, sizeCount);
                if(result.Item1 == true)
                {
                    txList.Add(mempoolTx);
                    sizeCount += result.Item2;
                }
            }

            return txList;
        }

        private static (bool, long) VerifyTXSize(Transaction tx, long currentSize)
        {
            var txJsonSize = JsonConvert.SerializeObject(tx);
            var size = Convert.ToInt64(txJsonSize.Length);

            var mempoolSize = Convert.ToInt64(currentSize + size).ToSize(GenericExtensions.SizeUnits.MB);
            
            if (mempoolSize > 1.00M)
            {
                return (false, 0);
            }

            return (true, size);
        }
    }
}
