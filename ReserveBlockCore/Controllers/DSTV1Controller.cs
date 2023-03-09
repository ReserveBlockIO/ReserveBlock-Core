using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ReserveBlockCore.Models;
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
        /// Saves or Updates Collection data. For Id please put 0 for new inserts
        /// </summary>
        /// <param name="jsonData"></param>
        /// <returns></returns>
        [HttpPost("SaveCollection")]
        public async Task<string> SaveCollection([FromBody] object jsonData)
        {
            var output = "";
            try
            {
                if (jsonData != null)
                {
                    var collection = JsonConvert.DeserializeObject<Collection>(jsonData.ToString());
                    if(collection != null)
                    {
                        var result = await Collection.SaveCollection(collection);
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
        /// Gets collection information
        /// </summary>
        /// <param name="collectionId"></param>
        /// <returns></returns>
        [HttpGet("GetCollection/{collectionId}")]
        public async Task<string> GetCollection(int collectionId)
        {
            var output = "";
            try
            {
                if (collectionId != 0)
                {
                    var collection = Collection.GetSingleCollection(collectionId);
                    if(collection != null)
                    {
                        output = JsonConvert.SerializeObject(new { Success = true, Message = "Collection Found", Collection = collection });
                        return output;
                    }
                    else
                    {
                        output = JsonConvert.SerializeObject(new { Success = false, Message = "Collection was not found." });
                        return output;
                    }
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Success = false, Message = "Collection Id cannot be null or 0" });
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
        /// Gets All Collection information
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetAllCollections")]
        public async Task<string> GetAllCollections()
        {
            var output = "";
            try
            {
                var collections = Collection.GetAllCollections();
                if (collections != null)
                {
                    output = JsonConvert.SerializeObject(new { Success = true, Message = "Collections Found", Collections = collections });
                    return output;
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Success = false, Message = "Collection was not found." });
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
        /// Gets the default Collection information
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetDefaultCollection")]
        public async Task<string> GetDefaultCollection()
        {
            var output = "";
            try
            {
                var collection = Collection.GetDefaultCollection();
                if (collection != null)
                {
                    output = JsonConvert.SerializeObject(new { Success = true, Message = "Default Collection Found", Collection = collection });
                    return output;
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Success = false, Message = "Default Collection was not found." });
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
        /// Updates default collection
        /// </summary>
        /// <param name="collectionId"></param>
        /// <returns></returns>
        [HttpGet("GetDefaultCollectionChange/{collectionId}")]
        public async Task<string> GetDefaultCollectionChange(int collectionId)
        {
            var output = "";
            try
            {
                if (collectionId != 0)
                {
                    var result = Collection.ChangeDefaultCollection(collectionId);
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
                    output = JsonConvert.SerializeObject(new { Success = false, Message = "Collection Id cannot be null or 0" });
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
        /// Deletes a Collection and all associated records with it
        /// </summary>
        /// <param name="collectionId"></param>
        /// <returns></returns>
        [HttpGet("DeleteCollection/{collectionId}")]
        public async Task<string> DeleteCollection(int collectionId)
        {
            var output = "";
            try
            {
                if (collectionId != 0)
                {
                    var listings = Listing.GetCollectionListings(collectionId);
                    var result = await Collection.DeleteCollection(collectionId);
                    if (result.Item1)
                    {
                        if(listings?.Count() > 0)
                        {

                            var listingDeleteResult = await Listing.DeleteAllListingsByCollection(collectionId);
                            var auctionsDeleteResult = await Auction.DeleteAllAuctionsByCollection(collectionId);
                            var bidDeleteResult = await Bid.DeleteAllBidsByCollection(collectionId);
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
                    output = JsonConvert.SerializeObject(new { Success = false, Message = "Collection Id cannot be null or 0" });
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
        /// Gets Collection listings
        /// </summary>
        /// <param name="collectionId"></param>
        /// <returns></returns>
        [HttpGet("GetCollectionListings/{collectionId}")]
        public async Task<string> GetCollectionListings(int collectionId)
        {
            var output = "";
            try
            {
                if (collectionId != 0)
                {
                    var listings = Listing.GetCollectionListings(collectionId);
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
                    output = JsonConvert.SerializeObject(new { Success = false, Message = "Collection Id cannot be null or 0" });
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
                    output = JsonConvert.SerializeObject(new { Success = false, Message = "Collection Id cannot be null or 0" });
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

        /// <summary>
        /// Gets dec shop info from Network. Example : 'rbx://someurlgoeshere'
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetDecShopByURL/{**url}")]
        public async Task<string> GetDecShopByURL(string url)
        {
            string output = "";

            var decshop = await DecShop.GetDecShopStateTreiLeafByURL(url);

            if (decshop != null)
            {
                output = JsonConvert.SerializeObject(new { Success = true, Message = "DecShop Found", DecShop = decshop });
                return output;
            }

            output = JsonConvert.SerializeObject(new { Success = false, Message = "No DecShop Found." });
            return output;
        }

        /// <summary>
        /// Publishes local Dec Shop
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetDecShop")]
        public async Task<string> GetDecShop()
        {
            string output = "";

            var decshop = DecShop.GetMyDecShopInfo();

            if (decshop != null)
            {
                output = JsonConvert.SerializeObject(new { Success = true, Message = "DecShop Found", DecShop = decshop });
                return output;
            }

            output = JsonConvert.SerializeObject(new { Success = false, Message = "No DecShop Found." });
            return output;
        }

        /// <summary>
        /// Sets the shop status for being off and online
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetSetShopStatus")]
        public async Task<string> GetSetShopStatus()
        {
            string output = "";

            var decshop = DecShop.GetMyDecShopInfo();

            if (decshop != null)
            {
                var result = DecShop.SetDecShopStatus();
                if(result != null)
                {
                    output = JsonConvert.SerializeObject(new { Success = true, Message = $"Is Offline? {result}" });
                    return output;
                }
            }

            output = JsonConvert.SerializeObject(new { Success = false, Message = "No DecShop Found." });
            return output;
        }

        /// <summary>
        /// Saves a shop to allow for the sale of items in collections
        /// </summary>
        /// <param name="jsonData"></param>
        /// <returns></returns>
        [HttpPost("SaveDecShop")]
        public async Task<string> SaveDecShop([FromBody] object jsonData)
        {
            var output = "";

            try
            {
                var myDS = DecShop.GetMyDecShopInfo();
                if (jsonData != null)
                {
                    var decShop = JsonConvert.DeserializeObject<DecShop>(jsonData.ToString());

                    if (myDS == null)
                    {
                        if (decShop != null)
                        {
                            var wordCount = decShop.Description.ToWordCountCheck(200);
                            var descLength = decShop.Description.ToLengthCheck(1200);

                            if (!wordCount || !descLength)
                            {
                                output = JsonConvert.SerializeObject(new { Success = false, Message = $"Description Word Count Allowed: {200}. Description length allowed: {1200}" });
                                return output;
                            }

                            var urlCheck = DecShop.ValidStateTreiURL(decShop.DecShopURL);

                            if (!urlCheck)
                            {
                                output = JsonConvert.SerializeObject(new { Success = false, Message = $"URL: {decShop.DecShopURL} has already been taken." });
                                return output;
                            }

                            var buildResult = decShop.Build();

                            if (!buildResult.Item1)
                            {
                                output = JsonConvert.SerializeObject(new { Success = buildResult.Item1, Message = buildResult.Item2 });
                                return output;
                            }

                            var result = await DecShop.SaveMyDecShopLocal(decShop);
                            output = JsonConvert.SerializeObject(new { Success = result.Item1, Message = result.Item2 });
                            return output;
                        }
                        else
                        {

                        }
                    }
                    else
                    {
                        if (decShop != null)
                        {
                            myDS.Name = decShop.Name;
                            myDS.Description = decShop.Description;
                            myDS.DecShopURL = $"rbx://{decShop.DecShopURL}";
                            myDS.IsOffline = decShop.IsOffline;
                            myDS.AutoUpdateNetworkDNS = decShop.AutoUpdateNetworkDNS;

                            if (decShop.HostingType == DecShopHostingType.SelfHosted)
                            {
                                myDS.IP = decShop.IP;
                                myDS.Port = decShop.Port;
                            }

                            var result = await DecShop.SaveMyDecShopLocal(myDS);
                            output = JsonConvert.SerializeObject(new { Success = result.Item1, Message = result.Item2 });
                            return output;
                        }
                        else
                        {
                            output = JsonConvert.SerializeObject(new { Success = false, Message = "Was not able to deserialize json payload." });
                        }
                    }
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Success = false, Message = "JSON payload was null." });
                }
            }
            catch(Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error. Error: {ex.ToString()}" });
            }
            
            return output;
        }

        /// <summary>
        /// Publishes Dec Shop to network
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetPublishDecShop")]
        public async Task<string> GetPublishDecShop()
        {
            string output = "";

            try
            {
                var localShop = DecShop.GetMyDecShopInfo();
                if(localShop != null)
                {
                    if(localShop.IsPublished)
                    {
                        output = JsonConvert.SerializeObject(new { Success = false, Message = $"Shop has already been created. Please use /GetUpdateDecShop to update your shop." });
                        return output;
                    }
                    var txResult = await DecShop.CreateDecShopTx(localShop);
                    if(txResult.Item1 != null)
                    {
                        output = JsonConvert.SerializeObject(new { Success = true, Message = $"Success! TX ID: {txResult.Item1.Hash}", Hash = txResult.Item1.Hash });
                        return output;
                    }
                    else
                    {
                        output = JsonConvert.SerializeObject(new { Success = false, Message = $"{txResult.Item2}" });
                    }
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Success = false, Message = "A local decshop does not exist." });
                }
            }
            catch { }

            return output;
        }

        /// <summary>
        /// Updates Dec Shop on network
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetUpdateDecShop")]
        public async Task<string> GetUpdateDecShop()
        {
            string output = "";

            try
            {
                var localShop = DecShop.GetMyDecShopInfo();
                if (localShop != null)
                {
                    if(localShop.NeedsPublishToNetwork)
                    {
                        var txResult = await DecShop.UpdateDecShopTx(localShop);
                        if (txResult.Item1 != null)
                        {
                            output = JsonConvert.SerializeObject(new { Success = true, Message = $"Success! TX ID: {txResult.Item2}" });
                        }
                    }
                    else
                    {
                        output = JsonConvert.SerializeObject(new { Success = false, Message = "No update is pending." });
                    }
                    
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Success = false, Message = "A local decshop does not exist." });
                }
            }
            catch { }

            return output;
        }

        /// <summary>
        /// Deletes Dec Shop on network
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetDeleteDecShop")]
        public async Task<string> GetDeleteDecShop()
        {
            string output = "";

            //var decshopdb = DecShop.DecShopLocalDB();
            //var mydec = DecShop.GetMyDecShopInfo();
            //if(decshopdb != null)
            //{
            //    if(mydec != null)
            //    {
            //        decshopdb.DeleteSafe(mydec.Id);
            //    }
            //}

            try
            {
                var localShop = DecShop.GetMyDecShopInfo();
                if (localShop != null)
                {
                    var txResult = await DecShop.DeleteDecShopTx(localShop.UniqueId, localShop.OwnerAddress);
                    if (txResult.Item1 != null)
                    {
                        output = JsonConvert.SerializeObject(new { Success = true, Message = $"Success! TX ID: {txResult.Item2}" });
                    }
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Success = false, Message = "A local decshop does not exist." });
                }
            }
            catch { }

            return output;
        }

        /// <summary>
        /// Gets dec shop from network
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        [HttpGet("GetImportDecShopFromNetwork/{address}")]
        public async Task<string> GetImportDecShopFromNetwork(string address)
        {
            var output = "";
            var dcStateTreiDb = DecShop.DecShopTreiDb();
            var leaf = dcStateTreiDb.Query().Where(x => x.OwnerAddress == address).FirstOrDefault();
            if(leaf != null)
            {
                var decShopExist = DecShop.GetMyDecShopInfo();
                if(decShopExist == null)
                {
                    leaf.Id = 0;
                    var result = await DecShop.SaveMyDecShopLocal(leaf, false, true);
                    output = JsonConvert.SerializeObject(new { Success = result.Item1, Message = result.Item2 });
                    return output;
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Success = false, Message = "Wallet already has a dec shop associated to it." });
                    return output;
                }
            }
            output = JsonConvert.SerializeObject(new { Success = false, Message = $"Could not find the DecShop leaf for address: {address}." });
            return output;
        }
    }
}
