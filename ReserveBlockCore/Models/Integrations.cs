namespace ReserveBlockCore.Models
{
    public class Integrations
    {
        public class Network
        {
            public long Height { get; set; }
            public string Hash { get; set; }
            public DateTime LastBlockAddedTimeUTC { get; set; }
            public string CLIVersion { get; set; }
            public string GitHubVersion { get; set; }
            public int BlockVersion { get; set; }
            public int NetworkAgeInDays { get; set; }
            public int TotalSupply { get; set; }
        }
    }
}
