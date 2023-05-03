using LiteDB;
using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.Utilities;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace ReserveBlockCore.Models.DST
{
    public class Listing
    {
        [BsonId]
        public int Id { get; set; }
        public string SmartContractUID { get; set; }
        public string AddressOwner { get; set; }
        public decimal? BuyNowPrice { get; set; }
        public bool IsBuyNowOnly { get; set; }
        public bool IsRoyaltyEnforced { get; set; }
        public string PurchaseKey { get; set; }  
        public bool IsCancelled { get; set; }
        public bool IsAuctionStarted { get; set; }
        public bool IsAuctionEnded { get; set; }
        public bool RequireBalanceCheck { get; set; }
        public decimal? FloorPrice { get; set; }
        public decimal? ReservePrice { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsVisibleBeforeStartDate { get; set; }
        public bool IsVisibleAfterEndDate { get; set; }
        public decimal? FinalPrice { get; set; }
        public string? WinningAddress { get; set; }
        public bool IsSaleTXSent { get; set; }
        public string? SaleTXHash { get; set; }
        public bool IsSaleComplete { get; set; }
        public int CollectionId { get; set; }
        public bool SaleHasFailed { get; set; }

        #region Get Listing Db
        public static LiteDB.ILiteCollection<Listing>? GetListingDb()
        {
            try
            {
                var listingDb = DbContext.DB_DST.GetCollection<Listing>(DbContext.RSRV_LISTING);
                return listingDb;
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError(ex.ToString(), "Listing.GetListingDb()");
                return null;
            }

        }

        #endregion

        #region Get All Listings
        public static IEnumerable<Listing>? GetAllListings()
        {
            var listingDb = GetListingDb();

            if (listingDb != null)
            {
                var listings = listingDb.Query().Where(x => true).ToEnumerable();
                if (listings.Count() == 0)
                {
                    return null;
                }

                return listings;
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region Get All Started Listings
        public static IEnumerable<Listing>? GetAllStartedListings()
        {
            var listingDb = GetListingDb();

            if (listingDb != null)
            {
                var listings = listingDb.Query().Where(x => x.IsAuctionStarted && !x.IsSaleComplete && !x.SaleHasFailed && !x.IsSaleTXSent).ToEnumerable();
                if (listings.Count() == 0)
                {
                    return null;
                }

                return listings;
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region Get All Started Listings Count
        public static int GetLiveListingsCount()
        {
            var listingDb = GetListingDb();

            if (listingDb != null)
            {
                var liveCollections = Collection.GetLiveCollectionsIds();
                if(liveCollections != null)
                {
                    var listings = listingDb.Query().Where(x => x.IsAuctionStarted && !x.IsAuctionEnded && !x.IsCancelled && liveCollections.Contains(x.CollectionId)).ToEnumerable().Count();
                    if (listings == 0)
                    {
                        return 0;
                    }

                    return listings;
                }
                else
                {
                    return 0;
                }
            }
            else
            {
                return 0;
            }
        }

        #endregion

        #region Get Single Listing
        public static Listing? GetSingleListing(int listingId)
        {
            var listingDb = GetListingDb();

            if (listingDb != null)
            {
                var listing = listingDb.Query().Where(x => x.Id == listingId).FirstOrDefault();
                if (listing == null)
                {
                    return null;
                }

                return listing;
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region Get All Collection Listing
        public static IEnumerable<Listing>? GetCollectionListings(int collectionId)
        {
            var listingDb = GetListingDb();

            if (listingDb != null)
            {
                var listings = listingDb.Query().Where(x => x.CollectionId == collectionId).ToEnumerable();
                if (listings.Count() == 0)
                {
                    return null;
                }

                return listings;
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region Save Listing
        public static async Task<(bool, string)> SaveListing(Listing listing)
        {
            var singleListing = GetSingleListing(listing.Id);
            var listingDb = GetListingDb();
            if (singleListing == null)
            {
                if (listingDb != null)
                {
                    listing.PurchaseKey = RandomStringUtility.GetRandomStringOnlyLetters(10, true);
                    listingDb.InsertSafe(listing);
                    _ = NFTAssetFileUtility.GenerateThumbnails(listing.SmartContractUID);
                    return (true, "Listing saved.");
                }
            }
            else
            {
                if (listingDb != null)
                {
                    listingDb.UpdateSafe(listing);
                    return (true, "Listing updated.");
                }
            }
            return (false, "Listing DB was null.");
        }

        #endregion

        #region Delete Listing
        public static async Task<(bool, string)> DeleteListing(int listingId)
        {
            var singleListing = GetSingleListing(listingId);
            if (singleListing != null)
            {
                var listingDb = GetListingDb();
                if (listingDb != null)
                {
                    listingDb.DeleteSafe(listingId);
                    return (true, "Listing deleted.");
                }
                else
                {
                    return (false, "Listing DB was null.");
                }
            }
            return (false, "Listing was not present.");

        }

        #endregion

        #region Delete All Listing
        public static async Task<(bool, string)> DeleteAllListingsByCollection(int collectionId)
        {
            try
            {
                var listingDb = GetListingDb();
                if (listingDb != null)
                {
                    listingDb.DeleteManySafe(x => x.CollectionId == collectionId);
                    return (true, "Listing deleted.");
                }
                else
                {
                    return (false, "Listing DB was null.");
                }
            }
            catch(Exception ex)
            {
                return (false, $"Failed to delete. Error: {ex.ToString()}");
            }
            
        }

        #endregion

    }
}
