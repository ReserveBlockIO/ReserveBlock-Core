namespace ReserveBlockCore.Bitcoin.ElectrumX.Results
{
    public class BlockchainEstimatefeeResult
    {
        /// <summary>
        /// The estimated transaction fee in coin units per kilobyte, as a floating point number.
        /// If the daemon does not have enough information to make an estimate, the integer -1 is returned.
        /// </summary>
        public decimal Fee { get; set; }
    }
}
