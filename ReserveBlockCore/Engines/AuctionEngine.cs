using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using ReserveBlockCore.DST;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.DST;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using Spectre.Console;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace ReserveBlockCore.Engines
{
    public class AuctionEngine
    {
        static SemaphoreSlim BidProcessingLock = new SemaphoreSlim(1, 1);
        static SemaphoreSlim AuctioneerLock = new SemaphoreSlim(1, 1);
        static bool BidProcessingOn = false;
        static ConcurrentDictionary<int, int> ListingPostSaleDict = new ConcurrentDictionary<int, int>(); 

        public static async Task StartBidProcessing()
        {
            BidProcessingOn = true;
            AuctionLogUtility.Log("Bid Processing Started", "AuctionEngine.StartBidProcessing()");
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
                    ProcessBuyNowQueue();
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
            AuctionLogUtility.Log("StartAuctioneer Started", "AuctionEngine.StartAuctioneer()");
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
                    ProcessSales();
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
                    AuctionLogUtility.Log($"New Auctions Found, ready to be started. Count: {notStartedListingAuctions.Count()}", "AuctionEngine.ProcessAuctions()");
                    if (listingDb != null)
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
                                        AuctionLogUtility.Log($"Auction Created for Listing: {newAuction.ListingId}", "AuctionEngine.ProcessAuctions()");
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
                    AuctionLogUtility.Log($"Auctions Ending Count: {listingsNeedingEnding.Count}.", "AuctionEngine.ProcessAuctions()-1");
                    var auctionDb = Auction.GetAuctionDb();
                    if (listingDb != null)
                    {
                        foreach(var listing in listingsNeedingEnding)
                        { 
                            AuctionLogUtility.Log($"Processing Listing: {listing.Id}.", "AuctionEngine.ProcessAuctions()-2");
                            var auction = Auction.GetListingAuction(listing.Id);
                            if(auction != null)
                            {
                                AuctionLogUtility.Log($"Processing Auction {auction.Id}.", "AuctionEngine.ProcessAuctions()-3");
                                var bids = Bid.GetListingBids(listing.Id);
                                if(bids?.Count() > 0)
                                {
                                    AuctionLogUtility.Log($"Auction Has Bids. Count: {bids?.Count()}.", "AuctionEngine.ProcessAuctions()-4");
                                    auction.IsAuctionOver = true;

                                    listing.IsAuctionEnded = true;
                                    listing.WinningAddress = auction.CurrentWinningAddress;
                                    listing.FinalPrice = auction.MaxBidPrice;

                                    if (listing.ReservePrice != null)
                                    {
                                        if (listing.ReservePrice.Value <= auction.CurrentBidPrice)
                                        {
                                            auction.IsReserveMet = true;
                                        }
                                        else
                                        {
                                            listing.WinningAddress = null;
                                        }
                                    }
                                    else
                                    {
                                        auction.IsReserveMet = true;
                                    }

                                    AuctionLogUtility.Log($"Auction Reserve Met?: {auction.IsReserveMet}.", "AuctionEngine.ProcessAuctions()-5");

                                    if (auctionDb != null)
                                    {
                                        Auction.SaveAuction(auction);
                                        _ = Listing.SaveListing(listing);

                                        if (auction.IsReserveMet && listing.WinningAddress != null)
                                        {
                                            AuctionLogUtility.Log($"Auction Processing", "AuctionEngine.ProcessAuctions()-6");
                                            //Create TX to start NFT send.
                                            if (listing.RequireBalanceCheck)
                                            {
                                                AuctionLogUtility.Log($"Balance Check Required", "AuctionEngine.ProcessAuctions()-7");
                                                var addressBalance = AccountStateTrei.GetAccountBalance(listing.WinningAddress);
                                                if (addressBalance >= listing.FinalPrice)
                                                {
                                                    if(auction.WinningBidId != null)
                                                    {
                                                        var winningBid = Bid.GetSingleBid(auction.WinningBidId.Value);
                                                        AuctionLogUtility.Log($"Balance Check Passed. Sending TX", "AuctionEngine.ProcessAuctions()-8");
                                                        _ = SmartContractService.StartSaleSmartContractTX(listing.SmartContractUID, listing.WinningAddress, listing.FinalPrice.Value, listing, winningBid);
                                                        continue;
                                                    }
                                                    else
                                                    {
                                                        AuctionLogUtility.Log($"Winning Bid ID was missing. This cannot be missing.", "AuctionEngine.ProcessAuctions()-8");
                                                    }
                                                }
                                                else
                                                {
                                                    AuctionLogUtility.Log($"Balance Check Failed.", "AuctionEngine.ProcessAuctions()-9");
                                                    //high bidder does not have balance.
                                                    //perhaps fall back to previous bid?
                                                }
                                            }
                                            else
                                            {
                                                if (auction.WinningBidId != null)
                                                {
                                                    var winningBid = Bid.GetSingleBid(auction.WinningBidId.Value);
                                                    AuctionLogUtility.Log($"No Balance Check Needed. Sending TX", "AuctionEngine.ProcessAuctions()-10");
                                                    _ = SmartContractService.StartSaleSmartContractTX(listing.SmartContractUID, listing.WinningAddress, listing.FinalPrice.Value, listing, winningBid);
                                                    continue;
                                                }
                                            }
                                            
                                        }
                                    }
                                }
                                else
                                {
                                    AuctionLogUtility.Log($"Auction had zero (0) bids.", "AuctionEngine.ProcessAuctions()-11");
                                    //auction did not get any bids
                                    auction.IsAuctionOver = true;
                                    auction.IsReserveMet = false;

                                    listing.IsAuctionEnded = true;
                                    listing.WinningAddress = null;
                                    listing.FinalPrice = auction.CurrentBidPrice;

                                    Auction.SaveAuction(auction);
                                    _ = Listing.SaveListing(listing);
                                }
                            }
                        }
                    }
                }

                var listingsThatAreCancelled = listings.Where(x => x.IsAuctionStarted == true && x.IsAuctionEnded == false && x.IsCancelled).ToList();
                if(listingsThatAreCancelled.Count > 0 )
                {
                    AuctionLogUtility.Log($"Auction Listings that were cancelled: {listingsThatAreCancelled.Count}", "AuctionEngine.ProcessAuctions()");
                    var auctionDb = Auction.GetAuctionDb();
                    if (listingDb != null)
                    {
                        foreach (var listing in listingsThatAreCancelled)
                        {
                            var auction = Auction.GetListingAuction(listing.Id);
                            if (auction != null)
                            {
                                auction.IsAuctionOver = true;
                                auction.IsReserveMet = false;

                                listing.IsAuctionEnded = true;
                                listing.WinningAddress = null;
                                listing.FinalPrice = auction.MaxBidPrice;

                                if (auctionDb != null)
                                {
                                    listingDb.UpdateSafe(listing);
                                    auctionDb.UpdateSafe(auction);
                                    AuctionLogUtility.Log($"Auction Listing cancelled Id: {listing.Id}", "AuctionEngine.ProcessAuctions()");
                                }
                            }
                        }
                    }
                }
            }
        }
        public static void ProcessBuyNowQueue()
        {
            var buyNowQueueList = Globals.BuyNowQueue.ToList();
            if (buyNowQueueList.Count > 0)
            {
                AuctionLogUtility.Log($"Buy Now Bids Detected. Count: {buyNowQueueList.Count}", "AuctionEngine.ProcessBuyNowQueue()");
                var listings = Listing.GetAllStartedListings(); //get all listings and store into memory. 
                var auctions = Auction.GetAllLiveAuctions(); //get all auctions and store into memory.

                if (auctions?.Count() > 0)
                {
                    foreach (var buyNow in buyNowQueueList)
                    {
                        try
                        {
                            AuctionLogUtility.Log($"Processing BuyNow for Listing: {buyNow.ListingId}", "AuctionEngine.ProcessBuyNowQueue()");
                            if (!buyNow.IsBuyNow)
                            {
                                AuctionLogUtility.Log($"Failed to process as it was not flagged as IsBuyNow = true. Listing: {buyNow.ListingId}", "AuctionEngine.ProcessBuyNowQueue()");
                                DequeueBid(buyNow, BidStatus.Rejected);
                                continue;
                            }
                            var auction = auctions.Where(x => x.ListingId == buyNow.ListingId).FirstOrDefault();
                            
                            if (auction != null)
                            {
                                var listing = listings?.Where(x => x.Id == buyNow.ListingId).FirstOrDefault();
                                if (listing != null)
                                {
                                    if(listing.BuyNowPrice == null)
                                    {
                                        AuctionLogUtility.Log($"Buy Now Rejected - Buy Now Price was Null. Listing: {buyNow.ListingId}", "AuctionEngine.ProcessBuyNowQueue()");
                                        DequeueBid(buyNow, BidStatus.Rejected);
                                        continue;
                                    }
                                    if (listing.RequireBalanceCheck)
                                    {
                                        var addressBalance = AccountStateTrei.GetAccountBalance(buyNow.BidAddress);
                                        if (addressBalance < listing.BuyNowPrice)
                                        {
                                            AuctionLogUtility.Log($"Buy Now Rejected - Address balance too low. Listing: {buyNow.ListingId}", "AuctionEngine.ProcessBuyNowQueue()");
                                            DequeueBid(buyNow, BidStatus.Rejected);
                                            continue;
                                        }

                                    }
                                    if (buyNow.BidAddress.StartsWith("xRBX"))
                                    {
                                        AuctionLogUtility.Log($"Buy Now Rejected - Buy Now cannot be purchases from Reserve Account. Listing: {buyNow.ListingId}", "AuctionEngine.ProcessBuyNowQueue()");
                                        DequeueBid(buyNow, BidStatus.Rejected);
                                        continue;
                                    }

                                    if(auction.IsAuctionOver)
                                    {
                                        AuctionLogUtility.Log($"Buy Now Rejected - Auction is already over. Listing: {buyNow.ListingId}", "AuctionEngine.ProcessBuyNowQueue()");
                                        DequeueBid(buyNow, BidStatus.Rejected);
                                        continue;
                                    }

                                    Bid aBid = new Bid
                                    {
                                        Id = Guid.NewGuid(),
                                        BidAddress = buyNow.BidAddress,
                                        BidAmount = listing.BuyNowPrice.Value,
                                        BidSendReceive = BidSendReceive.Received,
                                        BidSendTime = buyNow.BidSendTime,
                                        BidSignature = buyNow.BidSignature,
                                        BidStatus = BidStatus.Accepted,
                                        CollectionId = buyNow.CollectionId,
                                        IsAutoBid = buyNow.IsAutoBid,
                                        IsBuyNow = true,
                                        IsProcessed = true,
                                        ListingId = buyNow.ListingId,
                                        MaxBidAmount = listing.BuyNowPrice.Value,
                                        PurchaseKey = buyNow.PurchaseKey
                                    };

                                    auction.CurrentBidPrice = listing.BuyNowPrice.Value;
                                    auction.MaxBidPrice = listing.BuyNowPrice.Value;
                                    auction.CurrentWinningAddress = buyNow.BidAddress;                                        
                                    auction.IncrementAmount = GetIncrementBidAmount(auction.MaxBidPrice);//update increment amount for next bid.
                                    auction.IsAuctionOver = true;
                                    auction.IsReserveMet = true;
                                    auction.WinningBidId = aBid.Id;

                                    listing.IsAuctionEnded = true;
                                    listing.WinningAddress = auction.CurrentWinningAddress;
                                    listing.FinalPrice = listing.BuyNowPrice.Value;

                                    Bid.SaveBid(aBid, true);
                                    Auction.SaveAuction(auction);
                                    _ = Listing.SaveListing(listing);

                                    DequeueBid(buyNow, BidStatus.Accepted);
                                    AuctionLogUtility.Log($"Buy Now Accepted - Sending TX now. Listing: {buyNow.ListingId}", "AuctionEngine.ProcessBuyNowQueue()");

                                    _ = SmartContractService.StartSaleSmartContractTX(listing.SmartContractUID, listing.WinningAddress, listing.FinalPrice.Value, listing, aBid);

                                    continue;
                                }
                                else
                                {
                                    AuctionLogUtility.Log($"Buy Now Rejected: {buyNow.ListingId}. The listing was null.", "AuctionEngine.ProcessBuyNowQueue()");
                                    continue;
                                }
                            }
                            else
                            {
                                AuctionLogUtility.Log($"Buy Now Rejected: {buyNow.ListingId}. The Auction was null.", "AuctionEngine.ProcessBuyNowQueue()");
                                continue;
                            }
                        }
                        catch(Exception ex) { AuctionLogUtility.Log($"Unknown Error for: {buyNow.ListingId}. Error: {ex.ToString()}", "AuctionEngine.ProcessBuyNowQueue()"); }
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
                AuctionLogUtility.Log($"Bids detected. Count: {bidQueueList.Count}", "AuctionEngine.ProcessBidQueue()");
                if (auctions?.Count() > 0)
                {
                    foreach (var bid in bidQueueList)
                    {
                        try
                        {
                            var auction = auctions.Where(x => x.ListingId == bid.ListingId).FirstOrDefault();
                            AuctionLogUtility.Log($"Processing bid for Listing: {bid.ListingId}", "AuctionEngine.ProcessBidQueue()");
                            if (auction != null)
                            {
                                if (auction?.CurrentWinningAddress != bid.BidAddress)
                                {
                                    if ((auction?.CurrentBidPrice + auction?.IncrementAmount) <= bid.MaxBidAmount)
                                    {
                                        var listing = listings?.Where(x => x.Id == bid.ListingId).FirstOrDefault();
                                        if(listing != null)
                                        {
                                            if(listing.RequireBalanceCheck)
                                            {
                                                var addressBalance = AccountStateTrei.GetAccountBalance(bid.BidAddress);
                                                if(addressBalance <  bid.BidAmount)
                                                {
                                                    AuctionLogUtility.Log($"Bid Rejected - Balance was too low. Listing: {bid.ListingId}", "AuctionEngine.ProcessBidQueue()");
                                                    DequeueBid(bid, BidStatus.Rejected); 
                                                    continue;
                                                }
                                            }
                                            if (bid.BidAddress.StartsWith("xRBX"))
                                            {
                                                AuctionLogUtility.Log($"Bid Rejected - Cannot bid with Reserve Account. Listing: {bid.ListingId}", "AuctionEngine.ProcessBidQueue()");
                                                DequeueBid(bid, BidStatus.Rejected);
                                                continue;
                                            }

                                            if(listing.IsBuyNowOnly)
                                            {
                                                AuctionLogUtility.Log($"Bid Rejected - This auction is a buy now only. Listing: {bid.ListingId}", "AuctionEngine.ProcessBidQueue()");
                                                DequeueBid(bid, BidStatus.Rejected);
                                                continue;
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
                                            auction.WinningBidId = aBid.Id;

                                            Bid.SaveBid(aBid, true);
                                            Auction.SaveAuction(auction);
                                            _ = Listing.SaveListing(listing);

                                            AuctionLogUtility.Log($"Bid Accepted. Listing: {bid.ListingId} | Bid {aBid.Id}", "AuctionEngine.ProcessBidQueue()");

                                            DequeueBid(bid, BidStatus.Accepted);
                                            continue;
                                        }
                                        else { DequeueBid(bid, BidStatus.Rejected); AuctionLogUtility.Log($"Bid Rejected - Listing was null: {bid.ListingId}", "AuctionEngine.ProcessBidQueue()"); continue; }
                                    }
                                    else { DequeueBid(bid, BidStatus.Rejected); AuctionLogUtility.Log($"Bid Rejected - Increment amount was wrong. Listing: {bid.ListingId}", "AuctionEngine.ProcessBidQueue()"); continue; }
                                }
                                else { DequeueBid(bid, BidStatus.Rejected); AuctionLogUtility.Log($"Bid Rejected - Already winning auction. Listing: {bid.ListingId}", "AuctionEngine.ProcessBidQueue()"); continue; }
                            }
                            else { DequeueBid(bid, BidStatus.Rejected); AuctionLogUtility.Log($"Bid Rejected - Auction was null. Listing: {bid.ListingId}", "AuctionEngine.ProcessBidQueue()"); continue; }
                        }
                        catch(Exception ex) { AuctionLogUtility.Log($"Bid Error (01): {ex.ToString}", "AuctionEngine.ProcessBidQueue()"); }
                        
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

        public static void ProcessSales()
        {
            try
            {
                var listings = Listing.GetAllListings();
                if (listings?.Count() > 0)
                {
                    var listingDb = Listing.GetListingDb();
                    var listingsNeedingSaleTX = listings.Where(x => x.IsAuctionStarted && x.IsAuctionEnded && !x.IsSaleTXSent && x.WinningAddress != null && !x.SaleHasFailed).ToList();
                    if (listingsNeedingSaleTX.Count > 0)
                    {
                        foreach(var listing in  listingsNeedingSaleTX)
                        {
                            //this is to give it time for first send to succeed and avoid double send.
                            if(ListingPostSaleDict.TryGetValue(listing.Id, out int value))
                            {
                                if(value > 3)
                                {
                                    listing.SaleHasFailed = true;
                                    _ = Listing.SaveListing(listing);
                                    ListingPostSaleDict.TryRemove(listing.Id, out value);
                                }
                                if(value > 1)
                                {
                                    var auction = Auction.GetListingAuction(listing.Id);
                                    if(auction != null)
                                    {
                                        if(auction.WinningBidId != null)
                                        {
                                            var winningBid = Bid.GetSingleBid(auction.WinningBidId.Value);
                                            if(winningBid != null)
                                                _ = SmartContractService.StartSaleSmartContractTX(listing.SmartContractUID, listing.WinningAddress, listing.FinalPrice.Value, listing, winningBid);
                                        }
                                        
                                    }
                                    
                                }

                                value += 1;
                                ListingPostSaleDict[listing.Id] = value;
                            }
                            else
                            {
                                ListingPostSaleDict.TryAdd(listing.Id, 0);
                            }
                        }
                    }
                }
            }
            catch(Exception ex)
            {

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
