using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Services
{
    public class TransactionValidatorService
    {
        public static async Task<(bool, string)> VerifyTX(Transaction txRequest, bool blockDownloads = false)
        {
            bool txResult = false;

            var accStTrei = StateData.GetAccountStateTrei();
            var from = StateData.GetSpecificAccountStateTrei(txRequest.FromAddress);

            //Balance Check
            if (from == null)
            {
                //They may also just need the block that contains this TX.
                //We might want to queue a block check and download.
                return (txResult, "This is a new account with no balance.");
            }
            else
            {
                if (from.Balance < (txRequest.Amount + txRequest.Fee))
                {
                    return (txResult, "The balance of this account is less than the amount being sent.");//balance was less than the amount they are trying to send.
                }
            }

            if (txRequest.ToAddress != "Adnr_Base" && txRequest.ToAddress != "DecShop_Base" && txRequest.ToAddress != "Topic_Base" && txRequest.ToAddress != "Vote_Base")
            {
                if (!AddressValidateUtility.ValidateAddress(txRequest.ToAddress))
                    return (txResult, "Address failed to validate");
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
            var memBlocksTxs = Globals.MemBlocks.SelectMany(x => x.Transactions).ToArray();
            var txExist = memBlocksTxs.Any(x => x.Hash == txRequest.Hash);
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
                                        return (txResult, "This smart contract has already been minted."); ;
                                    }

                                    break;
                                }

                            case "Transfer()":
                                {
                                    var toAddress = (string?)scData["ToAddress"];
                                    var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
                                    if (scStateTreiRec != null)
                                    {
                                        if (txRequest.FromAddress != scStateTreiRec.OwnerAddress)
                                        {
                                            return (txResult, "You are attempting to transfer a Smart contract you don't own.");
                                        }
                                    }
                                    else
                                    {
                                        return (txResult, "SC does not exist.");
                                    }

                                    break;
                                }

                            case "Burn()":
                                {
                                    var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
                                    if (scStateTreiRec != null)
                                    {
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
                                    var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
                                    if (scStateTreiRec != null)
                                    {
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
                                    var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
                                    if (scStateTreiRec != null)
                                    {
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
                                    var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
                                    if (scStateTreiRec != null)
                                    {
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

                if (txRequest.TransactionType == TransactionType.ADNR)
                {
                    var txData = txRequest.Data;
                    if (txData != null)
                    {
                        try
                        {
                            var jobj = JObject.Parse(txData);
                            var function = (string)jobj["Function"];

                            if (function == "AdnrCreate()")
                            {
                                var name = (string)jobj["Name"];

                                var adnrList = Adnr.GetAdnr();
                                if (adnrList != null)
                                {
                                    var nameCheck = adnrList.FindOne(x => x.Name == name);
                                    if (nameCheck != null)
                                    {
                                        return (txResult, "Name has already been taken.");
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
                                var adnrList = Adnr.GetAdnr();
                                if (adnrList != null)
                                {
                                    var addressCheck = adnrList.FindOne(x => x.Address == txRequest.FromAddress);
                                    if (addressCheck == null)
                                    {
                                        return (txResult, "Address is not associated with a DNR.");
                                    }
                                }
                            }

                            if (function == "AdnrTransfer()")
                            {
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
                            var jobj = JObject.Parse(txData);
                            if(jobj != null)
                            {
                                var function = (string)jobj["Function"];
                                TopicTrei topic = jobj["Topic"].ToObject<TopicTrei>();//review this to ensure deserialization works.
                                if (function == "TopicAdd()")
                                {
                                    if (topic == null)
                                        return (txResult, "Topic trei record cannot be null.");

                                    var topicSig = topic.TopicOwnerSignature;
                                    if(!string.IsNullOrEmpty(topicSig))
                                    {
                                        var isTopicSigValid = SignatureService.VerifySignature(txRequest.FromAddress, topic.TopicUID, topicSig);
                                        if(isTopicSigValid)
                                        {
                                            if(!blockDownloads)
                                            {
                                                //checks if topic height is within realm of mem blocks
                                                if (!Globals.MemBlocks.Where(x => x.Height == topic.BlockHeight).Any())
                                                {
                                                    return (txResult, "Your topic was not created within the realm of memblocks.");
                                                }

                                                //checks if validator has solved block in past 30 days
                                                var startDate = DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeSeconds();
                                                var validatorList = BlockchainData.GetBlocks().Query().Where(x => x.Timestamp >= startDate).Select(x => x.Validator).ToList().Distinct();
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

                                                if (txRequest.Amount < 1M)
                                                    return (txResult, "There must be at least 1 RBX to create a Topic.");
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
                            var jobj = JObject.Parse(txData);
                            if (jobj != null)
                            {
                                var function = (string)jobj["Function"];
                                Vote vote = jobj["Vote"].ToObject<Vote>();//review this to ensure deserialization works.
                                if (function == "TopicVote()")
                                {
                                    if (vote == null)
                                        return (txResult, "Vote record cannot be null.");

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
            }

            //Signature Check - Final Check to return true.
            var isTxValid = SignatureService.VerifySignature(txRequest.FromAddress, txRequest.Hash, txRequest.Signature);
            if (isTxValid)
            {
                txResult = true;
            }
            else
            {
                return (txResult, "Signature Failed to verify.");
            }

            //Return verification result.
            return (txResult, "Transaction has been verified.");

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
