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
        public static async Task<bool> VerifyTX(Transaction txRequest, bool blockDownloads = false)
        {
            bool txResult = false;

            var accStTrei = StateData.GetAccountStateTrei();
            var from = StateData.GetSpecificAccountStateTrei(txRequest.FromAddress);

            //Balance Check
            if(from == null)
            {
                //They may also just need the block that contains this TX.
                //We might want to queue a block check and download.
                return txResult;
            }
            else
            {
                if(from.Balance < (txRequest.Amount + txRequest.Fee))
                {
                    return txResult;//balance was less than the amount they are trying to send.
                }
            }

            //Timestamp Check
            if(!blockDownloads)
            {
                var currentTime = TimeUtil.GetTime();
                var timeDiff = currentTime - txRequest.Timestamp;
                var minuteDiff = timeDiff / 60M;

                if (minuteDiff > 120.0M)
                {
                    return txResult;
                }
            }

            //Prev Tx in Block Check - this is to prevent someone sending a signed TX again
            var memBlocksTxs = Program.MemBlocks.ToArray().SelectMany(x => x.Transactions).ToArray();
            var txExist = memBlocksTxs.Any(x => x.Hash == txRequest.Hash);
            if (txExist)
            {
                var mempool = TransactionData.GetPool();
                if (mempool.Count() > 0)
                {
                    mempool.DeleteManySafe(x => x.Hash == txRequest.Hash);
                }
                return txResult;
            }

            var checkSize = await VerifyTXSize(txRequest);
            if(checkSize == false)
            {
                return txResult;
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
                        return txResult;
                    }
                }
                
            }

            if(txRequest.TransactionType != TransactionType.TX)
            {
                if(txRequest.TransactionType == TransactionType.NFT_TX || txRequest.TransactionType == TransactionType.NFT_MINT 
                    || txRequest.TransactionType == TransactionType.NFT_BURN)
                {
                    var scDataArray = JsonConvert.DeserializeObject<JArray>(txRequest.Data);
                    var scData = scDataArray[0];

                    var function = (string?)scData["Function"];
                    var scUID = (string?)scData["ContractUID"];

                    if (function != "")
                    {
                        switch (function)
                        {
                            case "Mint()":
                                {
                                    var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
                                    if (scStateTreiRec != null)
                                    {
                                        return txResult;
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
                                            return txResult;
                                        }
                                    }
                                    else
                                    {
                                        return txResult;
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
                                            return txResult;
                                        }
                                    }
                                    else
                                    {
                                        return txResult;
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
                                            return txResult;
                                        }
                                        if(txRequest.ToAddress != scStateTreiRec.OwnerAddress)
                                        {
                                            return txResult;
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
                                            return txResult;
                                        }
                                        if (txRequest.ToAddress != scStateTreiRec.OwnerAddress)
                                        {
                                            return txResult;
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
                                            return txResult;
                                        }
                                        if (txRequest.ToAddress != scStateTreiRec.OwnerAddress)
                                        {
                                            return txResult;
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

                if(txRequest.TransactionType == TransactionType.ADNR)
                {
                    var txData = txRequest.Data;
                    if(txData != null)
                    {
                        try
                        {
                            var jobj = JObject.Parse(txData);

                            var function = (string)jobj["Function"];
                            if (function == "AdnrCreate()")
                            {
                                var name = (string)jobj["Name"];
                                var adnrList = Adnr.GetAdnr();
                                if(adnrList != null)
                                {
                                    var nameCheck = adnrList.FindOne(x => x.Name == name);
                                    if (nameCheck != null)
                                    {
                                        return txResult;
                                    }

                                    var addressCheck = adnrList.FindOne(x => x.Address == txRequest.FromAddress);
                                    if(addressCheck != null)
                                    {
                                        return txResult;
                                    }

                                    if(txRequest.ToAddress != "Adnr_Base")
                                    {
                                        return txResult;
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
                                        return txResult;
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
                                        return txResult;
                                    }

                                    var toAddressCheck = adnrList.FindOne(x => x.Address == txRequest.ToAddress);
                                    if (toAddressCheck != null)
                                    {
                                        return txResult;
                                    }
                                }
                            }
                        }
                        catch(Exception ex)
                        {
                            ErrorLogUtility.LogError("Failed to deserialized TX Data for ADNR", "TransactionValidatorService.VerifyTx()");
                            return txResult;
                        }
                    }
                }

                if(txRequest.TransactionType == TransactionType.DSTR)
                {
                    //PERFORM DSTR HERE
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
                return txResult;
            }

            //Return verification result.
            return txResult;

        }

        public static async Task<(bool, string)> VerifyTXDetailed(Transaction txRequest, bool blockDownloads = false)
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

            //Timestamp Check
            if (!blockDownloads)
            {
                var currentTime = TimeUtil.GetTime();
                var timeDiff = currentTime - txRequest.Timestamp;
                var minuteDiff = timeDiff / 60M;

                if (minuteDiff > 120.0M)
                {
                    return (txResult, "The timestamp of this transactions is too old to be sent now.");
                }
            }

            //Prev Tx in Block Check - this is to prevent someone sending a signed TX again
            var memBlocksTxs = Program.MemBlocks.ToArray().SelectMany(x => x.Transactions).ToArray();
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
                if(txRequest.Amount != 0)
                {
                    var amountCheck = txRequest.Amount % 1 == 0;
                    var amountFormat = 0M;
                    if (amountCheck)
                    {
                        var amountStr = txRequest.Amount.ToString("#");
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
                        return (txResult, "This transactions hash is not equal to the original hash."); 
                    }
                }
                else
                {
                    return (txResult, "This transactions hash is not equal to the original hash.");
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

                    if (function != "")
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

                        }
                        catch (Exception ex)
                        {
                            ErrorLogUtility.LogError("Failed to deserialized TX Data for ADNR", "TransactionValidatorService.VerifyTx()");
                            return (txResult, "Failed to deserialized TX Data for ADNR");
                        }
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
