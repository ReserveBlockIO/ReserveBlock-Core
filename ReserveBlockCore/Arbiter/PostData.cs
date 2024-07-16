using NBitcoin;

namespace ReserveBlockCore.Arbiter
{
    public class PostData
    {
        public record MultiSigSigningPostData(NBitcoin.Transaction TransactionData, List<ScriptCoin> ScriptCoinListData, string SCUID);
    }
}
