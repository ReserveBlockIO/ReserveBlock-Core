using NBitcoin;
using ReserveBlockCore.Bitcoin.Models;

namespace ReserveBlockCore.Arbiter
{
    public class PostData
    {
        public class MultiSigSigningPostData 
        { 
            public string TransactionData { get; set;  }
            public List<CoinInput> ScriptCoinListData { get; set; }
            public string SCUID { get; set; }
            public string VFXAddress { get; set; }
            public string Signature { get; set; }
            public long Timestamp { get; set; }
            public decimal Amount { get; set; }
            public string UniqueId { get; set; }
        }
    }
}
