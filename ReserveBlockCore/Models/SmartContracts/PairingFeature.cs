namespace ReserveBlockCore.Models.SmartContracts
{
    public class PairingFeature
    {
        public string PairedSmartContractUID { get; set; }
        public string PairedSmartContractName { get; set; }
        public string PairedSmartContractDescription { get; set; }
        public string PairedSmartContractAssetName { get; set; }
        public string PairedSmartContractOwnerAddress { get; set; }
        public string OwnershipSignature { get; set; }
        public string PairingDescription { get; set; }
        public string PairingReason { get; set; }
        public SmartContractAsset PairedAsset { get; set; }

    }
}
