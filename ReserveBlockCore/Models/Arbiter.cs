namespace ReserveBlockCore.Models
{
    public class Arbiter
    {
        public string Address { get; set; }
        public string SigningAddress { get; set; }
        public string Title { get; set; }
        public int Generation { get; set; }
        public string IPAddress { get; set; }
        public long StartOfService { get; set; }
        public long? EndOfService { get; set;}
    }
}
