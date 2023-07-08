namespace ReserveBlockCore.Models.SmartContracts
{
    public class TokenFeature
    {
        public string TokenName { get; set; }
        public string TokenTicker { get; set; }
        public int TokenDecimalPlaces { get; set; }
        public long TokenSupply { get; set; }
        public bool TokenBurnable { get; set; }
        public bool TokenVoting { get; set; }
        public bool TokenMintable { get; set; }
        public string? TokenImageURL { get; set; }
        public string? TokenImageBase { get; set; }

        public static TokenFeature CreateTokenFeature(Token token)
        {
            TokenFeature tokenFeature = new TokenFeature {
                TokenBurnable = token.IsBurningEnabled,
                TokenDecimalPlaces = token.DecimalPlaces,
                TokenImageBase = token.TokenImageBase,
                TokenImageURL = token.TokenImageURL,
                TokenMintable = token.IsTotalSupplyInfinite,
                TokenName = token.Name,
                TokenTicker = token.Ticker,
                TokenSupply = token.TotalSupply,
                TokenVoting = token.IsVotingEnabled
            };

            return tokenFeature;
        }
    }
}
