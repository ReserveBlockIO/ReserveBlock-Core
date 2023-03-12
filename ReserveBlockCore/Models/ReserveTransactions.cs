using LiteDB;

namespace ReserveBlockCore.Models
{
    public class ReserveTransactions
    {
        [BsonId]
        public Guid Id { get; set; }
        public Transaction Transaction { get; set; }
        public long ConfirmTimestamp { get; set; } //will not be valid till after this.
        public string FromAddress { get; set; }
        public string ToAddress { get; set; }
        
    }
}
