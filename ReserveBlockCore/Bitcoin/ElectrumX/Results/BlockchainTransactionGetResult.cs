namespace ReserveBlockCore.Bitcoin.ElectrumX.Results
{
    public class BlockchainTransactionGetConfirmsResult
    {
        /// <summary>
        /// The raw transaction as a hexadecimal string.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("confirmations")]
        public int Confirmations { get; set; }
    }
}
