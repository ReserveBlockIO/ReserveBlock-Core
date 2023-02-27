using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ReserveBlockCore.Models.DST;

namespace ReserveBlockCore.Controllers
{
    [ActionFilterController]
    [Route("dstapi/[controller]")]
    [Route("dstapi/[controller]/{somePassword?}")]
    [ApiController]
    public class DSTV1Controller : ControllerBase
    {
        /// <summary>
        /// Check Status of API
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "RBX-Wallet DST", "DST API" };
        }

        /// <summary>
        /// Saves or Updates store data. For Id please put 0 for new inserts
        /// </summary>
        /// <param name="jsonData"></param>
        /// <returns></returns>
        [HttpPost("SaveStore")]
        public async Task<string> SaveStore([FromBody] object jsonData)
        {
            var output = "";
            try
            {
                if (jsonData != null)
                {
                    var store = JsonConvert.DeserializeObject<Store>(jsonData.ToString());
                    if(store != null)
                    {
                        var result = await Store.SaveStore(store);
                        if(result.Item1)
                        {
                            output = JsonConvert.SerializeObject(new { Success = true, Message = result.Item2 });
                            return output;
                        }
                        else
                        {
                            output = JsonConvert.SerializeObject(new { Success = false, Message = result.Item2 });
                            return output;
                        }
                    }
                    else
                    {
                        output = JsonConvert.SerializeObject(new { Success = false, Message = "Failed to deserialize JSON Payload." });
                        return output;
                    }
                }
            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = ex.ToString() });
            }

            return output;
        }

        /// <summary>
        /// Gets store information
        /// </summary>
        /// <param name="storeId"></param>
        /// <returns></returns>
        [HttpGet("GetStore/{storeId}")]
        public async Task<string> GetStore(int storeId)
        {
            var output = "";
            try
            {
                if (storeId != 0)
                {
                    var store = Store.GetSingleStore(storeId);
                    if(store != null)
                    {
                        output = JsonConvert.SerializeObject(new { Success = true, Message = "Store Found", Store = store });
                        return output;
                    }
                    else
                    {
                        output = JsonConvert.SerializeObject(new { Success = false, Message = "Store was not found." });
                        return output;
                    }
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Success = false, Message = "Store Id cannot be null or 0" });
                    return output;
                }

            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = ex.ToString() });
            }

            return output;
        }

        /// <summary>
        /// Gets All store information
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetAllStores")]
        public async Task<string> GetAllStores()
        {
            var output = "";
            try
            {
                var stores = Store.GetAllStores();
                if (stores != null)
                {
                    output = JsonConvert.SerializeObject(new { Success = true, Message = "Stores Found", Stores = stores });
                    return output;
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Success = false, Message = "Store was not found." });
                    return output;
                }
            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = ex.ToString() });
            }

            return output;
        }

        /// <summary>
        /// Gets the default stores information
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetDefaultStore")]
        public async Task<string> GetDefaultStore()
        {
            var output = "";
            try
            {
                var store = Store.GetDefaultStore();
                if (store != null)
                {
                    output = JsonConvert.SerializeObject(new { Success = true, Message = "Default Store Found", Store = store });
                    return output;
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Success = false, Message = "Default Store was not found." });
                    return output;
                }
            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = ex.ToString() });
            }

            return output;
        }

        /// <summary>
        /// Updates default store
        /// </summary>
        /// <param name="storeId"></param>
        /// <returns></returns>
        [HttpGet("GetDefaultStoreChange/{storeId}")]
        public async Task<string> GetDefaultStoreChange(int storeId)
        {
            var output = "";
            try
            {
                if (storeId != 0)
                {
                    var result = Store.ChangeDefaultStore(storeId);
                    if (result.Item1)
                    {
                        output = JsonConvert.SerializeObject(new { Success = true, Message = result.Item2});
                        return output;
                    }
                    else
                    {
                        output = JsonConvert.SerializeObject(new { Success = false, Message = result.Item2 });
                        return output;
                    }
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Success = false, Message = "Store Id cannot be null or 0" });
                    return output;
                }

            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = ex.ToString() });
            }

            return output;
        }

        /// <summary>
        /// Deletes a Store and all associated records with it
        /// </summary>
        /// <param name="storeId"></param>
        /// <returns></returns>
        [HttpGet("DeleteStore/{storeId}")]
        public async Task<string> DeleteStore(int storeId)
        {
            var output = "";
            try
            {
                if (storeId != 0)
                {
                    var listings = Listing.GetStoreListings(storeId);
                    var result = await Store.DeleteStore(storeId);
                    if (result.Item1)
                    {
                        if(listings?.Count() > 0)
                        {

                            var listingDeleteResult = await Listing.DeleteAllListingsByStore(storeId);
                            var auctionsDeleteResult = await Auction.DeleteAllAuctionsByStore(storeId);
                            var bidDeleteResult = await Bid.DeleteAllBidsByStore(storeId);
                        }
                        
                        output = JsonConvert.SerializeObject(new { Success = true, Message = result.Item2 });

                        return output;
                    }
                    else
                    {
                        output = JsonConvert.SerializeObject(new { Success = false, Message = result.Item2 });
                        return output;
                    }
                    
                    
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Success = false, Message = "Store Id cannot be null or 0" });
                    return output;
                }

            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = ex.ToString() });
            }

            return output;
        }

        /// <summary>
        /// Creates or Updates a listing for an NFT Item
        /// </summary>
        /// <param name="jsonData"></param>
        /// <returns></returns>
        [HttpPost("SaveListing")]
        public async Task<string> SaveListing([FromBody] object jsonData)
        {
            var output = "";
            try
            {
                if (jsonData != null)
                {
                    var listing = JsonConvert.DeserializeObject<Listing>(jsonData.ToString());
                    if (listing != null)
                    {
                        var result = await Listing.SaveListing(listing);
                        if (result.Item1)
                        {
                            output = JsonConvert.SerializeObject(new { Success = true, Message = result.Item2 });
                            return output;
                        }
                        else
                        {
                            output = JsonConvert.SerializeObject(new { Success = false, Message = result.Item2 });
                            return output;
                        }
                    }
                    else
                    {
                        output = JsonConvert.SerializeObject(new { Success = false, Message = "Failed to deserialize JSON Payload." });
                        return output;
                    }
                }
            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = ex.ToString() });
            }

            return output;
        }

        /// <summary>
        /// Gets store listings
        /// </summary>
        /// <param name="storeId"></param>
        /// <returns></returns>
        [HttpGet("GetStoreListings/{storeId}")]
        public async Task<string> GetStoreListings(int storeId)
        {
            var output = "";
            try
            {
                if (storeId != 0)
                {
                    var listings = Listing.GetStoreListings(storeId);
                    if (listings != null)
                    {
                        output = JsonConvert.SerializeObject(new { Success = true, Message = "Listings Found", Listings = listings });
                        return output;
                    }
                    else
                    {
                        output = JsonConvert.SerializeObject(new { Success = false, Message = "Listings were not found." });
                        return output;
                    }
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Success = false, Message = "Store Id cannot be null or 0" });
                    return output;
                }

            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = ex.ToString() });
            }

            return output;
        }

        /// <summary>
        /// Gets Listing Information
        /// </summary>
        /// <param name="listingId"></param>
        /// <returns></returns>
        [HttpGet("GetListing/{listingId}")]
        public async Task<string> GetListing(int listingId)
        {
            var output = "";
            try
            {
                if (listingId != 0)
                {
                    var listing = Listing.GetSingleListing(listingId);
                    if (listing != null)
                    {
                        var auction = Auction.GetSingleAuction(listingId);
                        var bids = Bid.GetListingBids(listingId);

                        output = JsonConvert.SerializeObject(new { Success = true, Message = "Listing Found", Listing = listing, Auction = auction, Bids = bids });
                        return output;
                    }
                    else
                    {
                        output = JsonConvert.SerializeObject(new { Success = false, Message = "Listing were not found." });
                        return output;
                    }
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Success = false, Message = "Store Id cannot be null or 0" });
                    return output;
                }

            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = ex.ToString() });
            }

            return output;
        }

        /// <summary>
        /// Deletes a listing and all associated records with it
        /// </summary>
        /// <param name="listingId"></param>
        /// <returns></returns>
        [HttpGet("CancelListing/{listingId}")]
        public async Task<string> CancelListing(int listingId)
        {
            var output = "";
            try
            {
                if (listingId != 0)
                {
                    var listing = Listing.GetSingleListing(listingId);
                    if (listing != null)
                    {
                        listing.IsCancelled = true;
                        var result = await Listing.SaveListing(listing);
                        if(result.Item1)
                        {
                            output = JsonConvert.SerializeObject(new { Success = true, Message = result.Item2 });
                            return output;
                        }
                        else
                        {
                            output = JsonConvert.SerializeObject(new { Success = false, Message = result.Item2 });
                            return output;
                        }
                    }
                    else
                    {
                        output = JsonConvert.SerializeObject(new { Success = false, Message = "No Listing Found." });
                        return output;
                    }


                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Success = false, Message = "Listing Id cannot be null or 0" });
                    return output;
                }

            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = ex.ToString() });
            }

            return output;
        }

        /// <summary>
        /// Deletes a listing and all associated records with it
        /// </summary>
        /// <param name="listingId"></param>
        /// <returns></returns>
        [HttpGet("DeleteListing/{listingId}")]
        public async Task<string> DeleteListing(int listingId)
        {
            var output = "";
            try
            {
                if (listingId != 0)
                {
                    var result = await Listing.DeleteListing(listingId);
                    if (result.Item1)
                    {
                        var auctionsDeleteResult = await Auction.DeleteAllAuctionsByListing(listingId);
                        var bidDeleteResult = await Bid.DeleteAllBidsByListing(listingId);

                        output = JsonConvert.SerializeObject(new { Success = true, Message = result.Item2 });

                        return output;
                    }
                    else
                    {
                        output = JsonConvert.SerializeObject(new { Success = false, Message = result.Item2 });
                        return output;
                    }


                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Success = false, Message = "Listing Id cannot be null or 0" });
                    return output;
                }

            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = ex.ToString() });
            }

            return output;
        }
    }
}
