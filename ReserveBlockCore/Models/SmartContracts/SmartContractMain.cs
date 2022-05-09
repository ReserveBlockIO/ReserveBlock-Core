namespace ReserveBlockCore.Models
{
    public class SmartContractMain
    {
        public string Name { get; set; } //User Defined
        public string Description { get; set; } //User Defined
        public string Address { get; set; } //User Defined
        public string AssetId { get; set; } //User Defined - But not now
        public bool IsPublic { get; set; } //System Set
        public Guid SmartContractUID { get; set; }//System Set
        public string Signature { get; set; }//System Set
        public List<SmartContractFeatures> Features { get; set; }
    }
}
