namespace ReserveBlockCore.Models.SmartContracts
{
    public class TokenizationFeature
    {
        public string AssetName { get; set; }
        public string AssetTicker { get; set; }
        public bool KeyRevealed { get; set; }
        public string KeyRevealRequestHash { get; set; }
    }
}
