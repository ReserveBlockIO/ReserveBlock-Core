namespace ReserveBlockCore.Bitcoin.Models
{
    public class BTCTokenizeTransaction
    {
        /// <summary>
        /// Smart Contract ID
        /// </summary>
        /// <example>somescguid:1234</example>
        public string SCUID { get; set; }

        /// <summary>
        /// From VFX Address
        /// </summary>
        /// <example>fromVFX</example>
        public string? FromAddress { get; set; }

        /// <summary>
        /// To VFX Address
        /// </summary>
        /// <example>toVFX</example>
        public string ToAddress { get; set; }

        /// <summary>
        /// Amount RBX Address
        /// </summary>
        /// <example>1.23</example>
        public decimal Amount { get; set; }
    }
}
