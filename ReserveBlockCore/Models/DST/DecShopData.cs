namespace ReserveBlockCore.Models.DST
{
    public class DecShopData
    {
        public DecShop? DecShop { get; set; }
        public List<Collection>? Collections { get; set; }
        public List<Auction>? Auctions { get; set; }
        public List<Listing>? Listings { get; set; }
        public List<Bid>? Bids { get; set; }
    }
}
