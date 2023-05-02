using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.DST;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.DST;
using System.Diagnostics;

namespace ReserveBlockCore.Controllers
{
    [ActionFilterController]
    [Route("wsapi/[controller]")]
    [Route("wsapi/[controller]/{somePassword?}")]
    [ApiController]
    public class WebShopV1Controller : ControllerBase
    {
        /// <summary>
        /// Check Status of API
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "RBX-Wallet Web Shop API", "WS API" };
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
                //removes current connection to shop
                await DSTMultiClient.DisconnectFromShop(url);
                var connectionResult = await DSTMultiClient.ConnectToShop(url, address);

                //if connectionResult == true create some looping event.

                if (connectionResult.Item1)
                    _ = DSTMultiClient.GetShopData(address, connectionResult.Item2);

                return connectionResult.Item1;
            }

            return false;
        }

        /// <summary>
        /// Returns the shops info stored in memory.'
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetDecShopData")]
        public async Task<string> GetDecShopData()
        {
            if (Globals.MultiDecShopData.Count > 0)
            {
                return JsonConvert.SerializeObject(new { Success = true, Message = "Data Found.", Globals.MultiDecShopData }, Formatting.Indented);
            }
            else
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = "Data not found." });
            }
        }

        /// <summary>
        /// Gets shop info
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetConnections")]
        public async Task<string> GetConnections()
        {
            var connectedShops = DSTMultiClient.ShopConnections.Where(x => x.Value.IsConnected).Select(x => x.Value).ToList();
            if (connectedShops.Count() > 0)
            {
                List<DecShop> DecShopList = new List<DecShop>();
                foreach(var shop in connectedShops) 
                { 
                    if(Globals.MultiDecShopData.TryGetValue(shop.ShopURL, out var decShopData))
                    {
                        if(decShopData.DecShop != null)
                        {
                            DecShopList.Add(decShopData.DecShop);
                        }
                        
                    }
                }

                if (DecShopList.Count > 0)
                    return JsonConvert.SerializeObject(new { Success = true, Message = $"Connected Shops Found", MultiDecShop = DecShopList, Connected = true }, Formatting.Indented);
            }
            return JsonConvert.SerializeObject(new { Success = true, Message = $"No Shops Found", DecShop = "", Connected = false }); ;
        }

        /// <summary>
        /// Gets shop Listings by collection from a specified URL
        /// </summary>
        /// <param name="listingId"></param>
        /// <param name="address"></param>
        /// <param name="shopURL"></param>
        /// <returns></returns>
        [HttpGet("GetShopSpecificAuction/{listingId}/{address}/{**shopURL}")]
        public async Task<bool> GetShopSpecificAuction(string listingId, string address, string shopURL)
        {
            if (DSTMultiClient.ShopConnections.TryGetValue(shopURL, out var shopConnection))
            {
                Message message = new Message
                {
                    Address = address,
                    Data = $"{DecShopRequestOptions.SpecificAuction},{listingId}",
                    Type = MessageType.DecShop,
                    ComType = MessageComType.Request
                };

                _ = DSTMultiClient.SendShopMessageFromClient(message, true, shopConnection.UdpClient, shopConnection.EndPoint);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Get bids for specific listing from shop - NOT LOCAL
        /// </summary>
        /// <param name="listingId"></param>
        /// <param name="address"></param>
        /// <param name="shopURL"></param>
        /// <returns></returns>
        [HttpGet("GetShopListingBids/{listingId}/{address}/{**shopURL}")]
        public async Task<bool> GetShopListingBids(int listingId, string address, string shopURL)
        {
            if (DSTMultiClient.ShopConnections.TryGetValue(shopURL, out var shopConnection))
            {
                Message message = new Message
                {
                    Address = address,
                    Data = $"{DecShopRequestOptions.Bids},{listingId}",
                    Type = MessageType.DecShop,
                    ComType = MessageComType.Request
                };

                _ = DSTMultiClient.SendShopMessageFromClient(message, true, shopConnection.UdpClient, shopConnection.EndPoint);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Send a bid to a listing
        /// </summary>
        /// <param name="address"></param>
        /// <param name="shopURL"></param>
        /// <returns></returns>
        [HttpPost("SendBid/{address}/{**shopURL}")]
        public async Task<string> SendBid([FromRoute] string address, [FromRoute] string shopURL, [FromBody] object jsonData)
        {
            try
            {
                if (jsonData != null)
                {
                    if (DSTMultiClient.ShopConnections.TryGetValue(shopURL, out var shopConnection))
                    {
                        var bidPayload = JsonConvert.DeserializeObject<Bid>(jsonData.ToString());
                        if (bidPayload == null)
                            return JsonConvert.SerializeObject(new { Success = false, Message = "Bid Payload cannot be null" });

                        if (!bidPayload.RawBid)
                        {
                            var localAddress = AccountData.GetSingleAccount(bidPayload.BidAddress);

                            if (localAddress == null)
                                return JsonConvert.SerializeObject(new { Success = false, Message = "You must own the bid address address" });
                        }

                        if (bidPayload.BidAddress.StartsWith("xRBX"))
                            return JsonConvert.SerializeObject(new { Success = false, Message = "You may not place bids with a Reserve Account" });

                        if (bidPayload.BidStatus != BidStatus.Accepted && bidPayload.BidStatus != BidStatus.Rejected)
                        {
                            if (!DSTMultiClient.ShopConnections.TryGetValue(shopURL, out var shopConnection1))
                                return JsonConvert.SerializeObject(new { Success = false, Message = "DecShop Data cannot be null." });
                        }

                        var thirdPartyBid = bidPayload.BidStatus == BidStatus.Accepted || bidPayload.BidStatus == BidStatus.Rejected ? true : false;

                        var bidBuild = bidPayload.Build(thirdPartyBid);

                        if (bidBuild == false)
                            return JsonConvert.SerializeObject(new { Success = false, Message = "Failed to build bid." });

                        var bidJson = JsonConvert.SerializeObject(bidPayload);

                        Message message = new Message
                        {
                            Address = address,
                            Data = bidJson,
                            Type = MessageType.Bid,
                            ComType = MessageComType.Request
                        };

                        var bidSave = Bid.SaveBid(bidPayload);

                        if (bidPayload.BidStatus != BidStatus.Accepted && bidPayload.BidStatus != BidStatus.Rejected)
                            _ = DSTMultiClient.SendShopMessageFromClient(message, false, shopConnection.UdpClient, shopConnection.EndPoint);

                        return JsonConvert.SerializeObject(new { Success = true, Message = "Bid sent.", BidId = bidPayload.Id, Bid = bidPayload });
                    }
                    else
                    {
                        return JsonConvert.SerializeObject(new { Success = false, Message = $"You are not connected to: {shopURL}" });
                    }
                }

            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error: {ex.ToString()}" });
            }

            return JsonConvert.SerializeObject(new { Success = false, Message = "Wallet already has a dec shop associated to it." }); ;
        }

        /// <summary>
        /// Send a Buy Now to a listing
        /// </summary>
        /// <param name="address"></param>
        /// <param name="shopURL"></param>
        /// <returns></returns>
        [HttpPost("SendBuyNowBid/{address}/{**shopURL}")]
        public async Task<string> SendBuyNowBid([FromRoute] string address, [FromRoute] string shopURL, [FromBody] object jsonData)
        {
            try
            {
                if (jsonData != null)
                {
                    if (DSTMultiClient.ShopConnections.TryGetValue(shopURL, out var shopConnection))
                    {
                        var bidPayload = JsonConvert.DeserializeObject<Bid>(jsonData.ToString());
                        if (bidPayload == null)
                            return JsonConvert.SerializeObject(new { Success = false, Message = "Bid Payload cannot be null" });

                        if (!bidPayload.RawBid)
                        {
                            var localAddress = AccountData.GetSingleAccount(bidPayload.BidAddress);

                            if (localAddress == null)
                                return JsonConvert.SerializeObject(new { Success = false, Message = "You must own the bid address address" });

                            if (bidPayload.BidAddress.StartsWith("xRBX"))
                                return JsonConvert.SerializeObject(new { Success = false, Message = "You may not perform a 'Buy Now' with a Reserve Account" });
                        }

                        if (bidPayload.BidStatus != BidStatus.Accepted && bidPayload.BidStatus != BidStatus.Rejected)
                        {
                            if (!DSTMultiClient.ShopConnections.TryGetValue(shopURL, out var shopConnection1))
                                return JsonConvert.SerializeObject(new { Success = false, Message = "DecShop Data cannot be null." });
                        }

                        var thirdPartyBid = bidPayload.BidStatus == BidStatus.Accepted || bidPayload.BidStatus == BidStatus.Rejected ? true : false;

                        var bidBuild = bidPayload.Build(thirdPartyBid);

                        if (bidPayload.IsBuyNow != true)
                            return JsonConvert.SerializeObject(new { Success = false, Message = "IsBuyNow must be set to 'true'." });

                        if (bidBuild == false)
                            return JsonConvert.SerializeObject(new { Success = false, Message = "Failed to build bid." });

                        var bidJson = JsonConvert.SerializeObject(bidPayload);

                        Message message = new Message
                        {
                            Address = address,
                            Data = bidJson,
                            Type = MessageType.Purchase,
                            ComType = MessageComType.Request
                        };

                        var bidSave = Bid.SaveBid(bidPayload);

                        if (bidPayload.BidStatus != BidStatus.Accepted && bidPayload.BidStatus != BidStatus.Rejected)
                            _ = DSTMultiClient.SendShopMessageFromClient(message, false, shopConnection.UdpClient, shopConnection.EndPoint);

                        return JsonConvert.SerializeObject(new { Success = true, Message = "Buy Now Bid sent.", BidId = bidPayload.Id, Bid = bidPayload });
                    }
                    else
                    {
                        return JsonConvert.SerializeObject(new { Success = false, Message = $"You are not connected to: {shopURL}" });
                    }
                }

            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error: {ex.ToString()}" });
            }

            return JsonConvert.SerializeObject(new { Success = false, Message = "Wallet already has a dec shop associated to it." }); ;
        }
    }
}
