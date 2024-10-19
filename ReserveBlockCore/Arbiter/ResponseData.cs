using ReserveBlockCore.Bitcoin.Models;

namespace ReserveBlockCore.Arbiter
{
    public class ResponseData
    {
        public class MultiSigSigningResponse
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public string SignedTransaction { get; set; }
            public TokenizedWithdrawals TokenizedWithdrawals { get; set; }
        }
    }
}
