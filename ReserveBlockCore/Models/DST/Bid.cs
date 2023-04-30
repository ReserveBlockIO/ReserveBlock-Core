using LiteDB;
using ReserveBlockCore.Data;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System.Net;
using System.Security.Cryptography;
using static ReserveBlockCore.Models.Mother;

namespace ReserveBlockCore.Models.DST
{
    public class Bid
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

        public bool Build(bool thirdParty = false)
        {
            if (BidAmount < Globals.BidMinimum)
                return false;

            if(string.IsNullOrEmpty(PurchaseKey))
                return false;

            Id = Guid.NewGuid();
            BidStatus = thirdParty ? BidStatus : BidStatus.Sent;
            IsAutoBid = false;
            BidSendTime = TimeUtil.GetTime();
            MaxBidAmount = BidAmount;
            BidSendReceive = BidSendReceive.Sent;

            var bidModifier = (BidAmount * Globals.BidModifier);
            var bidAmount = Convert.ToInt64(bidModifier);

            if(!RawBid)
            {
                var account = AccountData.GetSingleAccount(BidAddress);
                if (account == null)
                    return false;

                if (account.GetPrivKey == null)
                    return false;
                var message = $"{PurchaseKey}_{bidAmount}_{BidAddress}";


                var signature = SignatureService.CreateSignature(message, account.GetPrivKey, account.PublicKey);

                BidSignature = signature;
            }
            
            return true;
        }

        #region Get Bid Db
        public static LiteDB.ILiteCollection<Bid>? GetBidDb()
        {
            try
            {
                var bidDb = DbContext.DB_DST.GetCollection<Bid>(DbContext.RSRV_BID);
                return bidDb;
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError(ex.ToString(), "Bid.GetBidDb()");
                return null;
            }

        }

        #endregion

        #region Get All Bids
        public static IEnumerable<Bid>? GetAllBids(BidSendReceive? bidSendReceive = null)
        {
            var bidDb = GetBidDb();

            if (bidDb != null)
            {
                var bids = bidSendReceive == null ? bidDb.Query().Where(x => true).ToEnumerable() : bidDb.Query().Where(x => x.BidSendReceive == bidSendReceive).ToEnumerable();
                if (bids.Count() == 0)
                {
                    return null;
                }

                return bids;
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region Get Single Bid
        public static Bid? GetSingleBid(Guid bidId)
        {
            var bidDb = GetBidDb();

            if (bidDb != null)
            {
                var bid = bidDb.Query().Where(x => x.Id == bidId).FirstOrDefault();
                if (bid == null)
                {
                    return null;
                }

                return bid;
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region Get Listing Bids
        public static IEnumerable<Bid>? GetListingBids(int listingId, BidSendReceive? bidSendReceive = null)
        {
            var bidDb = GetBidDb();

            if (bidDb != null)
            {
                var bids = bidSendReceive == null ? bidDb.Query().Where(x => x.ListingId == listingId).ToEnumerable() :
                    bidDb.Query().Where(x => x.ListingId == listingId && x.BidSendReceive == bidSendReceive).ToEnumerable();
                if (bids.Count() == 0)
                {
                    return null;
                }

                return bids;
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region Get Bid By Status
        public static IEnumerable<Bid>? GetBidByStatus(BidStatus bidStatus, BidSendReceive? bidSendReceive = null)
        {
            var bidDb = GetBidDb();

            if (bidDb != null)
            {
                var bids = bidSendReceive == null ? bidDb.Query().Where(x => x.BidStatus == bidStatus).ToEnumerable() : 
                    bidDb.Query().Where(x => x.BidStatus == bidStatus && x.BidSendReceive == bidSendReceive).ToEnumerable();
                if (bids.Count() == 0)
                {
                    return null;
                }

                return bids;
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region Save Bid
        public static (bool, string) SaveBid(Bid bid, bool dontUpdate = false)
        {
            var singleBid = GetSingleBid(bid.Id);
            var bidDb = GetBidDb();
            if (singleBid == null)
            {
                if (bidDb != null)
                {
                    bidDb.InsertSafe(bid);
                    return (true, "Bid saved.");
                }
            }
            else
            {
                if (bidDb != null && !dontUpdate)
                {
                    bidDb.UpdateSafe(bid);
                    return (true, "Bid updated.");
                }
            }
            return (false, "Bid DB was null.");
        }

        #endregion

        #region Delete Bid
        public static (bool, string) DeleteBid(Guid bidId)
        {
            var singleBid = GetSingleBid(bidId);
            if (singleBid != null)
            {
                var bidDb = GetBidDb();
                if (bidDb != null)
                {
                    bidDb.DeleteSafe(bidId);
                    return (true, "Bid deleted.");
                }
                else
                {
                    return (false, "Bid DB was null.");
                }
            }
            return (false, "Bid was not present.");

        }

        #endregion

        #region Delete All Bids By Collection
        public static async Task<(bool, string)> DeleteAllBidsByCollection(int collectionId)
        {
            try
            {
                var bidDb = GetBidDb();
                if (bidDb != null)
                {
                    bidDb.DeleteManySafe(x => x.CollectionId == collectionId);
                    return (true, "Bids deleted.");
                }
                else
                {
                    return (false, "Bid DB was null.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Failed to delete. Error: {ex.ToString()}");
            }

        }

        #endregion

        #region Delete All Bids By Listing
        public static async Task<(bool, string)> DeleteAllBidsByListing(int listingId)
        {
            try
            {
                var bidDb = GetBidDb();
                if (bidDb != null)
                {
                    bidDb.DeleteManySafe(x => x.ListingId ==  listingId);
                    return (true, "Bids deleted.");
                }
                else
                {
                    return (false, "Bid DB was null.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Failed to delete. Error: {ex.ToString()}");
            }

        }

        #endregion

        #region Verify Bid Signature
        public static bool VerifyBidSignature(string keySign, decimal amount, string address, string bidSignature)
        {
            var bidModifier = (amount * Globals.BidModifier).ToString();
            var bidAmount = Convert.ToInt64(bidModifier);

            var bidMessage = $"{keySign}_{bidAmount}_{address}";
            var signatureVerify = SignatureService.VerifySignature(address, bidMessage, bidSignature);

            return signatureVerify;
        }
        #endregion

    }

    public enum BidStatus
    { 
        Accepted,
        Rejected,
        Sent,
        Received
    }

    public enum BidSendReceive
    {
        Sent,
        Received
    }


}
