using Microsoft.AspNetCore.Http;
using ReserveBlockCore.DST;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.DST;
using ReserveBlockCore.Utilities;
using Spectre.Console;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

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
            LogUtility.Log("Bid Processing Started", "AuctionEngine.StartBidProcessing()");
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
            LogUtility.Log("StartAuctioneer Started", "AuctionEngine.StartAuctioneer()");
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
                var listingDb = Listing.GetListingDb();
                var notStartedListingAuctions = listings.Where(x => x.IsAuctionStarted == false && x.IsAuctionEnded == false && x.StartDate <= DateTime.UtcNow).ToList();
                if(notStartedListingAuctions.Count() > 0)
                {
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

                var listingsNeedingEnding = listings.Where(x => x.IsAuctionStarted == true && x.IsAuctionEnded == false && x.EndDate <= DateTime.UtcNow).ToList();
                if(listingsNeedingEnding.Count > 0 )
                {
                    var auctionDb = Auction.GetAuctionDb();
                    if (listingDb != null)
                    {
                        foreach(var listing in listingsNeedingEnding)
                        {
                            var auction = Auction.GetListingAuction(listing.Id);
                            if(auction != null)
                            {
                                auction.IsAuctionOver = true;

                                listing.IsAuctionEnded = true;
                                listing.WinningAddress = auction.CurrentWinningAddress;
                                listing.FinalPrice = auction.CurrentBidPrice;

                                if(listing.ReservePrice != null)
                                {
                                    if(listing.ReservePrice.Value <= auction.CurrentBidPrice)
                                        auction.IsReserveMet = true;
                                }
                                else
                                {
                                    auction.IsReserveMet = true;
                                }

                                if(auctionDb != null)
                                {
                                    listingDb.UpdateSafe(listing);
                                    auctionDb.UpdateSafe(auction);

                                    if(auction.IsReserveMet)
                                    {
                                        //Create TX to start NFT send.
                                    }
                                }
                            }
                        }
                    }
                }

                var listingsThatAreCancelled = listings.Where(x => x.IsAuctionStarted == true && x.IsAuctionEnded == false && x.IsCancelled).ToList();
                if(listingsThatAreCancelled.Count > 0 )
                {
                    var auctionDb = Auction.GetAuctionDb();
                    if (listingDb != null)
                    {
                        foreach (var listing in listingsThatAreCancelled)
                        {
                            var auction = Auction.GetListingAuction(listing.Id);
                            if (auction != null)
                            {
                                auction.IsAuctionOver = true;

                                listing.IsAuctionEnded = true;
                                listing.WinningAddress = "CANCELLED";
                                listing.FinalPrice = auction.CurrentBidPrice;

                                if (auctionDb != null)
                                {
                                    listingDb.UpdateSafe(listing);
                                    auctionDb.UpdateSafe(auction);
                                }
                            }
                        }
                    }
                }
            }
        }

        public static void ProcessBidQueue()
        {
            var bidQueueList = Globals.BidQueue.ToList();
            if(bidQueueList.Count > 0)
            {
                var listings = Listing.GetAllStartedListings(); //get all listings and store into memory. 
                var auctions = Auction.GetAllLiveAuctions(); //get all auctions and store into memory.
                                                             
                if(auctions?.Count() > 0)
                {
                    foreach (var bid in Globals.BidQueue)
                    {
                        try
                        {
                            var auction = auctions.Where(x => x.ListingId == bid.ListingId).FirstOrDefault();

                            if (auction != null)
                            {
                                if (auction?.CurrentWinningAddress != bid.BidAddress)
                                {
                                    if ((auction?.CurrentBidPrice + auction?.IncrementAmount) < bid.MaxBidAmount)
                                    {
                                        var listing = listings.Where(x => x.Id == bid.ListingId).FirstOrDefault();
                                        if(listing != null)
                                        {
                                            if(listing.RequireBalanceCheck)
                                            {
                                                var addressBalance = AccountStateTrei.GetAccountBalance(bid.BidAddress);
                                                if(addressBalance <  bid.BidAmount)
                                                {
                                                    DequeueBid(bid, BidStatus.Rejected); 
                                                    continue;
                                                }
                                            }
                                            Bid aBid = new Bid
                                            {
                                                Id = Guid.NewGuid(),
                                                BidAddress = bid.BidAddress,
                                                BidAmount = bid.BidAmount,
                                                BidSendReceive = BidSendReceive.Received,
                                                BidSendTime = bid.BidSendTime,
                                                BidSignature = bid.BidSignature,
                                                BidStatus = BidStatus.Accepted,
                                                CollectionId = bid.CollectionId,
                                                IsAutoBid = bid.IsAutoBid,
                                                IsBuyNow = bid.IsBuyNow,
                                                IsProcessed = true,
                                                ListingId = bid.ListingId,
                                                MaxBidAmount = bid.MaxBidAmount,
                                            };

                                            auction.CurrentBidPrice = bid.MaxBidAmount;
                                            auction.MaxBidPrice = bid.MaxBidAmount;
                                            auction.CurrentWinningAddress = bid.BidAddress;
                                            auction.IncrementAmount = GetIncrementBidAmount(auction.MaxBidPrice);//update increment amount for next bid.

                                            Bid.SaveBid(aBid, true);
                                            Auction.SaveAuction(auction);
                                            DequeueBid(bid, BidStatus.Accepted);
                                            continue;
                                        }
                                        else { DequeueBid(bid, BidStatus.Rejected); continue; }
                                    }
                                    else { DequeueBid(bid, BidStatus.Rejected); continue; }
                                }
                                else { DequeueBid(bid, BidStatus.Rejected); continue; }
                            }
                            else { DequeueBid(bid, BidStatus.Rejected); continue; }
                        }
                        catch(Exception ex) { ErrorLogUtility.LogError($"Bid Error (01): {ex.ToString}", "AuctionEngine.ProcessBidQueue()"); }
                        
                    }
                }
                else
                {
                    foreach (var bid in bidQueueList)
                    {
                        DequeueBid(bid, BidStatus.Rejected);
                    }
                }
            }
        }

        public static void DequeueBid(BidQueue bid, BidStatus bidStatus)
        {
            Globals.BidQueue.TryDequeue(out _);
            Message message = new Message
            {
                Type = MessageType.Bid,
                ComType = MessageComType.Response,
                Data = $"{bid.Id},{bidStatus}",
            };

            _ = DSTClient.SendClientMessageFromShop(message, bid.EndPoint, false);
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
