namespace ReserveBlockCore.Bitcoin.ElectrumX.Results
{
    public class BlockchainScripthashGetBalanceResult
    {
        /// <summary>
        /// Confirmed value in satoshis
        /// </summary>
        [Newtonsoft.Json.JsonProperty("confirmed")]
        public decimal Confirmed { get; set; }
        /// <summary>
        /// Unconfirmed value in satoshis
        /// </summary>
        [Newtonsoft.Json.JsonProperty("unconfirmed")]
        public decimal Unconfirmed { get; set; }

        public decimal TotalBalance => Confirmed + Unconfirmed;
    }
}
