using NBitcoin;

namespace ReserveBlockCore.Arbiter
{
    public class PostData
    {
        public record MultiSigSigningPostData(string TransactionData, List<ScriptCoin> ScriptCoinListData, string SCUID);
    }
}
