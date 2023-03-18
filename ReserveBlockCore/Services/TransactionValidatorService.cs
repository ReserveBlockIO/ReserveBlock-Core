using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Utilities;
using Spectre.Console;
using System.Security.Principal;

namespace ReserveBlockCore.Services
{
    public class TransactionValidatorService
    {
        public static async Task<(bool, string)> VerifyTX(Transaction txRequest, bool blockDownloads = false)
        {
            bool txResult = false;
            bool runReserveCheck = true;

            var badTx = Globals.BadTxList.Exists(x => x == txRequest.Hash);
            if (badTx)
                return (true, "");

            var accStTrei = StateData.GetAccountStateTrei();
            var from = StateData.GetSpecificAccountStateTrei(txRequest.FromAddress);

            //Balance Check
            if (from == null)
            {
                return (txResult, "This is a new account with no balance, or your wallet does not have all the blocks in the chain.");
            }
            else
            {
                if (from.Balance < (txRequest.Amount + txRequest.Fee))
                {
                    return (txResult, "The balance of this account is less than the amount being sent.");//balance was less than the amount they are trying to send.
                }
            }

            if (txRequest.Fee <= 0)
            {
                return (txResult, "Fee cannot be less than or equal to zero.");
            }
            
            if (txRequest.ToAddress != "Adnr_Base" && 
                txRequest.ToAddress != "DecShop_Base" && 
                txRequest.ToAddress != "Topic_Base" && 
                txRequest.ToAddress != "Vote_Base" && 
                txRequest.ToAddress != "Reserve_Base")
            {
                if (!AddressValidateUtility.ValidateAddress(txRequest.ToAddress))
                    return (txResult, "To Address failed to validate");
            }

            //Timestamp Check
            if (Globals.BlocksDownloadSlim.CurrentCount != 0)
            {
                var currentTime = TimeUtil.GetTime();
                var timeDiff = currentTime - txRequest.Timestamp;
                var minuteDiff = timeDiff / 60M;

                if (minuteDiff > 60.0M)
                {
                    return (txResult, "The timestamp of this transactions is too old to be sent now.");
                }
            }

            //Prev Tx in Block Check - this is to prevent someone sending a signed TX again            
            var txExist = Globals.MemBlocks.ContainsKey(txRequest.Hash);
            if (txExist)
            {
                var mempool = TransactionData.GetPool();
                if (mempool.Count() > 0)
                {
                    mempool.DeleteManySafe(x => x.Hash == txRequest.Hash);
                }
                return (txResult, "This transactions has already been sent.");
            }

            var checkSize = await VerifyTXSize(txRequest);

            if (checkSize == false)
            {
                return (txResult, $"This transactions is too large. Max size allowed is 30 kb.");
            }

            //Hash Check
            var newTxn = new Transaction()
            {
                Timestamp = txRequest.Timestamp,
                FromAddress = txRequest.FromAddress,
                ToAddress = txRequest.ToAddress,
                Amount = txRequest.Amount,
                Fee = txRequest.Fee,
                Nonce = txRequest.Nonce,
                TransactionType = txRequest.TransactionType,
                Data = txRequest.Data,
                UnlockTime = txRequest.UnlockTime,
            };

            newTxn.Build();

            if (!newTxn.Hash.Equals(txRequest.Hash))
            {
                var amountCheck = txRequest.Amount % 1 == 0;
                var amountFormat = 0M;
                if (amountCheck)
                {
                    var amountStr = txRequest.Amount.ToString("0.0");
                    amountFormat = decimal.Parse(amountStr);
                }

                var newTxnMod = new Transaction()
                {
                    Timestamp = txRequest.Timestamp,
                    FromAddress = txRequest.FromAddress,
                    ToAddress = txRequest.ToAddress,
                    Amount = amountFormat,
                    Fee = txRequest.Fee,
                    Nonce = txRequest.Nonce,
                    TransactionType = txRequest.TransactionType,
                    Data = txRequest.Data,
                    UnlockTime = txRequest.UnlockTime,
                };

                newTxnMod.Build();

                if (!newTxnMod.Hash.Equals(txRequest.Hash))
                {
                    var newTxnModZero = new Transaction()
                    {
                        Timestamp = txRequest.Timestamp,
                        FromAddress = txRequest.FromAddress,
                        ToAddress = txRequest.ToAddress,
                        Amount = 0,
                        Fee = txRequest.Fee,
                        Nonce = txRequest.Nonce,
                        TransactionType = txRequest.TransactionType,
                        Data = txRequest.Data,
                        UnlockTime = txRequest.UnlockTime,
                    };

                    newTxnModZero.Build();

                    if (!newTxnModZero.Hash.Equals(txRequest.Hash))
                    {
                        return (txResult, "This transactions hash is not equal to the original hash.");
                    }
                }

            }

            if (txRequest.TransactionType != TransactionType.TX)
            {
                if (txRequest.TransactionType == TransactionType.NFT_TX || txRequest.TransactionType == TransactionType.NFT_MINT
                    || txRequest.TransactionType == TransactionType.NFT_BURN)
                {
                    try
                    {
                        var txData = txRequest.Data;
                        if(txData != null)
                        {
                            var scDataArray = JsonConvert.DeserializeObject<JArray>(txRequest.Data);
                            var scData = scDataArray[0];

                            var function = (string?)scData["Function"];
                            var scUID = (string?)scData["ContractUID"];

                            if (!string.IsNullOrWhiteSpace(function))
                            {
                                switch (function)
                                {
                                    case "Mint()":
                                        {
                                            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
                                            if (scStateTreiRec != null)
                                            {
                                                return (txResult, "This smart contract has already been minted.");
                                            }
                                            if(txRequest.FromAddress.StartsWith("xRBX"))
                                                return (txResult, "A reserve account may not mint a smart contract.");
                                            break;
                                        }

                                    case "Transfer()":
                                        {
                                            var toAddress = (string?)scData["ToAddress"];
                                            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
                                            if (scStateTreiRec != null)
                                            {
                                                if (txRequest.FromAddress != scStateTreiRec.OwnerAddress)
                                                    return (txResult, "You are attempting to transfer a Smart contract you don't own.");
                                                
                                                if(scStateTreiRec.IsLocked)
                                                    return (txResult, "You are attempting to transfer a Smart contract that is locked.");
                                                
                                                if(scStateTreiRec.NextOwner != null)
                                                    return (txResult, "You are attempting to transfer a Smart contract that has a new owner assigned to it.");
                                            }
                                            else
                                            {
                                                return (txResult, "SC does not exist.");
                                            }

                                            break;
                                        }

                                    case "Burn()":
                                        {
                                            if (txRequest.FromAddress.StartsWith("xRBX"))
                                                return (txResult, "A reserve account may not burn a smart contract.");

                                            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
                                            if (scStateTreiRec != null)
                                            {
                                                if (scStateTreiRec.IsLocked)
                                                    return (txResult, "You are attempting to burn a Smart contract that is locked.");

                                                if (txRequest.FromAddress != scStateTreiRec.OwnerAddress)
                                                {
                                                    return (txResult, "You are attempting to burn a Smart contract you don't own.");
                                                }
                                            }
                                            else
                                            {
                                                return (txResult, "SC does not exist.");
                                            }

                                            break;
                                        }
                                    case "Evolve()":
                                        {
                                            if (txRequest.FromAddress.StartsWith("xRBX"))
                                                return (txResult, "A reserve account may not evolve a smart contract.");

                                            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
                                            if (scStateTreiRec != null)
                                            {
                                                if (scStateTreiRec.IsLocked)
                                                    return (txResult, "You are attempting to evolve a Smart contract that is locked.");

                                                if (txRequest.FromAddress != scStateTreiRec.MinterAddress)
                                                {
                                                    return (txResult, "You are attempting to evolve a Smart contract you don't own.");
                                                }
                                                if (txRequest.ToAddress != scStateTreiRec.OwnerAddress)
                                                {
                                                    return (txResult, "You are attempting to evolve a Smart contract you don't own.");
                                                }
                                            }
                                            //Run the Trillium REPL To ensure new state is valid again.
                                            break;
                                        }
                                    case "Devolve()":
                                        {
                                            if (txRequest.FromAddress.StartsWith("xRBX"))
                                                return (txResult, "A reserve account may not devolve a smart contract.");

                                            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
                                            if (scStateTreiRec != null)
                                            {
                                                if (scStateTreiRec.IsLocked)
                                                    return (txResult, "You are attempting to devolve a Smart contract that is locked.");

                                                if (txRequest.FromAddress != scStateTreiRec.MinterAddress)
                                                {
                                                    return (txResult, "You are attempting to devolve a Smart contract you don't own.");
                                                }
                                                if (txRequest.ToAddress != scStateTreiRec.OwnerAddress)
                                                {
                                                    return (txResult, "You are attempting to devolve a Smart contract you don't own.");
                                                }
                                            }
                                            //Run the Trillium REPL To ensure new state is valid again.
                                            break;
                                        }
                                    case "ChangeEvolveStateSpecific()":
                                        {
                                            if (txRequest.FromAddress.StartsWith("xRBX"))
                                                return (txResult, "A reserve account may not evolve/devolve a smart contract.");

                                            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
                                            if (scStateTreiRec != null)
                                            {
                                                if (scStateTreiRec.IsLocked)
                                                    return (txResult, "You are attempting to evolve/devolve a Smart contract that is locked.");

                                                if (txRequest.FromAddress != scStateTreiRec.MinterAddress)
                                                {
                                                    return (txResult, "You are attempting to devolve/evolve a Smart contract you don't own.");
                                                }
                                                if (txRequest.ToAddress != scStateTreiRec.OwnerAddress)
                                                {
                                                    return (txResult, "You are attempting to devolve/evolve a Smart contract you don't own.");
                                                }
                                            }
                                            //Run the Trillium REPL To ensure new state is valid again.
                                            break;
                                        }

                                    default:
                                        break;
                                }
                            }
                        }
                        else
                        {
                            return (txResult, $"TX Data cannot be null for transaction type: {txRequest.TransactionType}");
                        }
                    }
                    catch { return (txResult, $"TX Could not be parsed. TX Hash: {txRequest.Hash}"); }
                    
                }

                if (txRequest.TransactionType == TransactionType.ADNR)
                {
                    var txData = txRequest.Data;
                    var badAdnrTx = Globals.BadADNRTxList.Exists(x => x == txRequest.Hash);
                    if (txData != null && !badAdnrTx)
                    {
                        try
                        {
                            var jobj = JObject.Parse(txData);
                            var function = (string)jobj["Function"];

                            if (function == "AdnrCreate()")
                            {
                                if (txRequest.FromAddress.StartsWith("xRBX"))
                                    return (txResult, "A reserve account may not create an ADNR.");

                                var name = (string)jobj["Name"];

                                var adnrList = Adnr.GetAdnr();
                                if (adnrList != null)
                                {
                                    if(!string.IsNullOrEmpty(name))
                                    {
                                        var nameRBX = name.ToLower() + ".rbx";
                                        var nameCheck = adnrList.FindOne(x => x.Name == name || x.Name == nameRBX);
                                        if (nameCheck != null)
                                        {
                                            return (txResult, "Name has already been taken.");
                                        }
                                    }
                                    
                                    var addressCheck = adnrList.FindOne(x => x.Address == txRequest.FromAddress);
                                    if (addressCheck != null)
                                    {
                                        return (txResult, "Address is already associated with an active DNR");
                                    }

                                    if (txRequest.ToAddress != "Adnr_Base")
                                    {
                                        return (txResult, "To Address was not the Adnr_Base.");
                                    }
                                }
                            }

                            if (function == "AdnrDelete()")
                            {
                                if (txRequest.FromAddress.StartsWith("xRBX"))
                                    return (txResult, "A reserve account may not delete an ADNR.");

                                var adnrList = Adnr.GetAdnr();
                                if (adnrList != null)
                                {
                                    var addressCheck = adnrList.FindOne(x => x.Address == txRequest.FromAddress);
                                    if (addressCheck == null)
                                    {
                                        return (txResult, "Address is not associated with a DNR.");
                                    }

                                    if (txRequest.ToAddress != "Adnr_Base")
                                    {
                                        return (txResult, "To Address was not the Adnr_Base.");
                                    }
                                }
                            }

                            if (function == "AdnrTransfer()")
                            {
                                if (txRequest.FromAddress.StartsWith("xRBX"))
                                    return (txResult, "A reserve account may not transfer an ADNR.");

                                var adnrList = Adnr.GetAdnr();
                                if (adnrList != null)
                                {
                                    var addressCheck = adnrList.FindOne(x => x.Address == txRequest.FromAddress);
                                    if (addressCheck == null)
                                    {
                                        return (txResult, "Address is not associated with a DNR.");
                                    }

                                    var toAddressCheck = adnrList.FindOne(x => x.Address == txRequest.ToAddress);
                                    if (toAddressCheck != null)
                                    {
                                        return (txResult, "To Address is already associated with a DNR.");
                                    }
                                }
                            }

                            if (txRequest.Amount < 1M)
                                return (txResult, "There must be at least 1 RBX to perform an ADNR Function.");

                        }
                        catch (Exception ex)
                        {                            
                            ErrorLogUtility.LogError("Failed to deserialized TX Data for ADNR", "TransactionValidatorService.VerifyTx()");
                            return (txResult, "Failed to deserialized TX Data for ADNR");
                        }
                    }
                }

                if(txRequest.TransactionType == TransactionType.VOTE_TOPIC)
                {
                    var txData = txRequest.Data;
                    if(txData != null)
                    {
                        try
                        {
                            if (txRequest.FromAddress.StartsWith("xRBX"))
                                return (txResult, "A reserve account may not performing voting actions.");

                            var jobj = JObject.Parse(txData);
                            if(jobj != null)
                            {
                                var function = (string)jobj["Function"];
                                TopicTrei topic = jobj["Topic"].ToObject<TopicTrei>();//review this to ensure deserialization works.
                                if (function == "TopicAdd()")
                                {
                                    if (topic == null)
                                        return (txResult, "Topic trei record cannot be null.");

                                    if(txRequest.ToAddress != "Topic_Base")
                                        return (txResult, "To Address must be Topic_Base.");

                                    if (txRequest.Amount < 1M)
                                        return (txResult, "There must be at least 1 RBX to create a Topic.");

                                    var topicSig = topic.TopicOwnerSignature;
                                    if(!string.IsNullOrEmpty(topicSig))
                                    {
                                        var isTopicSigValid = SignatureService.VerifySignature(txRequest.FromAddress, topic.TopicUID, topicSig);
                                        if(isTopicSigValid)
                                        {
                                            if(!blockDownloads)
                                            {
                                                //checks if topic height is within realm of mem blocks
                                                if (!Globals.MemBlocks.Values.Where(x => x == topic.BlockHeight).Any())
                                                {
                                                    return (txResult, "Your topic was not created within the realm of memblocks.");
                                                }

                                                //checks if validator has solved block in past 30 days
                                                var startDate = DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeSeconds();
                                                var validatorList = BlockchainData.GetBlocks().Query().Where(x => x.Timestamp >= startDate).Select(x => x.Validator).ToEnumerable().Distinct();
                                                var valExist = validatorList.Where(x => x == txRequest.FromAddress).Any();
                                                if (!valExist)
                                                    return (txResult, "Validator has not crafted a block. Please wait til you craft a block to create a topic.");

                                                if (topic.VoterType == TopicVoterType.Validator)
                                                {
                                                    var stAcct = StateData.GetSpecificAccountStateTrei(txRequest.FromAddress);
                                                    if (stAcct != null)
                                                    {
                                                        var balance = (stAcct.Balance - (txRequest.Amount + txRequest.Fee));
                                                        if (balance < 1000)
                                                        {
                                                            return (txResult, "Balance is under 1000. Topic will not be allowed.");
                                                        }
                                                    }
                                                    else
                                                    {
                                                        return (txResult, "Could not locate account in state trei.");
                                                    }
                                                }
                                                if (topic.VoterType == TopicVoterType.Adjudicator)
                                                {
                                                    var adjs = Globals.AdjNodes.Values.ToList();
                                                    var isAdj = adjs.Exists(x => x.Address == txRequest.FromAddress);
                                                    if (!isAdj)
                                                    {
                                                        return (txResult, $"The from addesss ({txRequest.FromAddress}) is not in the adjudicator pool.");
                                                    }
                                                }

                                                var activeTopics = TopicTrei.GetSpecificTopicByAddress(txRequest.FromAddress, true);
                                                if (activeTopics != null)
                                                    return (txResult, "Only one active topic per address is allowed.");

                                                
                                                if(topic.VoteTopicCategory == VoteTopicCategories.AdjVoteIn)
                                                {
                                                    try
                                                    {
                                                        var adjVoteReq = JsonConvert.DeserializeObject<AdjVoteInReqs>(topic.TopicDescription);
                                                        if (adjVoteReq != null)
                                                        {
                                                            var adjVoteReqResult = VoteValidatorService.ValidateAdjVoteIn(adjVoteReq);
                                                            if(!adjVoteReqResult)
                                                            {
                                                                return (txResult, "You did not meet the required specs or information was not completed. This topic has been cancelled.");
                                                            }
                                                        }
                                                        else
                                                        {
                                                            return (txResult, "Topic description was missing the Adj Vote in Requirements.");
                                                        }

                                                        var topicSize = topic.TopicDescription.Length + topic.TopicName.Length;
                                                        if(topicSize > 2800)
                                                        {
                                                            return (txResult, "Topic is larger than the 2800 limit.");
                                                        }
                                                    }
                                                    catch
                                                    {

                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            return (txResult, "Topic Signature was not valid.");
                                        }
                                    }
                                    else
                                    {
                                        return (txResult, "Topic missing signature. A signature is required to send a voting topic.");
                                    }
                                }
                            }
                        }
                        catch(Exception ex)
                        {                            
                            ErrorLogUtility.LogError("Failed to deserialized TX Data for Topic", "TransactionValidatorService.VerifyTx()");
                            return (txResult, "Failed to deserialized TX Data for Topic");
                        }
                    }
                    else
                    {
                        return (txResult, "TX Data cannot be null on a vote Topic.");
                    }
                    
                }

                if (txRequest.TransactionType == TransactionType.VOTE)
                {
                    var txData = txRequest.Data;
                    if (txData != null)
                    {
                        try
                        {
                            if (txRequest.FromAddress.StartsWith("xRBX"))
                                return (txResult, "A reserve account may not performing voting actions.");

                            var jobj = JObject.Parse(txData);
                            if (jobj != null)
                            {
                                var function = (string)jobj["Function"];
                                Vote vote = jobj["Vote"].ToObject<Vote>();//review this to ensure deserialization works.
                                if (function == "TopicVote()")
                                {
                                    if (vote == null)
                                        return (txResult, "Vote record cannot be null.");

                                    if (txRequest.ToAddress != "Vote_Base")
                                        return (txResult, "To Address must be Vote_Base.");

                                    var topic = TopicTrei.GetSpecificTopic(vote.TopicUID);
                                    if(topic == null)
                                        return (txResult, "Topic does not exist.");

                                    var currentTime = DateTime.UtcNow;
                                    if(currentTime > topic.VotingEndDate)
                                        return (txResult, "Voting for this topic has ended.");

                                    //from address must equal vote address
                                    //validator address must equal vote address
                                    if (txRequest.FromAddress != vote.Address)
                                        return (txResult, "Vote address must match the transactions From Address.");

                                    var voteExixt = Vote.CheckSpecificAddressVoteOnTopic(vote.Address, vote.TopicUID);

                                    if(voteExixt)
                                        return (txResult, "You have already voted on this topic and may not do so again.");

                                    if(Globals.BlocksDownloadSlim.CurrentCount != 0)
                                    {
                                        var stAcct = StateData.GetSpecificAccountStateTrei(txRequest.FromAddress);
                                        if (stAcct != null)
                                        {
                                            var balance = (stAcct.Balance - (txRequest.Amount + txRequest.Fee));
                                            if (balance < 1000)
                                            {
                                                return (txResult, "Balance is under 1000. Vote will not be allowed.");
                                            }
                                        }
                                        else
                                        {
                                            return (txResult, "Could not locate account in state trei.");
                                        }
                                    }
                                }
                            }

                        }
                        catch (Exception ex)
                        {                            
                            ErrorLogUtility.LogError("Failed to deserialized TX Data for Topic", "TransactionValidatorService.VerifyTx()");
                            return (txResult, "Failed to deserialized TX Data for Topic");
                        }
                    }
                    else
                    {
                        return (txResult, "TX Data cannot be null on a vote Topic.");
                    }

                }
                if (txRequest.TransactionType == TransactionType.DSTR)
                {
                    var badDSTTx = Globals.BadDSTList.Exists(x => x == txRequest.Hash);
                    var txData = txRequest.Data;
                    if (txData != null && !badDSTTx)
                    {
                        try
                        {
                            if (txRequest.FromAddress.StartsWith("xRBX"))
                                return (txResult, "A reserve account may not performing DST actions.");

                            if (txRequest.ToAddress != "DecShop_Base")
                                return (txResult, "To Address must be DecShop_Base.");

                            var jobj = JObject.Parse(txData);
                            if (jobj != null)
                            {
                                var function = (string?)jobj["Function"];
                                if (function == "DecShopDelete()")
                                {
                                    if (txRequest.Amount < 1M)
                                        return (txResult, "There must be at least 1 RBX to create a Auction House.");

                                    string dsUID = jobj["UniqueId"].ToObject<string>();
                                    if(!string.IsNullOrEmpty(dsUID))
                                    {
                                        //ensure they own the shop
                                        var treiRec = DecShop.GetDecShopStateTreiLeaf(dsUID);
                                        if (treiRec != null)
                                        {
                                            if (treiRec.OwnerAddress != txRequest.FromAddress)
                                                return (txResult, "You must be the valid owner of this shop.");
                                        }
                                        else
                                        {
                                            return (txResult, "No record found to delete.");
                                        }
                                    }
                                }
                                else
                                {
                                    DecShop decshop = jobj["DecShop"].ToObject<DecShop>();

                                    if (decshop == null)
                                        return (txResult, "DecShop record cannot be null.");

                                    var result = DecShop.CheckURL(decshop.DecShopURL);
                                    if (!result)
                                        return (txResult, "URL does not meet requirements.");

                                    var wordCount = decshop.Description.ToWordCountCheck(200);
                                    var descLength = decshop.Description.ToLengthCheck(1200);
                                    var nameLength = decshop.Name.ToLengthCheck(64);

                                    if (!wordCount || !descLength)
                                        return (txResult, $"Failed to insert/update. Description Word Count Allowed: {200}. Description length allowed: {1200}");

                                    if (!nameLength)
                                        return (txResult, $"Failed to insert/update. Name length allowed: {64}");

                                    if (function == "DecShopCreate()")
                                    {
                                        if (txRequest.Amount < 1M)
                                            return (txResult, "There must be at least 1 RBX to create a Auction House.");

                                        var urlValid = DecShop.ValidStateTreiURL(decshop.DecShopURL);
                                        if (!urlValid)
                                            return (txResult, "The URL in this TX has already been used. URLs must be unique.");
                                        var recExist = DecShop.GetDecShopStateTreiLeaf(decshop.UniqueId);
                                        if (recExist != null)
                                            return (txResult, "This record has already been inserted to trei. Rejecting.");
                                    }
                                    if (function == "DecShopUpdate()")
                                    {
                                        //ensure they own the shop
                                        var treiRec = DecShop.GetDecShopStateTreiLeaf(decshop.UniqueId);
                                        if (treiRec != null)
                                        {
                                            //86400 seconds in a day
                                            var currentTime = TimeUtil.GetTime();
                                            var lastUpdateTime = currentTime - treiRec.UpdateTimestamp;

                                            if(lastUpdateTime < 43200)
                                            {
                                                if (txRequest.Amount < 1M)
                                                    return (txResult, "There must be at least 1 RBX to Update an Auction House more than 1 time in 12 hours.");
                                            }
                                            if (decshop.DecShopURL.ToLower() != treiRec.DecShopURL.ToLower())
                                            {
                                                var urlValid = DecShop.ValidStateTreiURL(decshop.DecShopURL);
                                                if (!urlValid)
                                                    return (txResult, "The URL in this TX has already been used. URLs must be unique.");
                                            }

                                            if (treiRec.OwnerAddress != txRequest.FromAddress)
                                                return (txResult, "You must be the valid owner of this shop.");
                                        }
                                        else
                                        {
                                            return (txResult, "No record found to update.");
                                        }
                                    }
                                }
                            }
                        }
                        catch { return (txResult, $"TX not formatted properly."); }
                    }
                }

                if(txRequest.TransactionType == TransactionType.RESERVE)
                {
                    var txData = txRequest.Data;
                    if (txData != null)
                    {
                        try
                        {
                            if (txRequest.ToAddress != "Reserve_Base")
                                return (txResult, "To Address must be Reserve_Base.");

                            if (!txRequest.FromAddress.StartsWith("xRBX"))
                                return (txResult, "Not a valid Reserve Account address. Must start with 'xRBX'");

                            var jobj = JObject.Parse(txData);
                            if (jobj != null)
                            {
                                var function = (string?)jobj["Function"];
                                if(function != null)
                                {
                                    if (function == "Register()")
                                    {
                                        runReserveCheck = false;

                                        if (txRequest.Amount < 4M)
                                            return (txResult, "There must be at least 4 RBX to register a Reserve Account on network.");

                                        string reserveAddress = txRequest.FromAddress;
                                        string recoveryAddress = jobj["RecoveryAddress"].ToObject<string>();
                                        if(!string.IsNullOrEmpty(reserveAddress) && !string.IsNullOrEmpty(recoveryAddress))
                                        {
                                            var stateRec = StateData.GetSpecificAccountStateTrei(reserveAddress);
                                            if(stateRec != null)
                                            {
                                                if (stateRec.RecoveryAccount != null)
                                                    return (txResult, $"Address already has a recovery account: {stateRec.RecoveryAccount}");

                                                if (stateRec.Balance - (txRequest.Amount + txRequest.Fee) < 0.5M)
                                                    return (txResult, "This transaction will make the balance too low. Must maintain a balance above 0.5 RBX with a Reserve Account.");
                                            }
                                            else
                                            {
                                                return (txResult, "Could not find a state trei leaf record.");
                                            }
                                        }
                                        else
                                        {
                                            return (txResult, "Could not find a proper reserve address and recovery address");
                                        }
                                    }

                                    if(function == "CallBack()")
                                    {
                                        runReserveCheck = false;
                                        string hash = jobj["Hash"].ToObject<string>();
                                        if(!string.IsNullOrEmpty(hash))
                                        {
                                            var currentTime = TimeUtil.GetTime();
                                            var rTx = ReserveTransactions.GetTransactions(hash);
                                            if(rTx == null)
                                                return (txResult, "Could not find a reserve transaction with that hash.");

                                            if (rTx.Transaction.FromAddress != txRequest.FromAddress)
                                                return (txResult, "From address does not match the reserve tx from address. Cannot call back.");

                                            if (Globals.BlocksDownloadSlim.CurrentCount != 0)
                                            {
                                                if (rTx.ConfirmTimestamp <= currentTime)
                                                    return (txResult, "This TX has already passed and can no longer be called back.");
                                            }
                                        }
                                    }

                                    if (function == "Recover()")
                                    {
                                        runReserveCheck = false;
                                        string hash = jobj["Hash"].ToObject<string>();
                                        if (!string.IsNullOrEmpty(hash))
                                        {
                                            var currentTime = TimeUtil.GetTime();
                                            var rTx = ReserveTransactions.GetTransactions(hash);
                                            if (rTx == null)
                                                return (txResult, "Could not find a reserve transaction with that hash.");

                                            if (rTx.Transaction.FromAddress != txRequest.FromAddress)
                                                return (txResult, "From address does not match the reserve tx from address. Cannot recover.");

                                            if (Globals.BlocksDownloadSlim.CurrentCount != 0)
                                            {
                                                if (rTx.ConfirmTimestamp <= currentTime)
                                                    return (txResult, "This TX has already passed and can no longer be recovered.");
                                            }
                                            var stateRec = StateData.GetSpecificAccountStateTrei(rTx.FromAddress);

                                            if (stateRec == null) 
                                                return (txResult, "State record cannot be null.");
                                            
                                            if (stateRec.RecoveryAccount == null)
                                                return (txResult, $"Reserve account does not have a recovery address.");
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }

                }
            }

            if (txRequest.FromAddress.StartsWith("xRBX") && runReserveCheck)
            {
                if (txRequest.TransactionType != TransactionType.TX && txRequest.TransactionType != TransactionType.RESERVE && txRequest.TransactionType != TransactionType.NFT_TX)
                    return (txResult, "Invalid Transaction Type was selected.");

                var balanceTooLow = from.Balance - (txRequest.Fee + txRequest.Amount) < 0.5M ? true : false;
                if (balanceTooLow)
                    return (txResult, "This transaction will make the balance too low. Must maintain a balance above 0.5 RBX with a Reserve Account.");

                if(txRequest.UnlockTime == null)
                    return (txResult, "There must be an unlock time for this transaction");

                if (Globals.BlocksDownloadSlim.CurrentCount != 0)
                {
                    var validUnlockTime = TimeUtil.GetReserveTime(-3);

                    if (txRequest.UnlockTime.Value < validUnlockTime)
                        return (txResult, "Unlock time does not meet 24 hour requirement.");
                }
            }

            //Signature Check - Final Check to return true.
            if (!string.IsNullOrEmpty(txRequest.Signature))
            {
                var isTxValid = SignatureService.VerifySignature(txRequest.FromAddress, txRequest.Hash, txRequest.Signature);
                if (isTxValid)
                {
                    txResult = true;
                }
                else
                {
                    return (txResult, "Signature Failed to verify.");
                }
            }
            else
            {
                return (txResult, "Signature cannot be null.");
            }
            
            //Return verification result.
            return (txResult, "Transaction has been verified.");

        }

        public static async Task BadTXDetected(Transaction Tx)
        {
            Console.WriteLine("A Transaction has failed validation. Would you like to ignore this transaction to move on?");
            AnsiConsole.MarkupLine($"[green]'y'[/] for [green]yes[/] and [red]'n'[/] for [red]no[/]");
            AnsiConsole.MarkupLine($"[yellow]Please note you may need to type 'y' and press enter twice.[/]");

            var answer = Console.ReadLine();

            if(!string.IsNullOrEmpty(answer)) 
            {
                if(answer.ToLower() == "y")
                {
                    var badTx = new BadTransaction {FromAddress = Tx.FromAddress, Hash = Tx.Hash, TransactionType = Tx.TransactionType };
                    var result = BadTransaction.SaveBadTransaction(badTx);
                    if(result)
                    {
                        AnsiConsole.MarkupLine($"[green]Bad Transaction has been added.[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]Failed to add bad transaction.[/]");
                    }
                }
            }

        }

        public static async Task<bool> VerifyTXSize(Transaction txRequest)
        {
            var txJsonSize = JsonConvert.SerializeObject(txRequest);
            var size = txJsonSize.Length;

            if (size > (1024 * 30))
            {
                return false;
            }

            return true;
        }

    }
}
