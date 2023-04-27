using LiteDB;
using ReserveBlockCore.Data;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Models.DST
{
    public class Auction
    {
        [BsonId]
        public int Id { get; set; }
        public decimal CurrentBidPrice { get; set; }
        public decimal MaxBidPrice { get; set; }
        public decimal IncrementAmount { get; set; }
        public bool IsReserveMet { get; set; }
        public bool IsAuctionOver { get; set; }
        public int ListingId { get; set; }
        public int CollectionId { get; set; }
        public string? CurrentWinningAddress { get; set; }
        public Guid? WinningBidId { get; set; }

        #region Get Auction Db
        public static LiteDB.ILiteCollection<Auction>? GetAuctionDb()
        {
            try
            {
                var auctionDb = DbContext.DB_DST.GetCollection<Auction>(DbContext.RSRV_AUCTION);
                return auctionDb;
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError(ex.ToString(), "Auction.GetAuctionDb()");
                return null;
            }

        }

        #endregion

        #region Get All Auctions
        public static IEnumerable<Auction>? GetAllAuctions()
        {
            var auctionDb = GetAuctionDb();

            if (auctionDb != null)
            {
                var auctions = auctionDb.Query().Where(x => true).ToEnumerable();
                if (auctions.Count() == 0)
                {
                    return null;
                }

                return auctions;
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region Get All Live Auctions
        public static IEnumerable<Auction>? GetAllLiveAuctions()
        {
            var auctionDb = GetAuctionDb();

            if (auctionDb != null)
            {
                var auctions = auctionDb.Query().Where(x => !x.IsAuctionOver).ToEnumerable();
                if (auctions.Count() == 0)
                {
                    return null;
                }

                return auctions;
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region Get All Live Auctions Count
        public static int GetLiveAuctionsCount()
        {
            var auctionDb = GetAuctionDb();

            if (auctionDb != null)
            {
                var auctions = auctionDb.Query().Where(x => !x.IsAuctionOver).ToEnumerable().Count();
                if (auctions == 0)
                {
                    return 0;
                }

                return auctions;
            }
            else
            {
                return 0;
            }
        }

        #endregion

        #region Get Single Auction
        public static Auction? GetSingleAuction(int auctionId)
        {
            var auctionDb = GetAuctionDb();

            if (auctionDb != null)
            {
                var auction = auctionDb.Query().Where(x => x.Id == auctionId).FirstOrDefault();
                if (auction == null)
                {
                    return null;
                }

                return auction;
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region Get Listing Auction
        public static Auction? GetListingAuction(int listingId)
        {
            var auctionDb = GetAuctionDb();

            if (auctionDb != null)
            {
                var auction = auctionDb.Query().Where(x => x.ListingId == listingId).FirstOrDefault();
                if (auction == null)
                {
                    return null;
                }

                return auction;
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region Save Auction
        public static (bool, string) SaveAuction(Auction auction)
        {
            var singleAuction = GetSingleAuction(auction.Id);
            var auctionDb = GetAuctionDb();
            if (singleAuction == null)
            {
                if (auctionDb != null)
                {
                    auctionDb.InsertSafe(auction);
                    return (true, "Auction saved.");
                }
            }
            else
            {
                if (auctionDb != null)
                {
                    auctionDb.UpdateSafe(auction);
                    return (true, "Auction updated.");
                }
            }
            return (false, "Auction DB was null.");
        }

        #endregion

        #region Delete Auction
        public static (bool, string) DeleteAuction(int auctionId)
        {
            var singleAuction = GetSingleAuction(auctionId);
            if (singleAuction != null)
            {
                var auctionDb = GetAuctionDb();
                if (auctionDb != null)
                {
                    auctionDb.DeleteSafe(auctionId);
                    return (true, "Auction deleted.");
                }
                else
                {
                    return (false, "Auction DB was null.");
                }
            }
            return (false, "Auction was not present.");

        }

        #endregion

        #region Delete All Auctions By Collection
        public static async Task<(bool, string)> DeleteAllAuctionsByCollection(int collectionId)
        {
            try
            {
                var auctionDb = GetAuctionDb();
                if (auctionDb != null)
                {
                    auctionDb.DeleteManySafe(x => x.CollectionId == collectionId);
                    return (true, "Auction deleted.");
                }
                else
                {
                    return (false, "Auction DB was null.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Failed to delete. Error: {ex.ToString()}");
            }

        }

        #endregion

        #region Delete All Auctions By Listing
        public static async Task<(bool, string)> DeleteAllAuctionsByListing(int listingId)
        {
            try
            {
                var auctionDb = GetAuctionDb();
                if (auctionDb != null)
                {
                    auctionDb.DeleteManySafe(x => x.ListingId == listingId);
                    return (true, "Auction deleted.");
                }
                else
                {
                    return (false, "Auction DB was null.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Failed to delete. Error: {ex.ToString()}");
            }

        }

        #endregion
    }
}
