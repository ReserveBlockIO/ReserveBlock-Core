using LiteDB;
using System.Net;

namespace ReserveBlockCore.Models.DST
{
    public class BidQueue
    {
        [BsonId]
        public Guid Id { get; set; }
        public string BidAddress { get; set; }
        public string BidSignature { get; set; }
        public decimal BidAmount { get; set; }
        public decimal MaxBidAmount { get; set; }
        public bool IsBuyNow { get; set; }
        public bool IsAutoBid { get; set; }
        public bool RawBid { get; set; }
        public string PurchaseKey { get; set; }
        public BidStatus BidStatus { get; set; }
        public BidSendReceive BidSendReceive { get; set; }
        public long BidSendTime { get; set; }
        public bool? IsProcessed { get; set; }// Bid Queue Item
        public int ListingId { get; set; }
        public int CollectionId { get; set; }
        public IPEndPoint EndPoint { get; set; }
    }
}
