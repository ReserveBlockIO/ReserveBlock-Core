namespace ReserveBlockCore.Models.SmartContracts
{
    public class WrappedFeature
    {
        public string WrappedName { get; set; }
        public string WrappedDescription { get; set; }
        public string WrappedForeignAssetName { get; set; }
        public string WrappedForeignAddress { get; set; }
        public string WrappedForeignChain { get; set; }
        public string WrappedForeignSignature { get; set; }
        public SmartContractAsset? WrappedForeignAsset { get; set; }
    }
}
