using Newtonsoft.Json;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.DST;
using ReserveBlockCore.Utilities;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace ReserveBlockCore.DST
{
    public class DecShopMessageService
    {
        public static Message? ProcessMessage(Message message)
        {
            if (message.ComType == MessageComType.Request)
            {
                var result = RequestMessage(message);
                return result;
            }
            
            if (message.ComType == MessageComType.Response)
            {
                ResponseMessage(message);
                return null;
            }

            return null;
        }

        private static void ResponseMessage(Message message)
        {
            try
            {
                Globals.ClientMessageDict.TryGetValue(message.ResponseMessageId, out var msg);
                if (msg != null)
                {
                    var requestOptArray = msg.Message.Data.Split(',');
                    var requestOpt = requestOptArray[0];
                    if (requestOpt != null)
                    {
                        var option = requestOpt;
                        if (option == "Info")
                        {
                            var decShopInfo = JsonConvert.DeserializeObject<DecShop>(message.Data);
                            if (Globals.DecShopData == null)
                            {
                                Globals.DecShopData = new DecShopData
                                {
                                    DecShop = decShopInfo
                                };
                            }
                            else
                            {
                                Globals.DecShopData.DecShop = decShopInfo;
                            }

                            msg.HasReceivedResponse = true;
                            msg.MessageResponseReceivedTimestamp = TimeUtil.GetTime();
                            Globals.ClientMessageDict[message.ResponseMessageId] = msg;
                        }
                        else if(option == "Collections")
                        {
                            var collections = JsonConvert.DeserializeObject<List<Collection>>(message.Data);
                            if(collections != null)
                            {
                                if (Globals.DecShopData == null)
                                {
                                    Globals.DecShopData = new DecShopData
                                    {
                                        Collections = collections,
                                    };
                                }
                                else
                                {
                                    foreach (var collection in collections)
                                    {
                                        if (Globals.DecShopData.Collections != null)
                                        {
                                            var colExist = Globals.DecShopData.Collections.Exists(x => x.Name == collection.Name);
                                            if (!colExist)
                                                Globals.DecShopData.Collections.Add(collection);
                                        }
                                        else
                                        {
                                            Globals.DecShopData.Collections = new List<Collection> { collection };
                                        }
                                    }
                                }
                            }
                            

                            msg.HasReceivedResponse = true;
                            msg.MessageResponseReceivedTimestamp = TimeUtil.GetTime();
                            Globals.ClientMessageDict[message.ResponseMessageId] = msg;
                        }
                        else if (option == "Listings")
                        {
                            var listings = JsonConvert.DeserializeObject<List<Listing>>(message.Data);
                            if (listings != null)
                            {
                                if (Globals.DecShopData == null)
                                {
                                    Globals.DecShopData = new DecShopData
                                    {
                                        Listings = listings,
                                    };
                                }
                                else
                                {
                                    foreach (var listing in listings)
                                    {
                                        if (Globals.DecShopData.Listings != null)
                                        {
                                            var listingExist = Globals.DecShopData.Listings.Exists(x => x.SmartContractUID == listing.SmartContractUID);
                                            if (!listingExist)
                                                Globals.DecShopData.Listings.Add(listing);
                                        }
                                        else
                                        {
                                            Globals.DecShopData.Listings = new List<Listing> { listing };
                                        }
                                    }
                                }
                            }


                            msg.HasReceivedResponse = true;
                            msg.MessageResponseReceivedTimestamp = TimeUtil.GetTime();
                            Globals.ClientMessageDict[message.ResponseMessageId] = msg;
                        }
                        else if (option == "ListingsByCollection")
                        {
                            var listings = JsonConvert.DeserializeObject<List<Listing>>(message.Data);
                            if (listings != null)
                            {
                                if (Globals.DecShopData == null)
                                {
                                    Globals.DecShopData = new DecShopData
                                    {
                                        Listings = listings,
                                    };
                                }
                                else
                                {
                                    foreach (var listing in listings)
                                    {
                                        if (Globals.DecShopData.Listings != null)
                                        {
                                            var listingExist = Globals.DecShopData.Listings.Exists(x => x.SmartContractUID == listing.SmartContractUID);
                                            if (!listingExist)
                                                Globals.DecShopData.Listings.Add(listing);
                                        }
                                        else
                                        {
                                            Globals.DecShopData.Listings = new List<Listing> { listing };
                                        }
                                    }
                                }
                            }


                            msg.HasReceivedResponse = true;
                            msg.MessageResponseReceivedTimestamp = TimeUtil.GetTime();
                            Globals.ClientMessageDict[message.ResponseMessageId] = msg;
                        }
                        else if (option == "SpecificListing")
                        {
                            var listing = JsonConvert.DeserializeObject<Listing>(message.Data);
                            if (listing != null)
                            {
                                if (Globals.DecShopData == null)
                                {
                                    Globals.DecShopData = new DecShopData
                                    {
                                        Listings = new List<Listing> { listing},
                                    };
                                }
                                else
                                {
                                    if (Globals.DecShopData.Listings != null)
                                    {
                                        var listingExist = Globals.DecShopData.Listings.Exists(x => x.SmartContractUID == listing.SmartContractUID);
                                        if (!listingExist)
                                            Globals.DecShopData.Listings.Add(listing);
                                    }
                                    else
                                    {
                                        Globals.DecShopData.Listings = new List<Listing> { listing };
                                    }
                                }
                            }


                            msg.HasReceivedResponse = true;
                            msg.MessageResponseReceivedTimestamp = TimeUtil.GetTime();
                            Globals.ClientMessageDict[message.ResponseMessageId] = msg;
                        }
                        else
                        {

                        }
                    }
                }

            }
            catch
            {
                Globals.ClientMessageDict.TryGetValue(message.ResponseMessageId, out var msg);
                if (msg != null)
                {
                    msg.HasReceivedResponse = true;
                    msg.MessageResponseReceivedTimestamp = TimeUtil.GetTime();
                    msg.DidMessageRequestFail = true;
                }
            }
        }

        private static Message RequestMessage(Message message)
        {
            try
            {
                var requestOptArray = message.Data.Split(',');
                var requestOpt = requestOptArray[0];
                if (requestOpt != null)
                {
                    var option = requestOpt;
                    if (option == "Info")
                    {
                        var decShop = DecShop.GetMyDecShopInfo();
                        if (decShop != null)
                        {
                            var respMessage = new Message
                            {
                                ResponseMessage = true,
                                ResponseMessageId = message.Id,
                                Type = message.Type,
                                ComType = MessageComType.Response,
                                Data = JsonConvert.SerializeObject(decShop)
                            };

                            return respMessage;
                        }
                        else
                        {
                            var respMessage = new Message
                            {
                                ResponseMessage = true,
                                ResponseMessageId = message.Id,
                                Type = message.Type,
                                ComType = MessageComType.Response,
                                Data = "BAD"
                            };

                            return respMessage;
                        }
                    }
                    else if(option == "Collections")
                    {
                        var collections = Collection.GetAllCollections()?.Where(x => x.CollectionLive).ToList();
                        if(collections?.Count() > 0)
                        {
                            var respMessage = new Message
                            {
                                ResponseMessage = true,
                                ResponseMessageId = message.Id,
                                Type = message.Type,
                                ComType = MessageComType.Response,
                                Data = JsonConvert.SerializeObject(collections)
                            };

                            collections.Clear();
                            collections = new List<Collection>();

                            return respMessage;
                        }
                        else
                        {
                            var respMessage = new Message
                            {
                                ResponseMessage = true,
                                ResponseMessageId = message.Id,
                                Type = message.Type,
                                ComType = MessageComType.Response,
                                Data = ""
                            };

                            return respMessage;
                        }
                    }
                    else if (option == "Listings")
                    {
                        var pageParse = int.TryParse(requestOptArray[1], out var page);
                        if(!pageParse)
                        {
                            var respMessage = new Message
                            {
                                ResponseMessage = true,
                                ResponseMessageId = message.Id,
                                Type = message.Type,
                                ComType = MessageComType.Response,
                                Data = ""
                            };

                            return respMessage;
                        }
                        var listings = Listing.GetAllListings()?.Where(x => x.IsVisibleAfterEndDate && x.IsVisibleBeforeStartDate && !x.IsCancelled).ToList();
                        if(listings?.Count() > 0)
                        {
                            var pageSkip = page * 6;
                            var listingsPageApplied = listings.Skip(pageSkip);

                            var respMessage = new Message
                            {
                                ResponseMessage = true,
                                ResponseMessageId = message.Id,
                                Type = message.Type,
                                ComType = MessageComType.Response,
                                Data = JsonConvert.SerializeObject(listingsPageApplied)
                            };

                            listings.Clear();
                            listings = new List<Listing>();

                            return respMessage;
                        }
                        else
                        {
                            var respMessage = new Message
                            {
                                ResponseMessage = true,
                                ResponseMessageId = message.Id,
                                Type = message.Type,
                                ComType = MessageComType.Response,
                                Data = ""
                            };

                            return respMessage;
                        }
                    }
                    else if (option == "ListingsByCollection")
                    {
                        var collectionidParse = int.TryParse(requestOptArray[1], out var collectionId);
                        var pageParse = int.TryParse(requestOptArray[2], out var page);
                        if (!collectionidParse || !pageParse)
                        {
                            var respMessage = new Message
                            {
                                ResponseMessage = true,
                                ResponseMessageId = message.Id,
                                Type = message.Type,
                                ComType = MessageComType.Response,
                                Data = ""
                            };

                            return respMessage;
                        }

                        var listings = Listing.GetAllListings()?.Where(x => x.IsVisibleAfterEndDate && x.IsVisibleBeforeStartDate && !x.IsCancelled && x.CollectionId == collectionId).ToList();
                        if (listings?.Count() > 0)
                        {
                            var pageSkip = page * 6;
                            var listingsPageApplied = listings.Skip(pageSkip);
                            var respMessage = new Message
                            {
                                ResponseMessage = true,
                                ResponseMessageId = message.Id,
                                Type = message.Type,
                                ComType = MessageComType.Response,
                                Data = JsonConvert.SerializeObject(listingsPageApplied)
                            };

                            listings.Clear();
                            listings = new List<Listing>();

                            return respMessage;
                        }
                        else
                        {
                            var respMessage = new Message
                            {
                                ResponseMessage = true,
                                ResponseMessageId = message.Id,
                                Type = message.Type,
                                ComType = MessageComType.Response,
                                Data = ""
                            };

                            return respMessage;
                        }
                    }
                    
                    else if (option == "SpecificListing")
                    {
                        var scUID = requestOptArray[1];

                        if (!string.IsNullOrEmpty(scUID))
                        {
                            var respMessage = new Message
                            {
                                ResponseMessage = true,
                                ResponseMessageId = message.Id,
                                Type = message.Type,
                                ComType = MessageComType.Response,
                                Data = ""
                            };

                            return respMessage;
                        }

                        var listing = Listing.GetAllListings()?.Where(x => x.IsVisibleAfterEndDate && x.IsVisibleBeforeStartDate && !x.IsCancelled && x.SmartContractUID == scUID).FirstOrDefault();
                        if (listing != null)
                        {
                            var respMessage = new Message
                            {
                                ResponseMessage = true,
                                ResponseMessageId = message.Id,
                                Type = message.Type,
                                ComType = MessageComType.Response,
                                Data = JsonConvert.SerializeObject(listing)
                            };

                            return respMessage;
                        }
                        else
                        {
                            var respMessage = new Message
                            {
                                ResponseMessage = true,
                                ResponseMessageId = message.Id,
                                Type = message.Type,
                                ComType = MessageComType.Response,
                                Data = ""
                            };

                            return respMessage;
                        }
                    }
                    else
                    {
                        //unknown?
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                var respMessage = new Message
                {
                    ResponseMessage = true,
                    ResponseMessageId = message.Id,
                    Type = message.Type,
                    ComType = MessageComType.Response,
                    Data = "BAD"
                };

                return respMessage;
            }

            var badRequest = new Message
            {
                ResponseMessage = true,
                ResponseMessageId = message.Id,
                Type = message.Type,
                ComType = MessageComType.Response,
                Data = "BAD"
            };

            return badRequest;
        }
    }
}
