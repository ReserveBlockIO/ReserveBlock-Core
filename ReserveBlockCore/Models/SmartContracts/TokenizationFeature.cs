namespace ReserveBlockCore.Models.SmartContracts
{
    public class TokenizationFeature
    {
        public string AssetName { get; set; }
        public string AssetTicker { get; set; }
        public string DepositAddress { get; set; }
        public string PublicKeyProofs { get; set; }
        public string? ImageBase { get; set; }
    }
}
