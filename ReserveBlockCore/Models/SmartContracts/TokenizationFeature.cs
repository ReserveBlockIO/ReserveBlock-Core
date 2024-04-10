namespace ReserveBlockCore.Models.SmartContracts
{
    public class TokenizationFeature
    {
        public string AssetName { get; set; }
        public string AssetTicker { get; set; }
        public string? KeyRevealRequestHash { get; set; }
        public string? DepositAddress { get; set; }
        public string? Share { get; set; }
        public string? BackupShare { get; set; }
    }
}
