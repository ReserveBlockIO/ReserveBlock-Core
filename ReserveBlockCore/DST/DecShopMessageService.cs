using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.DST;
using ReserveBlockCore.Utilities;
using System;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using static ReserveBlockCore.Models.Mother;
using static System.Collections.Specialized.BitVector32;

namespace ReserveBlockCore.DST
{
    public class DecShopMessageService
    {
        const int PaginationAmount = 10;
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

                                            if (colExist)
                                            {
                                                int index = Globals.DecShopData.Collections.FindIndex(x => x.Name == collection.Name);
                                                if (index != -1)
                                                    Globals.DecShopData.Collections[index] = collection;
                                            }
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

                                            if (listingExist)
                                            {
                                                int index = Globals.DecShopData.Listings.FindIndex(x => x.SmartContractUID == listing.SmartContractUID);
                                                if (index != -1)
                                                    Globals.DecShopData.Listings[index] = listing;
                                            }
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

                                            if (listingExist)
                                            {
                                                int index = Globals.DecShopData.Listings.FindIndex(x => x.SmartContractUID == listing.SmartContractUID);
                                                if (index != -1)
                                                    Globals.DecShopData.Listings[index] = listing;
                                            }
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

                                        if (listingExist)
                                        {
                                            int index = Globals.DecShopData.Listings.FindIndex(x => x.SmartContractUID == listing.SmartContractUID);
                                            if (index != -1)
                                                Globals.DecShopData.Listings[index] = listing;
                                        }
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
                        else if (option == "SpecificAuction")
                        {
                            var auction = JsonConvert.DeserializeObject<Auction>(message.Data);
                            if (auction != null)
                            {
                                if (Globals.DecShopData == null)
                                {
                                    Globals.DecShopData = new DecShopData
                                    {
                                        Auctions = new List<Auction> { auction },
                                    };
                                }
                                else
                                {
                                    if (Globals.DecShopData.Auctions != null)
                                    {
                                        var auctionExist = Globals.DecShopData.Auctions.Exists(x => x.ListingId == auction.ListingId);
                                        if (!auctionExist)
                                            Globals.DecShopData.Auctions.Add(auction);

                                        if (auctionExist)
                                        {
                                            int index = Globals.DecShopData.Auctions.FindIndex(x => x.ListingId == auction.ListingId);
                                            if (index != -1)
                                                Globals.DecShopData.Auctions[index] = auction;
                                        }
                                    }
                                    else
                                    {
                                        Globals.DecShopData.Auctions = new List<Auction> { auction };
                                    }
                                }
                            }


                            msg.HasReceivedResponse = true;
                            msg.MessageResponseReceivedTimestamp = TimeUtil.GetTime();
                            Globals.ClientMessageDict[message.ResponseMessageId] = msg;
                        }
                        else if(option == "Auctions")
                        {
                            var auctions = JsonConvert.DeserializeObject<List<Auction>>(message.Data);
                            if (auctions != null)
                            {
                                if (Globals.DecShopData == null)
                                {
                                    Globals.DecShopData = new DecShopData
                                    {
                                        Auctions = auctions,
                                    };
                                }
                                else
                                {
                                    foreach (var auction in auctions)
                                    {
                                        if (Globals.DecShopData.Auctions != null)
                                        {
                                            var auctionExist = Globals.DecShopData.Auctions.Exists(x => x.ListingId == auction.ListingId);
                                            if (!auctionExist)
                                                Globals.DecShopData.Auctions.Add(auction);

                                            if (auctionExist)
                                            {
                                                int index = Globals.DecShopData.Auctions.FindIndex(x => x.Id == auction.Id);
                                                if (index != -1)
                                                    Globals.DecShopData.Auctions[index] = auction;
                                            }
                                                
                                        }
                                        else
                                        {
                                            Globals.DecShopData.Auctions = new List<Auction> { auction };
                                        }
                                    }
                                }
                            }


                            msg.HasReceivedResponse = true;
                            msg.MessageResponseReceivedTimestamp = TimeUtil.GetTime();
                            Globals.ClientMessageDict[message.ResponseMessageId] = msg;
                        }
                        else if (option == "Bids")
                        {
                            var bids = JsonConvert.DeserializeObject<List<Bid>>(message.Data);
                            if (bids != null)
                            {
                                if (Globals.DecShopData == null)
                                {
                                    Globals.DecShopData = new DecShopData
                                    {
                                        Bids = bids,
                                    };
                                }
                                else
                                {
                                    foreach (var bid in bids)
                                    {
                                        if (Globals.DecShopData.Bids != null)
                                        {
                                            var bidExist = Globals.DecShopData.Bids.Exists(x => x.ListingId == bid.ListingId && x.Id == bid.Id);
                                            if (!bidExist)
                                                Globals.DecShopData.Bids.Add(bid);

                                            if (bidExist)
                                            {
                                                int index = Globals.DecShopData.Bids.FindIndex(x => x.Id == bid.Id);
                                                if (index != -1)
                                                    Globals.DecShopData.Bids[index] = bid;
                                            }
                                        }
                                        else
                                        {
                                            Globals.DecShopData.Bids = new List<Bid> { bid };
                                        }
                                    }
                                }
                            }

                            msg.HasReceivedResponse = true;
                            msg.MessageResponseReceivedTimestamp = TimeUtil.GetTime();
                            Globals.ClientMessageDict[message.ResponseMessageId] = msg;
                        }
                        else if(option == "Update")
                        {
                            try
                            {
                                var uData = JsonConvert.DeserializeObject<DecShopUpdateData>(message.Data);
                                if (uData != null)
                                {
                                    if (Globals.DecShopData != null)
                                    {
                                        if (uData.CollectionList?.Count > 0)
                                        {
                                            if (Globals.DecShopData.Collections?.Count > 0)
                                            {
                                                var oldCollectionList = Globals.DecShopData.Collections.Where(x => !uData.CollectionList.Contains(x.Id)).Select(x => x).ToList();
                                                foreach(var collection in oldCollectionList)
                                                {
                                                    try
                                                    {
                                                        Globals.DecShopData.Collections.Remove(collection);
                                                    }
                                                    catch { }
                                                }

                                                try
                                                {
                                                    var newCollectionList = uData.CollectionList.Where(x => !Globals.DecShopData.Collections.Select(y => y.Id).Contains(x)).Select(x => x).ToList();
                                                    if (newCollectionList.Count > 0)
                                                        DSTClient.NewCollectionsFound = true;
                                                }
                                                catch { }
                                            }
                                            else
                                            {
                                                if (uData.CollectionList.Count > 0)
                                                    DSTClient.NewCollectionsFound = true;
                                                
                                            }
                                        }
                                        if (uData.ListingList?.Count > 0)
                                        {
                                            if (Globals.DecShopData.Listings?.Count > 0)
                                            {
                                                var oldListingList = Globals.DecShopData.Listings.Where(x => !uData.ListingList.Contains(x.Id)).Select(x => x).ToList();
                                                foreach (var listing in oldListingList)
                                                {
                                                    try
                                                    {
                                                        Globals.DecShopData.Listings.Remove(listing);
                                                    }
                                                    catch { }
                                                }

                                                try
                                                {
                                                    var newListingsList = uData.ListingList.Where(x => !Globals.DecShopData.Listings.Select(y => y.Id).Contains(x)).Select(x => x).ToList();
                                                    if (newListingsList.Count > 0)
                                                        DSTClient.NewListingsFound = true;
                                                }
                                                catch { }
                                            }
                                            else
                                            {
                                                if(uData.ListingList.Count > 0)
                                                    DSTClient.NewListingsFound = true;
                                            }
                                        }
                                        if (uData.AuctionList?.Count > 0)
                                        {
                                            if (Globals.DecShopData.Auctions?.Count > 0)
                                            {
                                                var oldAuctionList = Globals.DecShopData.Auctions.Where(x => !uData.AuctionList.Contains(x.Id)).Select(x => x).ToList();
                                                foreach (var auction in oldAuctionList)
                                                {
                                                    try
                                                    {
                                                        Globals.DecShopData.Auctions.Remove(auction);
                                                    }
                                                    catch { }
                                                }

                                                try
                                                {
                                                    var newAuctionList = uData.AuctionList.Where(x => !Globals.DecShopData.Auctions.Select(y => y.Id).Contains(x)).Select(x => x).ToList();
                                                    if (newAuctionList.Count > 0)
                                                        DSTClient.NewAuctionsFound = true;
                                                }
                                                catch { }
                                            }
                                            else
                                            {
                                                if (uData.AuctionList.Count > 0)
                                                    DSTClient.NewAuctionsFound = true;
                                            }
                                        }
                                    }
                                }
                            }
                            catch { }
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
                        var listings = Listing.GetAllListings()?.Where(x => x.IsVisibleBeforeStartDate && !x.IsCancelled && !x.IsAuctionEnded).ToList();
                        if (listings?.Count() > 0)
                        {
                            var pageSkip = page * PaginationAmount;
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
                            var pageSkip = page * PaginationAmount;
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
                    else if (option == "Auctions")
                    {
                        var pageParse = int.TryParse(requestOptArray[1], out var page);
                        if (!pageParse)
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
                        var auctions = Auction.GetAllAuctions()?.Where(x => !x.IsAuctionOver).ToList();
                        if (auctions?.Count() > 0)
                        {
                            var pageSkip = page * PaginationAmount;
                            var auctionsPageApplied = auctions.Skip(pageSkip);

                            var respMessage = new Message
                            {
                                ResponseMessage = true,
                                ResponseMessageId = message.Id,
                                Type = message.Type,
                                ComType = MessageComType.Response,
                                Data = JsonConvert.SerializeObject(auctionsPageApplied)
                            };

                            auctions.Clear();
                            auctions = new List<Auction>();

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
                    else if (option == "SpecificAuction")
                    {
                        var listingIdStr = requestOptArray[1];

                        if (!string.IsNullOrEmpty(listingIdStr))
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

                        var parseAttempt = int.TryParse(listingIdStr, out var listingId);
                        if(!parseAttempt)
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

                        var auction = Auction.GetListingAuction(listingId);
                        if (auction != null)
                        {
                            var respMessage = new Message
                            {
                                ResponseMessage = true,
                                ResponseMessageId = message.Id,
                                Type = message.Type,
                                ComType = MessageComType.Response,
                                Data = JsonConvert.SerializeObject(auction)
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
                    else if (option == "Bids")
                    {
                        var listingIdParse = int.TryParse(requestOptArray[1], out var listingId);
                        if (!listingIdParse)
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

                        var bids = Bid.GetListingBids(listingId);
                        if (bids?.Count() > 0)
                        {
                            var bidList = bids.ToList();
                            var respMessage = new Message
                            {
                                ResponseMessage = true,
                                ResponseMessageId = message.Id,
                                Type = message.Type,
                                ComType = MessageComType.Response,
                                Data = JsonConvert.SerializeObject(bidList)
                            };

                            //bidList.Clear();
                            //bidList = new List<Bid>();

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
                    else if(option == "Update")
                    {
                        var collections = Collection.GetAllCollections()?.ToList();
                        var listings = Listing.GetAllListings()?.Where(x => x.IsVisibleBeforeStartDate && !x.IsCancelled && !x.IsAuctionEnded).ToList();
                        var auctions = Auction.GetAllAuctions()?.Where(x => !x.IsAuctionOver).ToList();

                        List<int>? collectionIds = null;
                        List<int>? listingIds = null;
                        List<int>? auctionsIds = null;

                        if(collections?.Count > 0)
                        {
                            var colIds = collections.Select(x => x.Id);    
                            collectionIds = new List<int>();
                            collectionIds.AddRange(colIds);
                        }

                        if (listings?.Count > 0)
                        {
                            var listIds = listings.Select(x => x.Id);
                            listingIds = new List<int>();
                            listingIds.AddRange(listIds);
                        }

                        if (auctions?.Count > 0)
                        {
                            var aucIds = auctions.Select(x => x.Id);
                            auctionsIds = new List<int>();
                            auctionsIds.AddRange(aucIds);
                        }

                        var decShopUpdateData = new DecShopUpdateData { 
                            AuctionList = auctionsIds,
                            CollectionList = collectionIds,
                            ListingList = listingIds,
                        };


                        var respMessage = new Message
                        {
                            ResponseMessage = true,
                            ResponseMessageId = message.Id,
                            Type = message.Type,
                            ComType = MessageComType.Response,
                            Data = JsonConvert.SerializeObject(decShopUpdateData)
                        };

                        return respMessage;
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
