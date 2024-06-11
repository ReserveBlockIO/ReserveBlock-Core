using ReserveBlockCore.Models;

namespace ReserveBlockCore.Bitcoin.Models
{
    public class BTCTokenizePayload
    {
        /// <summary>
        /// VFX Address to assign to
        /// </summary>
        /// <example>fromVFXAddress</example>
        public string RBXAddress { get; set; }
        /// <summary>
        /// Name of the contract
        /// </summary>
        /// <example>sSomeName</example>
        public string Name { get; set; }
        /// <summary>
        /// Contract Description
        /// </summary>
        /// <example>SomeDescription</example>
        public string Description { get; set; }
        /// <summary>
        /// Smart Contract ID
        /// </summary>
        /// <example>Image Asset File Location</example>
        public string FileLocation { get; set; }

        /// <summary>
        /// May only contain Multi-Asset at this point and time.
        /// </summary>
        /// <example>ListOfThings</example>
        public List<SmartContractFeatures>? Features { get; set; }
    }
}
