using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.DST;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.DST;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
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
        /// Checks your status of connection to shop
        /// </summary>
        /// <param name="pingId"></param>
        /// <param name="url"></param>
        /// <returns></returns>
        [HttpGet("PingShop/{pingId}/{**url}")]
        public async Task<string> PingShop(string pingId, string url)
        {
            var decshop = await DecShop.GetDecShopStateTreiLeafByURL(url);

            if (decshop != null)
            {
                var result = await DSTMultiClient.PingConnection(url, pingId);

                if(result)
                {
                    return JsonConvert.SerializeObject(new { Success = result, Message = $"Ping Started Result: {result}", Ping = Globals.PingResultDict[pingId] });
                }
                else
                {
                    return JsonConvert.SerializeObject(new { Success = false, Message = $"Ping attempt failed.", Ping = (false, 0) });
                }
            }

            return JsonConvert.SerializeObject(new { Success = false, Message = "Ping Failed. Decshop not found." });
        }

        /// <summary>
        /// Checks your status of your ping request
        /// </summary>
        /// <param name="pingId"></param>
        /// <returns></returns>
        [HttpGet("CheckPingShop/{pingId}")]
        public async Task<string> CheckPingShop(string pingId)
        {
            if (Globals.PingResultDict.TryGetValue(pingId, out var value))
            {
                return JsonConvert.SerializeObject(new { Success = true, Message = $"Ping Result", Ping = value });
            }
            else 
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = "Could not find that PingId" });
            }            
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

        /// <summary>
        /// Send a chat message
        /// </summary>
        /// <param name="address"></param>
        /// <param name="shopURL"></param>
        /// <returns></returns>
        [HttpPost("SendChatMessage/{address}/{**shopURL}")]
        public async Task<string> SendChatMessage([FromRoute] string address, [FromRoute] string shopURL, [FromBody] object jsonData)
        {
            try
            {
                if (jsonData != null)
                {
                    if (DSTMultiClient.ShopConnections.TryGetValue(shopURL, out var shopConnection))
                    {
                        var chatPayload = JsonConvert.DeserializeObject<Chat.ChatPayload>(jsonData.ToString());
                        if (chatPayload == null)
                            return JsonConvert.SerializeObject(new { Success = false, Message = "Chat Payload cannot be null" });

                        if (!Globals.MultiDecShopData.TryGetValue(shopURL, out var decShopData))
                            return JsonConvert.SerializeObject(new { Success = false, Message = "DecShop Data cannot be null." });

                        var messageLengthCheck = chatPayload.Message.ToLengthCheck(240);
                        if (!messageLengthCheck)
                            return JsonConvert.SerializeObject(new { Success = false, Message = "Message is too long. Please shorten to 240 characters." });

                        var chatMessage = new Chat.ChatMessage
                        {
                            Id = RandomStringUtility.GetRandomString(10, true),
                            FromAddress = address,
                            Message = chatPayload.Message,
                            ToAddress = shopURL,
                            MessageHash = chatPayload.Message.ToHash(),
                            ShopURL = shopURL,
                            TimeStamp = chatPayload.TimeStamp.Value,
                            Signature = chatPayload.Signature,
                            IsThirdParty = chatPayload.IsThirdParty
                        };

                        var chatMessageJson = JsonConvert.SerializeObject(chatMessage);

                        Message message = new Message
                        {
                            Address = address,
                            Data = chatMessageJson,
                            Type = MessageType.Chat,
                            ComType = MessageComType.Chat
                        };

                        if (Globals.ChatMessageDict.TryGetValue(chatMessage.ShopURL, out var chatMessageList))
                        {
                            chatMessageList.Add(chatMessage);
                            Globals.ChatMessageDict[chatMessage.ShopURL] = chatMessageList;
                        }
                        else
                        {
                            Globals.ChatMessageDict.TryAdd(chatMessage.ShopURL, new List<Chat.ChatMessage> { chatMessage });
                        }

                        _ = DSTMultiClient.SendShopMessageFromClient(message, false, shopConnection.UdpClient, shopConnection.EndPoint);

                        return JsonConvert.SerializeObject(new { Success = true, Message = "Message sent.", MessageId = chatMessage.Id });
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
        /// Send a chat message
        /// </summary>
        /// <param name="address"></param>
        /// <param name="shopURL"></param>
        /// <returns></returns>
        [HttpPost("SendChatMessageThirdParty")]
        public async Task<string> SendChatMessageThirdParty([FromRoute] string address, [FromRoute] string shopURL, [FromBody] object jsonData)
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

                    var messageLengthCheck = chatPayload.Message.ToLengthCheck(240);
                    if (!messageLengthCheck)
                        return JsonConvert.SerializeObject(new { Success = false, Message = "Message is too long. Please shorten to 240 characters." });

                    var chatMessage = new Chat.ChatMessage
                    {
                        Id = RandomStringUtility.GetRandomString(10, true),
                        FromAddress = localAddress.Address,
                        Message = chatPayload.Message,
                        ToAddress = shopURL,
                        MessageHash = chatPayload.Message.ToHash(),
                        ShopURL = shopURL,
                        TimeStamp = TimeUtil.GetTime(),
                        IsThirdParty = chatPayload.IsThirdParty,
                    };

                    chatMessage.Signature = SignatureService.CreateSignature(chatMessage.FromAddress + chatMessage.TimeStamp.ToString(), localAddress.GetPrivKey, localAddress.PublicKey);
                    var chatMessageJson = JsonConvert.SerializeObject(chatMessage);

                    Message message = new Message
                    {
                        Address = address,
                        Data = chatMessageJson,
                        Type = MessageType.Chat,
                        ComType = MessageComType.Chat
                    };

                    if (Globals.ChatMessageDict.TryGetValue(chatMessage.ShopURL, out var chatMessageList))
                    {
                        chatMessageList.Add(chatMessage);
                        Globals.ChatMessageDict[chatMessage.ShopURL] = chatMessageList;
                    }
                    else
                    {
                        Globals.ChatMessageDict.TryAdd(chatMessage.ShopURL, new List<Chat.ChatMessage> { chatMessage });
                    }

                    return JsonConvert.SerializeObject(new { Success = true, Message = "Message sent.", MessageId = chatMessage.Id });
                }

            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"Unknown Error: {ex.ToString()}" });
            }

            return JsonConvert.SerializeObject(new { Success = false, Message = "Wallet already has a dec shop associated to it." }); ;
        }

        /// <summary>
        /// Resend a chat message
        /// </summary>
        /// <param name="messageId"></param>
        /// <param name="address"></param>
        /// <param name="shopUrl"></param>
        /// <returns></returns>
        [HttpGet("ResendChatMessage/{messageId}/{address}/{**shopUrl}")]
        public async Task<string> ResendChatMessage(string messageId, string address, string shopUrl)
        {
            if (DSTMultiClient.ShopConnections.TryGetValue(shopUrl, out var shopConnection))
            {
                if (Globals.ChatMessageDict.TryGetValue(shopUrl, out var chatMessageList))
                {
                    var chatMessage = chatMessageList.Where(x => x.Id == messageId).FirstOrDefault();

                    if (chatMessage == null)
                        return JsonConvert.SerializeObject(new { Success = true, Message = "Chat ID found, but message was null." });

                    if (chatMessage.MessageReceived)
                        return JsonConvert.SerializeObject(new { Success = true, Message = "Message was reported as received." });

                    var chatMessageJson = JsonConvert.SerializeObject(chatMessage);

                    Message message = new Message
                    {
                        Address = address,
                        Data = chatMessageJson,
                        Type = MessageType.Chat,
                        ComType = MessageComType.Chat
                    };

                    _ = DSTMultiClient.SendShopMessageFromClient(message, false, shopConnection.UdpClient, shopConnection.EndPoint);

                    return JsonConvert.SerializeObject(new { Success = true, Message = "Message Resent." });
                }
                else
                {
                    return JsonConvert.SerializeObject(new { Success = false, Message = "Chat messages were not found." });
                }
            }
            else
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = $"You are not connected to: {shopUrl}" });
            }

            
        }

        /// <summary>
        /// Debug Data for DST
        /// </summary>
        /// <returns></returns>
        [HttpGet("Debug")]
        public async Task<string> Debug()
        {
            try
            {
                List<DSTConnection> connections = new List<DSTConnection>();    
                var shops = DSTMultiClient.ShopConnections;
                foreach (var shopConnection in shops)
                {
                    connections.Add(new DSTConnection { 
                        AttemptReconnect = shopConnection.Value.AttemptReconnect,
                        ConnectDate = shopConnection.Value.ConnectDate,
                        ConnectionId= shopConnection.Value.ConnectionId,
                        InitialMessage= shopConnection.Value.InitialMessage,
                        IPAddress= shopConnection.Value.IPAddress,
                        KeepAliveStarted= shopConnection.Value.KeepAliveStarted,
                        LastMessageSent = shopConnection.Value.LastMessageSent,
                        LastReceiveMessage = shopConnection.Value.LastReceiveMessage,
                        LastSentMessage = shopConnection.Value.LastSentMessage 
                    });
                }

                return JsonConvert.SerializeObject(new { Success = true, Shops = connections }, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { Success = false, Message = ex.Message });
            }
        }
    }
}
