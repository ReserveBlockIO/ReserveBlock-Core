using LiteDB;
using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.Utilities;
using System.Drawing;

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
        public bool IsCancelled { get; set; }
        public bool RequireBalanceCheck { get; set; }
        public decimal? FloorPrice { get; set; }
        public decimal? ReservePrice { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsVisibleBeforeStartDate { get; set; }
        public bool IsVisibleAfterEndDate { get; set; }
        public decimal? FinalPrice { get; set; }
        public string? WinningAddress { get; set; }
        public int CollectionId { get; set; }

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
                    listingDb.InsertSafe(listing);
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

        #region Produce Thumbnails
        public static async Task GenerateThumbnails(string scUID)
        {
            List<string> ImageExtensionList = new List<string> { 
                "jpg", "png", "gif", "webp", "tiff", "psd", "raw", "bmp", "heif", "indd", "jpeg2000", "svg", "eps", "ai"
            };

            //need util that gets all assets for an NFT.
            //create a folder inside NFT folder called Thumbs that will contain all thumbnails.
            //below code works. Just need pathing modifications.

            //Image image = Image.FromFile(fileName);
            //Image thumb = image.GetThumbnailImage(256, 256, () => false, IntPtr.Zero);
            //thumb.Save(Path.ChangeExtension(fileName, "thumb"));
        }
        #endregion

    }
}
