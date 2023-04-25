using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.DST;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.DST;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Net;

namespace ReserveBlockCore.Controllers
{
    [ActionFilterController]
    [Route("dstapi/[controller]")]
    [Route("dstapi/[controller]/{somePassword?}")]
    [ApiController]
    public class DSTV1Controller : ControllerBase
    {
        private static string? ConnectingAddress = null;

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
        /// Gets NFT assets for caching
        /// </summary>
        /// <param name="scUID"></param>
        /// <returns></returns>
        [HttpGet("GetNFTAssets/{scUID}")]
        public async Task<bool> GetNFTAssets(string scUID)
        {
            NFTLogUtility.Log($"Asset Download Started for: {scUID}", "DSTV1Controller.GetNFTAssets()");
            var connectedShop = Globals.ConnectedClients.Where(x => x.Value.IsConnected).Take(1);
            if (connectedShop.Count() > 0)
            {
                NFTLogUtility.Log($"Connected to shop, attempting download  for: {scUID}", "DSTV1Controller.GetNFTAssets()");
                Message message = new Message
                {
                    Address = ConnectingAddress,
                    Data = scUID,
                    Type = MessageType.AssetReq,
                    ComType = MessageComType.Info
                };

                if (!Globals.AssetDownloadLock)
                {
                    Globals.AssetDownloadLock = true;
                    NFTLogUtility.Log($"Asset download unlocked for: {scUID}", "DSTV1Controller.GetNFTAssets()");
                    await DSTClient.DisconnectFromAsset();
                    var connected = await DSTClient.ConnectToShopForAssets();
                    if (connected)
                        _ = DSTClient.GetListingAssetThumbnails(message, scUID);
                    else
                        return false;

                    return true;
                }
                else
                {
                    NFTLogUtility.Log($"Asset download locked for: {scUID}", "DSTV1Controller.GetNFTAssets()");
                }
            }

            return false;
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
                            myDS.IsOffline = decShop.IsOffline;
                            myDS.AutoUpdateNetworkDNS = decShop.AutoUpdateNetworkDNS;

                            if(myDS.DecShopURL != decShop.DecShopURL)
                            {
                                myDS.DecShopURL = $"rbx://{decShop.DecShopURL}";
                            }

                            if (decShop.HostingType == DecShopHostingType.SelfHosted)
                            {
                                myDS.IP = decShop.IP;
                                myDS.Port = decShop.Port;
                                myDS.HostingType = DecShopHostingType.SelfHosted;
                            }

                            if(myDS.IsIPDifferent && myDS.HostingType == DecShopHostingType.Network)
                            {
                                myDS.IP = P2PClient.MostLikelyIP();
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
                            output = JsonConvert.SerializeObject(new { Success = true, Message = $"Success! TX ID: {txResult.Item1.Hash}" });
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

            try
            {
                var localShop = DecShop.GetMyDecShopInfo();
                if (localShop != null)
                {
                    var txResult = await DecShop.DeleteDecShopTx(localShop.UniqueId, localShop.OwnerAddress);
                    if (txResult.Item1 != null)
                    {
                        output = JsonConvert.SerializeObject(new { Success = true, Message = $"Success! TX ID: {txResult.Item1.Hash}" });
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
        [HttpGet("GetDeleteLocalDecShop")]
        public async Task<string> GetDeleteLocalDecShop()
        {
            string output = "";

            try
            {
                var localShop = DecShop.GetMyDecShopInfo();
                if (localShop != null)
                {
                    var decDb = DecShop.DecShopLocalDB();
                    if(decDb != null)
                    {
                        var result = decDb.DeleteSafe(localShop.Id);
                        output = JsonConvert.SerializeObject(new { Success = true, Message = $"Local Dec Shop Deleted : {result}" });
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

        /// <summary>
        /// Get network shop info rbx://someurlgoeshere'
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        [HttpGet("GetNetworkDecShopInfo/{**url}")]
        public async Task<string> GetNetworkDecShopInfo(string url)
        {
            var decshop = await DecShop.GetDecShopStateTreiLeafByURL(url);

            if(decshop == null)
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Could not find the DecShop leaf for url: {url}." });

            return JsonConvert.SerializeObject(new { Success = true, Message = $"Shop Found", DecShop = decshop });

        }

        /// <summary>
        /// Get network dec shop list
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetDecShopStateTreiList")]
        public async Task<string> GetDecShopStateTreiList(string url)
        {
            var decshops = await DecShop.GetDecShopStateTreiList();

            if(decshops?.Count() == 0)
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Could not find any DecShops." });

            return JsonConvert.SerializeObject(new { Success = true, Message = $"Shops Found", DecShops = decshops }, Formatting.Indented);
        }


        /// <summary>
        /// Connects to a shop : 'rbx://someurlgoeshere'
        /// </summary>
        /// <param name="address"></param>
        /// <param name="url"></param>
        /// <returns></returns>
        [HttpGet("ConnectToDecShop/{address}/{**url}")]
        public async Task<bool> ConnectToDecShop(string address, string url)
        {
            var decshop = await DecShop.GetDecShopStateTreiLeafByURL(url);

            if (decshop != null)
            {
                ConnectingAddress = address;
                var accountExist = AccountData.GetSingleAccount(address);
                if(!Debugger.IsAttached)
                {
                    if (accountExist == null)
                        return false;
                }
                //removes current connection to shop
                await DSTClient.DisconnectFromShop();
                var connectionResult = await DSTClient.ConnectToShop(url, address);

                //if connectionResult == true create some looping event.

                if (connectionResult)
                    _ = DSTClient.GetShopData(ConnectingAddress);

                return connectionResult;
            }

            return false;
        }

        /// <summary>
        /// Gets shop info : 'rbx://someurlgoeshere'
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetShopInfo")]
        public async Task<bool> GetShopInfo()
        {
            var connectedShop = Globals.ConnectedClients.Where(x => x.Value.IsConnected).Take(1);
            if(connectedShop.Count() > 0)
            {
                Message message = new Message
                {
                    Address = ConnectingAddress,
                    Data = $"{DecShopRequestOptions.Info}",
                    Type = MessageType.DecShop,
                    ComType = MessageComType.Request
                };

                _ = DSTClient.SendShopMessageFromClient(message, true);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets shop collections
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetShopCollections")]
        public async Task<bool> GetShopCollections()
        {
            var connectedShop = Globals.ConnectedClients.Where(x => x.Value.IsConnected).Take(1);
            if (connectedShop.Count() > 0)
            {
                Message message = new Message
                {
                    Address = ConnectingAddress,
                    Data = $"{DecShopRequestOptions.Collections}",
                    Type = MessageType.DecShop,
                    ComType = MessageComType.Request
                };

                _ = DSTClient.SendShopMessageFromClient(message, true);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets shop Listings
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        [HttpGet("GetShopListings/{page}")]
        public async Task<bool> GetShopListings(int page)
        {
            var connectedShop = Globals.ConnectedClients.Where(x => x.Value.IsConnected).Take(1);
            if (connectedShop.Count() > 0)
            {
                Message message = new Message
                {
                    Address = ConnectingAddress,
                    Data = $"{DecShopRequestOptions.Listings},{page}",
                    Type = MessageType.DecShop,
                    ComType = MessageComType.Request
                };

                _ = DSTClient.SendShopMessageFromClient(message, true);

                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets shop Auctions
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        [HttpGet("GetShopAuctions/{page}")]
        public async Task<bool> GetShopAuctions(int page)
        {
            var connectedShop = Globals.ConnectedClients.Where(x => x.Value.IsConnected).Take(1);
            if (connectedShop.Count() > 0)
            {
                Message message = new Message
                {
                    Address = ConnectingAddress,
                    Data = $"{DecShopRequestOptions.Auctions},{page}",
                    Type = MessageType.DecShop,
                    ComType = MessageComType.Request
                };

                _ = DSTClient.SendShopMessageFromClient(message, true);

                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets shop Listings by collection
        /// </summary>
        /// <param name="collectionId"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        [HttpGet("GetShopListingsByCollection/{collectionId}/{page}")]
        public async Task<bool> GetShopListingsByCollection(int collectionId, int page)
        {
            var connectedShop = Globals.ConnectedClients.Where(x => x.Value.IsConnected).Take(1);
            if (connectedShop.Count() > 0)
            {
                Message message = new Message
                {
                    Address = ConnectingAddress,
                    Data = $"{DecShopRequestOptions.ListingsByCollection},{collectionId},{page}",
                    Type = MessageType.DecShop,
                    ComType = MessageComType.Request
                };

                _ = DSTClient.SendShopMessageFromClient(message, true);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets shop Listings by collection
        /// </summary>
        /// <param name="scUID"></param>
        /// <returns></returns>
        [HttpGet("GetShopSpecificListing/{scUID}")]
        public async Task<bool> GetShopSpecificListing(string scUID)
        {
            var connectedShop = Globals.ConnectedClients.Where(x => x.Value.IsConnected).Take(1);
            if (connectedShop.Count() > 0)
            {
                Message message = new Message
                {
                    Address = ConnectingAddress,
                    Data = $"{DecShopRequestOptions.SpecificListing},{scUID}",
                    Type = MessageType.DecShop,
                    ComType = MessageComType.Request
                };

                _ = DSTClient.SendShopMessageFromClient(message, true);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets shop Listings by collection
        /// </summary>
        /// <param name="listingId"></param>
        /// <returns></returns>
        [HttpGet("GetShopSpecificAuction/{listingId}")]
        public async Task<bool> GetShopSpecificAuction(string listingId)
        {
            var connectedShop = Globals.ConnectedClients.Where(x => x.Value.IsConnected).Take(1);
            if (connectedShop.Count() > 0)
            {
                Message message = new Message
                {
                    Address = ConnectingAddress,
                    Data = $"{DecShopRequestOptions.SpecificAuction},{listingId}",
                    Type = MessageType.DecShop,
                    ComType = MessageComType.Request
                };

                _ = DSTClient.SendShopMessageFromClient(message, true);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Send a bid to a listing
        /// </summary>
        /// <returns></returns>
        [HttpPost("SendBid")]
        public async Task<string> SendBid([FromBody] object jsonData)
        {
            try
            {
                if (jsonData != null)
                {
                    var bidPayload = JsonConvert.DeserializeObject<Bid>(jsonData.ToString());
                    if (bidPayload == null)
                        return JsonConvert.SerializeObject(new { Success = false, Message = "Bid Payload cannot be null" });

                    var localAddress = AccountData.GetSingleAccount(bidPayload.BidAddress);

                    if (localAddress == null)
                        return JsonConvert.SerializeObject(new { Success = false, Message = "You must own the bid address address" });

                    if(bidPayload.BidAddress.StartsWith("xRBX"))
                        return JsonConvert.SerializeObject(new { Success = false, Message = "You may not place bids with a Reserve Account" });

                    if (Globals.DecShopData?.DecShop == null)
                        return JsonConvert.SerializeObject(new { Success = false, Message = "DecShop Data cannot be null." });

                    var bidBuild = bidPayload.Build();
                    if(bidBuild == false)
                        return JsonConvert.SerializeObject(new { Success = false, Message = "Failed to build bid." });

                    var bidJson = JsonConvert.SerializeObject(bidPayload);

                    Message message = new Message
                    {
                        Address = ConnectingAddress,
                        Data = bidJson,
                        Type = MessageType.Bid,
                        ComType = MessageComType.Request
                    };

                    var bidSave = Bid.SaveBid(bidPayload);
                    
                    _ = DSTClient.SendShopMessageFromClient(message, false);

                    return JsonConvert.SerializeObject(new { Success = true, Message = "Bid sent.", BidId = bidPayload.Id });
                }

            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error: {ex.ToString()}" });
            }

            return JsonConvert.SerializeObject(new { Success = false, Message = "Wallet already has a dec shop associated to it." }); ;
        }

        /// <summary>
        /// Send a bid to a listing
        /// </summary>
        /// <param name="bidId"></param>
        /// <returns></returns>
        [HttpGet("ResendBid/{bidId}")]
        public async Task<string> ResendBid(Guid bidId)
        {
            try
            {
                var bid = Bid.GetSingleBid(bidId);
                if (bid != null)
                {
                    var bidJson = JsonConvert.SerializeObject(bid);

                    Message message = new Message
                    {
                        Address = ConnectingAddress,
                        Data = bidJson,
                        Type = MessageType.Bid,
                        ComType = MessageComType.Request
                    };

                    _ = DSTClient.SendShopMessageFromClient(message, false);

                    return JsonConvert.SerializeObject(new { Success = true, Message = "Bid Resent.", BidId = bid.Id });
                }
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error: {ex.ToString()}" });
            }

            return JsonConvert.SerializeObject(new { Success = false, Message = "Wallet already has a dec shop associated to it." }); ;
        }

        /// <summary>
        /// Send a bid to a listing
        /// </summary>
        /// <returns></returns>
        [HttpPost("SendBuyNowBid")]
        public async Task<string> SendBuyNowBid([FromBody] object jsonData)
        {
            try
            {
                if (jsonData != null)
                {
                    var bidPayload = JsonConvert.DeserializeObject<Bid>(jsonData.ToString());
                    if (bidPayload == null)
                        return JsonConvert.SerializeObject(new { Success = false, Message = "Bid Payload cannot be null" });

                    var localAddress = AccountData.GetSingleAccount(bidPayload.BidAddress);

                    if (localAddress == null)
                        return JsonConvert.SerializeObject(new { Success = false, Message = "You must own the bid address address" });

                    if (bidPayload.BidAddress.StartsWith("xRBX"))
                        return JsonConvert.SerializeObject(new { Success = false, Message = "You may not perform a 'Buy Now' with a Reserve Account" });

                    if (Globals.DecShopData?.DecShop == null)
                        return JsonConvert.SerializeObject(new { Success = false, Message = "DecShop Data cannot be null." });

                    var bidBuild = bidPayload.Build();

                    if(bidPayload.IsBuyNow != true)
                        return JsonConvert.SerializeObject(new { Success = false, Message = "IsBuyNow must be set to 'true'." });

                    if (bidBuild == false)
                        return JsonConvert.SerializeObject(new { Success = false, Message = "Failed to build bid." });

                    var bidJson = JsonConvert.SerializeObject(bidPayload);

                    Message message = new Message
                    {
                        Address = ConnectingAddress,
                        Data = bidJson,
                        Type = MessageType.Purchase,
                        ComType = MessageComType.Request
                    };

                    var bidSave = Bid.SaveBid(bidPayload);

                    _ = DSTClient.SendShopMessageFromClient(message, false);

                    return JsonConvert.SerializeObject(new { Success = true, Message = "Buy Now Bid sent.", BidId = bidPayload.Id });
                }

            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error: {ex.ToString()}" });
            }

            return JsonConvert.SerializeObject(new { Success = false, Message = "Wallet already has a dec shop associated to it." }); ;
        }

        /// <summary>
        /// Get bids
        /// </summary>
        /// <param name="sendReceive"></param>
        /// <returns></returns>
        [HttpGet("GetBids/{sendReceive}")]
        public async Task<string> GetBids(BidSendReceive sendReceive)
        {
            try
            {
                var bids = Bid.GetAllBids(sendReceive);
                
                if (bids?.Count() > 0)
                {
                    return JsonConvert.SerializeObject(new { Success = true, Message = "Bids Found.", Bids = bids });
                }
                else
                {
                    return JsonConvert.SerializeObject(new { Success = false, Message = "Bids not found." });
                }
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error: {ex.ToString()}" });
            }
        }

        /// <summary>
        /// Get bids for specific listing
        /// </summary>
        /// <param name="listingId"></param>
        /// <param name="sendReceive"></param>
        /// <returns></returns>
        [HttpGet("GetListingBids/{listingId}/{sendReceive}")]
        public async Task<string> GetListingBids(int listingId, BidSendReceive sendReceive)
        {
            try
            {
                var bids = Bid.GetListingBids(listingId, sendReceive);

                if (bids?.Count() > 0)
                {
                    return JsonConvert.SerializeObject(new { Success = true, Message = "Bids Found.", Bids = bids });
                }
                else
                {
                    return JsonConvert.SerializeObject(new { Success = false, Message = "Bids not found." });
                }
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error: {ex.ToString()}" });
            }
        }

        /// <summary>
        /// Get bids for specific status
        /// </summary>
        /// <param name="bidStatus"></param>
        /// <param name="sendReceive"></param>
        /// <returns></returns>
        [HttpGet("GetBidsByStatus/{bidStatus}/{sendReceive}")]
        public async Task<string> GetBidsByStatus(BidStatus bidStatus, BidSendReceive sendReceive)
        {
            try
            {
                var bids = Bid.GetBidByStatus(bidStatus);

                if (bids?.Count() > 0)
                {
                    return JsonConvert.SerializeObject(new { Success = true, Message = "Bids Found.", Bids = bids });
                }
                else
                {
                    return JsonConvert.SerializeObject(new { Success = false, Message = "Bids not found." });
                }
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error: {ex.ToString()}" });
            }
        }

        /// <summary>
        /// Get specific bid.
        /// </summary>
        /// <param name="bidId"></param>
        /// <returns></returns>
        [HttpGet("GetSingleBids/{bidId}")]
        public async Task<string> GetSingleBids(Guid bidId)
        {
            try
            {
                var bid = Bid.GetSingleBid(bidId);

                if (bid != null)
                {
                    return JsonConvert.SerializeObject(new { Success = true, Message = "Bid Found.", Bid = bid });
                }
                else
                {
                    return JsonConvert.SerializeObject(new { Success = false, Message = "Bid not found." });
                }
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error: {ex.ToString()}" });
            }
        }

        /// <summary>
        /// Get bids for specific listing from shop - NOT LOCAL
        /// </summary>
        /// <param name="listingId"></param>
        /// <returns></returns>
        [HttpGet("GetShopListingBids/{listingId}")]
        public async Task<bool> GetShopListingBids(int listingId)
        {
            var connectedShop = Globals.ConnectedClients.Where(x => x.Value.IsConnected).Take(1);
            if (connectedShop.Count() > 0)
            {
                Message message = new Message
                {
                    Address = ConnectingAddress,
                    Data = $"{DecShopRequestOptions.Bids},{listingId}",
                    Type = MessageType.DecShop,
                    ComType = MessageComType.Request
                };

                _ = DSTClient.SendShopMessageFromClient(message, true);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Send a chat message
        /// </summary>
        /// <returns></returns>
        [HttpPost("SendChatMessage")]
        public async Task<string> SendChatMessage([FromBody] object jsonData)
        {
            try
            {
                if(jsonData != null)
                {
                    var chatPayload = JsonConvert.DeserializeObject<Chat.ChatPayload>(jsonData.ToString());
                    if(chatPayload == null)
                        return JsonConvert.SerializeObject(new { Success = false, Message = "Chat Payload cannot be null" });

                    var localAddress = AccountData.GetSingleAccount(chatPayload.FromAddress);

                    if(localAddress == null)
                        return JsonConvert.SerializeObject(new { Success = false, Message = "You must own the from address" });

                    if(Globals.DecShopData?.DecShop == null)
                        return JsonConvert.SerializeObject(new { Success = false, Message = "DecShop Data cannot be null." });

                    var messageLengthCheck = chatPayload.Message.ToLengthCheck(240);
                    if (!messageLengthCheck)
                        return JsonConvert.SerializeObject(new { Success = false, Message = "Message is too long. Please shorten to 240 characters." });

                    var chatMessage = new Chat.ChatMessage {
                        Id = RandomStringUtility.GetRandomString(10, true),
                        FromAddress = localAddress.Address,
                        Message = chatPayload.Message,
                        ToAddress = Globals.DecShopData.DecShop.DecShopURL,
                        MessageHash = chatPayload.Message.ToHash(),
                        ShopURL = Globals.DecShopData.DecShop.DecShopURL,
                        TimeStamp = TimeUtil.GetTime(),
                    };

                    chatMessage.Signature = SignatureService.CreateSignature(chatMessage.FromAddress + chatMessage.TimeStamp.ToString(), localAddress.GetPrivKey, localAddress.PublicKey);
                    var chatMessageJson = JsonConvert.SerializeObject(chatMessage);

                    Message message = new Message
                    {
                        Address = ConnectingAddress,
                        Data = chatMessageJson,
                        Type = MessageType.Chat,
                        ComType = MessageComType.Chat
                    };

                    if(Globals.ChatMessageDict.TryGetValue(chatMessage.ShopURL,out var chatMessageList))
                    {
                        chatMessageList.Add(chatMessage);
                        Globals.ChatMessageDict[chatMessage.ShopURL] = chatMessageList;
                    }
                    else
                    {
                        Globals.ChatMessageDict.TryAdd(chatMessage.ShopURL, new List<Chat.ChatMessage> { chatMessage });
                    }
                    
                    _ = DSTClient.SendShopMessageFromClient(message, false);

                    return JsonConvert.SerializeObject(new { Success = true, Message = "Message sent.", MessageId = chatMessage.Id});
                }
                
            }
            catch(Exception ex) 
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error: {ex.ToString()}" });
            }

            return JsonConvert.SerializeObject(new { Success = false, Message = "Wallet already has a dec shop associated to it." }); ;
        }

        /// <summary>
        /// Resend a chat message
        /// </summary>
        /// <param name="messageId"></param>
        /// <param name="shopUrl"></param>
        /// <returns></returns>
        [HttpGet("ResendChatMessage/{messageId}/{**shopUrl}")]
        public async Task<string> ResendChatMessage(string messageId, string shopUrl)
        {
            if(Globals.ChatMessageDict.TryGetValue(shopUrl, out var chatMessageList))
            {
                var chatMessage = chatMessageList.Where(x => x.Id == messageId).FirstOrDefault();

                if(chatMessage == null)
                    return JsonConvert.SerializeObject(new { Success = true, Message = "Chat ID found, but message was null."});

                if(chatMessage.MessageReceived)
                    return JsonConvert.SerializeObject(new { Success = true, Message = "Message was reported as received." });

                var chatMessageJson = JsonConvert.SerializeObject(chatMessage);

                Message message = new Message
                {
                    Address = ConnectingAddress,
                    Data = chatMessageJson,
                    Type = MessageType.Chat,
                    ComType = MessageComType.Chat
                };

                _ = DSTClient.SendShopMessageFromClient(message, false);

                return JsonConvert.SerializeObject(new { Success = true, Message = "Message Resent."});
            }
            else
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = "Chat messages were not found." });
            }
        }

        /// <summary>
        /// Get chat messages for client, not a shop
        /// </summary>
        /// <param name="shopUrl"></param>
        /// <returns></returns>
        [HttpGet("GetDetailedChatMessages/{**shopUrl}")]
        public async Task<string> GetDetailedChatMessages(string shopUrl)
        {
            if (Globals.ChatMessageDict.TryGetValue(shopUrl, out var chatMessageList))
            {
                if (chatMessageList.Count > 0)
                {
                    return JsonConvert.SerializeObject(new { Success = true, Message = "Messages Found.", ChatMessages = chatMessageList });
                }
                else
                {
                    return JsonConvert.SerializeObject(new { Success = false, Message = "Chat messages not found." });
                }
            }
            else
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = "Chat messages not found for this shop." });
            }
        }

        /// <summary>
        /// Delete a chat message tree. Key = the identifier. For a client it would be the URL. for a Shop it would from the from RBX address.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        [HttpGet("DeleteChatMessages/{**key}")]
        public async Task<string> DeleteChatMessages(string key)
        {
            if (Globals.ChatMessageDict.TryRemove(key, out var chatMessageList))
            {
                return JsonConvert.SerializeObject(new { Success = true, Message = "Chat messages have been deleted." });
            }
            else
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = "Chat messages not found for this shop." });
            }
        }

        /// <summary>
        /// Get chat messages for client, not a shop
        /// </summary>
        /// <param name="shopUrl"></param>
        /// <returns></returns>
        [HttpGet("GetSimpleChatMessages/{**shopUrl}")]
        public async Task<string> GetSimpleChatMessages(string shopUrl)
        {
            if (Globals.ChatMessageDict.TryGetValue(shopUrl, out var chatMessageList))
            {
                if (chatMessageList.Count > 0)
                {
                    var simpleChatMessage = chatMessageList.Select(x => new { x.Id, x.Message, x.TimeStamp, x.FromAddress, x.ToAddress, x.IsShopSentMessage }).ToList();
                    return JsonConvert.SerializeObject(new { Success = true, Message = "Messages Found.", ChatMessages = simpleChatMessage });
                }
                else
                {
                    return JsonConvert.SerializeObject(new { Success = false, Message = "Chat messages not found." });
                }
            }
            else
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = "Chat messages not found for this shop." });
            }
        }

        /// <summary>
        /// Gets a specific chat message for client, not a shop
        /// </summary>
        /// <param name="messageId"></param>
        /// <returns></returns>
        [HttpGet("GetSpecificChatMessages/{messageId}")]
        public async Task<string> GetSpecificChatMessages(string messageId)
        {
            var specificMessage = Globals.ChatMessageDict.Values.SelectMany(x => x).Where(y => y.Id == messageId).FirstOrDefault();

            if (specificMessage != null)
            {
                return JsonConvert.SerializeObject(new { Success = true, Message = "Message Resent.", ChatMessage = specificMessage });
            }
            else
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = "Chat message was not found." });
            }
        }


        /// <summary>
        /// Gets first message for summary
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetMostRecentChatMessages/{key}")]
        public async Task<string> GetMostRecentChatMessages(string key)
        {
            if (Globals.ChatMessageDict.ContainsKey(key))
            {
                var chat = Globals.ChatMessageDict[key];

                if (chat == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Messages Found for {key}." });

                var chatSummary = chat.OrderByDescending(x => x.TimeStamp).Take(50);

                return JsonConvert.SerializeObject(new { Success = true, Message = $"Messages Found for {key}.", ChatMessages = chatSummary });
            }
            else
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = "Chat messages not found." });
            }
        }

        /// <summary>
        /// Gets first message for shop summary, not a client/buyer method
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetSummaryChatMessages")]
        public async Task<string> GetSummaryChatMessages()
        {
            var chatMessages = Globals.ChatMessageDict.Keys.ToList();
            if (chatMessages.Count > 0)
            {
                var sMessages = Globals.ChatMessageDict.Select(x => new {
                    User = x.Key,
                    Messages = x.Value.Count > 0 ? x.Value.OrderByDescending(x => x.TimeStamp).Take(1) : null
                }).ToList();
                return JsonConvert.SerializeObject(new { Success = true, Message = "Messages Found.", ChatMessages = sMessages });
            }
            else
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = "Chat messages not found." });
            }
        }

        /// <summary>
        /// Send a chat message from a shop
        /// </summary>
        /// <returns></returns>
        [HttpPost("SendShopChatMessage")]
        public async Task<string> SendShopChatMessage([FromBody] object jsonData)
        {
            try
            {
                if (jsonData != null)
                {
                    var chatPayload = JsonConvert.DeserializeObject<Chat.ChatPayload>(jsonData.ToString());
                    if (chatPayload == null)
                        return JsonConvert.SerializeObject(new { Success = false, Message = "Chat Payload cannot be null" });

                    var localAddress = AccountData.GetSingleAccount(chatPayload.FromAddress);

                    if (localAddress == null)
                        return JsonConvert.SerializeObject(new { Success = false, Message = "You must own the from address" });

                    var myDecShop = DecShop.GetMyDecShopInfo();

                    if (myDecShop == null)
                        return JsonConvert.SerializeObject(new { Success = false, Message = "DecShop Data cannot be null." });

                    if(myDecShop.OwnerAddress != chatPayload.FromAddress)
                        return JsonConvert.SerializeObject(new { Success = false, Message = "Only the shop  any may send messages back" });

                    var messageLengthCheck = chatPayload.Message.ToLengthCheck(240);
                    if(!messageLengthCheck)
                        return JsonConvert.SerializeObject(new { Success = false, Message = "Message is too long. Please shorten to 240 characters." });

                    if(chatPayload.ToAddress == null)
                        return JsonConvert.SerializeObject(new { Success = false, Message = "'To' address cannot be null." });

                    var endpoint = Globals.ShopChatUsers[chatPayload.ToAddress];

                    if(endpoint == null)
                        return JsonConvert.SerializeObject(new { Success = false, Message = "Shop Endpoint was null. Please ensure you have sent a message to them before and that they are actively communicating with you." });

                    var chatMessage = new Chat.ChatMessage
                    {
                        Id = RandomStringUtility.GetRandomString(10, true),
                        FromAddress = localAddress.Address,
                        Message = chatPayload.Message,
                        ToAddress = chatPayload.ToAddress,
                        MessageHash = chatPayload.Message.ToHash(),
                        ShopURL = myDecShop.DecShopURL,
                        TimeStamp = TimeUtil.GetTime(),
                        IsShopSentMessage = true,
                    };

                    chatMessage.Signature = SignatureService.CreateSignature(chatMessage.FromAddress + chatMessage.TimeStamp.ToString(), localAddress.GetPrivKey, localAddress.PublicKey);
                    var chatMessageJson = JsonConvert.SerializeObject(chatMessage);

                    Message message = new Message
                    {
                        Address = ConnectingAddress,
                        Data = chatMessageJson,
                        Type = MessageType.Chat,
                        ComType = MessageComType.Chat
                    };

                    if (Globals.ChatMessageDict.TryGetValue(chatPayload.ToAddress, out var chatMessageList))
                    {
                        chatMessageList.Add(chatMessage);
                        Globals.ChatMessageDict[chatPayload.ToAddress] = chatMessageList;
                    }
                    else
                    {
                        Globals.ChatMessageDict.TryAdd(chatPayload.ToAddress, new List<Chat.ChatMessage> { chatMessage });
                    }

                    _ = DSTClient.SendClientMessageFromShop(message, endpoint, false);

                    return JsonConvert.SerializeObject(new { Success = true, Message = "Message sent." });
                }

            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error: {ex.ToString()}" });
            }

            return JsonConvert.SerializeObject(new { Success = false, Message = "Wallet already has a dec shop associated to it." }); 
        }

        /// <summary>
        /// Get chat messages for shop, not a client/buyer
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetSimpleShopChatMessages")]
        public async Task<string> GetSimpleShopChatMessages()
        {
            var chatMessages = Globals.ChatMessageDict.Keys.ToList();
            if (chatMessages?.Count > 0)
            {
                var sMessages = Globals.ChatMessageDict.Select(x => new {
                    User = x.Key,
                    Messages = x.Value.Count > 0 ? x.Value.Select(y => new { y.Id, y.Message, y.TimeStamp, y.FromAddress, y.ToAddress, y.IsShopSentMessage }) : null
                }).ToList();

                return JsonConvert.SerializeObject(new { Success = true, Message = "Messages Found.", ChatMessages = sMessages });
            }
            else
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = "Chat messages not found." });
            }
        }


        /// <summary>
        /// Get chat messages for shop, not a client/buyer
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetDetailedShopChatMessages")]
        public async Task<string> GetDetailedShopChatMessages()
        {
            var chatMessages = Globals.ChatMessageDict.Keys.ToList();
            if (chatMessages.Count > 0)
            {
                var sMessages = Globals.ChatMessageDict.Select(x => new {
                    User = x.Key,
                    Messages = x.Value.Count > 0 ? x.Value : null
                }).ToList();
                return JsonConvert.SerializeObject(new { Success = true, Message = "Messages Found.", ChatMessages = sMessages });
            }
            else
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = "Chat messages not found." });
            }
        }

        /// <summary>
        /// Gets a specific chat messages for shop, not a client/buyer
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetDetailedSpecificShopChatMessages/{rbxAddress}")]
        public async Task<string> GetDetailedSpecificShopChatMessages(string rbxAddress)
        {
            if(Globals.ChatMessageDict.ContainsKey(rbxAddress))
            {
                var chat = Globals.ChatMessageDict[rbxAddress];
                return JsonConvert.SerializeObject(new { Success = true, Message = $"Messages Found for {rbxAddress}.", ChatMessages = chat });
            }
            else
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = "Chat messages not found." });
            }
        }

        /// <summary>
        /// Gets simple specific chat messages for shop, not a client/buyer
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetSimpleSpecificShopChatMessages/{rbxAddress}")]
        public async Task<string> GetSimpleSpecificShopChatMessages(string rbxAddress)
        {
            if (Globals.ChatMessageDict.ContainsKey(rbxAddress))
            {
                var chat = Globals.ChatMessageDict[rbxAddress];
                var chatSimple = chat.Select(x => new { x.Message, x.TimeStamp, x.FromAddress, x.ToAddress, x.IsShopSentMessage });
                return JsonConvert.SerializeObject(new { Success = true, Message = $"Messages Found for {rbxAddress}.", ChatMessages = chatSimple });
            }
            else
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = "Chat messages not found." });
            }
        }

        /// <summary>
        /// Returns the shops info stored in memory.'
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetDecShopData")]
        public async Task<string> GetDecShopData()
        {
            if (Globals.DecShopData != null)
            {
                return JsonConvert.SerializeObject(new { Success = true, Message = "Data Found.", Globals.DecShopData }, Formatting.Indented);
            }
            else
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = "Data not found." });
            }
        }

        /// <summary>
        /// Complete NFT purchase
        /// </summary>
        /// <param name="keySign"></param>
        /// <param name="scUID"></param>
        /// <returns></returns>
        [HttpGet("CompleteNFTPurchase/{keySign}/{**scUID}")]
        public async Task<string> CompleteNFTPurchase(string keySign, string scUID)
        {
            var scStateTrei = SmartContractStateTrei.GetSmartContractState(scUID);

            if(scStateTrei == null)
                return JsonConvert.SerializeObject(new { Success = false, Message = "Smart Contract was not found." });

            var nextOwner = scStateTrei.NextOwner;
            var purchaseAmount = scStateTrei.PurchaseAmount;

            if(nextOwner == null || purchaseAmount == null)
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Smart contract data missing or purchase already completed. Next Owner: {nextOwner} | Purchase Amount {purchaseAmount}" });

            var localAccount = AccountData.GetSingleAccount(nextOwner);

            if(localAccount == null)
                return JsonConvert.SerializeObject(new { Success = false, Message = $"A local account with next owner address was not found. Next Owner: {nextOwner}" });

            if(localAccount.Balance <= purchaseAmount.Value)
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Not enough funds to purchase NFT. Purchase Amount {purchaseAmount} | Current Balance: {localAccount.Balance}." });

            var result = await SmartContractService.CompleteSaleSmartContractTX(scUID, scStateTrei.OwnerAddress, purchaseAmount.Value, keySign);

            return JsonConvert.SerializeObject(new { Success = result.Item1 == null ? false : true, Message = result.Item2 });
        }

        /// <summary>
        /// Debug Data for DST
        /// </summary>
        /// <returns></returns>
        [HttpGet("Debug")]
        public async Task<string> Debug()
        {
            var clients = Globals.ConnectedClients;
            var shops = Globals.ConnectedShops;
            var stunServer = Globals.STUNServer;

            return JsonConvert.SerializeObject(new { Success = true, Clients = clients, Shops = shops, StunServer = stunServer }, Formatting.Indented);           
        }

        /// <summary>
        /// Debug Data for DST
        /// </summary>
        /// <returns></returns>
        [HttpGet("DebugData")]
        public async Task<string> DebugData()
        {
            var CollectionCount = Collection.GetLiveCollections();
            var ListingCount = Listing.GetLiveListingsCount();
            var AuctionCount = Auction.GetLiveAuctionsCount();

            return JsonConvert.SerializeObject(new { Success = true, CollectionCount, ListingCount, AuctionCount }, Formatting.Indented);
        }
    }
}
