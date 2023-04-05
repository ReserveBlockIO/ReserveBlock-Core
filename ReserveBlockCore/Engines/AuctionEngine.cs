using ReserveBlockCore.Models.DST;
using System.Collections.Concurrent;
using System.Net;

namespace ReserveBlockCore.Engines
{
    public class AuctionEngine
    {
        static SemaphoreSlim BidProcessingLock = new SemaphoreSlim(1, 1);
        static SemaphoreSlim AuctioneerLock = new SemaphoreSlim(1, 1);
        static bool BidProcessingOn = false;

        public static async Task StartBidProcessing()
        {
            BidProcessingOn = true;

            while (BidProcessingOn)
            {
                var delay = Task.Delay(300);
                if (Globals.StopAllTimers && !Globals.IsChainSynced)
                {
                    await delay;
                    continue;
                }
                await BidProcessingLock.WaitAsync();
                try
                {
                    ProcessBidQueue();
                }
                finally
                {
                    BidProcessingLock.Release();
                }

                await delay;
            }
        }
        
        public static async Task StartAuctioneer()
        {
            while (true)
            {
                var delay = Task.Delay(10000);
                if (Globals.StopAllTimers && !Globals.IsChainSynced)
                {
                    await delay;
                    continue;
                }
                await AuctioneerLock.WaitAsync();
                try
                {
                    ProcessAuctions();
                }
                finally
                {
                    AuctioneerLock.Release();
                }

                await delay;
            }
        }

        public static void StopBidProcessing()
        {
            if (BidProcessingOn)
                BidProcessingOn = false;
        }

        public static void ProcessAuctions()
        {
            var listings = Listing.GetAllListings();
            if(listings?.Count() > 0)
            {
                var notStartedListingAuctions = listings.Where(x => x.IsAuctionStarted == false && x.IsAuctionEnded == false && x.StartDate <= DateTime.UtcNow).ToList();
                if(notStartedListingAuctions.Count() > 0)
                {
                    var listingDb = Listing.GetListingDb();
                    if(listingDb != null)
                    {
                        foreach (var listing in notStartedListingAuctions)
                        {
                            try
                            {
                                listing.IsAuctionStarted = true;
                                listingDb.UpdateSafe(listing);

                                var auctionCheck = Auction.GetListingAuction(listing.Id);

                                if(auctionCheck == null)
                                {
                                    Auction newAuction = new Auction();
                                    newAuction.CurrentBidPrice = listing.FloorPrice != null ? listing.FloorPrice.Value : 0.0M;
                                    newAuction.MaxBidPrice = listing.FloorPrice != null ? listing.FloorPrice.Value : 0.0M;
                                    newAuction.IncrementAmount = GetIncrementBidAmount(newAuction.CurrentBidPrice);
                                    newAuction.CollectionId = listing.CollectionId;
                                    newAuction.ListingId = listing.Id;
                                    newAuction.CurrentWinningAddress = listing.AddressOwner;

                                    var auctionDb = Auction.GetAuctionDb();
                                    if (auctionDb != null)
                                    {
                                        auctionDb.InsertSafe(newAuction);
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
        }

        public static void ProcessBidQueue()
        {
            //Save bid, assign status.
            foreach(var bid in Globals.BidQueue)
            {
                var listing = Listing.GetSingleListing(bid.ListingId);
                var auction = Auction.GetListingAuction(bid.ListingId); 
                if (listing == null || auction == null)
                {
                    DequeueBid(); 
                    continue;
                }

                DequeueBid();
            }
        }

        public static void DequeueBid()
        {
            Globals.BidQueue.TryDequeue(out _);
        }

        public static decimal GetIncrementBidAmount(decimal CurrentBidPrice)
        {
            double incrementBidAmount = 0;
            double CurBidPrice = (double)CurrentBidPrice;
            if (CurBidPrice >= 0.00 && CurBidPrice < 0.99)
                incrementBidAmount = 0.01;
            else if (CurBidPrice >= 1.00 && CurBidPrice <= 4.99)
                incrementBidAmount = 0.5;
            else if (CurBidPrice >= 5.00 && CurBidPrice <= 24.99)
                incrementBidAmount = 0.5;
            else if (CurBidPrice >= 25.00 && CurBidPrice <= 99.99)
                incrementBidAmount = 1.00;
            else if (CurBidPrice >= 100 && CurBidPrice <= 249.99)
                incrementBidAmount = 1.00;
            else if (CurBidPrice >= 250 && CurBidPrice <= 499.99)
                incrementBidAmount = 5.00;
            else if (CurBidPrice >= 500.00 && CurBidPrice <= 999.99)
                incrementBidAmount = 10.00;
            else if (CurBidPrice >= 1000.00 && CurBidPrice <= 2499.99)
                incrementBidAmount = 50.00;
            else if (CurBidPrice >= 2500.00 && CurBidPrice <= 4999.99)
                incrementBidAmount = 75.00;
            else if (CurBidPrice >= 5000.00)
                incrementBidAmount = 100.00;

            return (decimal)incrementBidAmount;
        }
    }
}
