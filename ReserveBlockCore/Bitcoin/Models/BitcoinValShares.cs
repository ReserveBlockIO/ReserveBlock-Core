using ReserveBlockCore.Arbiter;

namespace ReserveBlockCore.Bitcoin.Models
{
    public class BitcoinValShares
    {
        public long CreateDate { get; set; }
        public Shares Share { get; set; }
        public long RemoveDate { get; set; }
    }
}
