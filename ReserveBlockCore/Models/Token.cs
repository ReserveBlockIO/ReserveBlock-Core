namespace ReserveBlockCore.Models
{
    public class Token
    {
        public string Name { get; set; }
        public string Ticker { get; set; }
        public int DecimalPlaces { get; set; }
        public int TotalSupply { get; set; }
        public bool IsVotingEnabled { get; set; }
        public bool IsBurningEnabled { get; set; }
        public string? TokenImageURL { get; set; }
        public string? TokenImageBase { get; set; }
        public bool IsTotalSupplyInfinite { get { return TotalSupply == 0 ? true : false; } }
    }
}
