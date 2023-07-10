using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.DST;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Utilities;
using Spectre.Console;
using System;
using System.Net;
using System.Security.Principal;

namespace ReserveBlockCore.Services
{
    public class TransactionValidatorService
    {
        public static async Task<(bool, string)> VerifyTX(Transaction txRequest, bool blockDownloads = false, bool blockVerify = false)
        {
            bool txResult = false;
            bool runReserveCheck = true;

            var badTx = Globals.BadTxList.Exists(x => x == txRequest.Hash);
            if (badTx)
                return (true, "");

            var badNFTTx = Globals.BadNFTTxList.Exists(x => x == txRequest.Hash);
            if (badNFTTx) 
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

            if(Globals.LastBlock.Height > Globals.TXHeightRule2) //around April 7, 2023 at 18:30 UTC
            {
                if (txRequest.Fee <= 0.000003M)
                {
                    return (txResult, "Fee cannot be less than 0.000003 RBX");
                }
            }

            if(!Globals.IsTestNet)
            {
                if (txRequest.ToAddress.StartsWith("xRBX") && txRequest.FromAddress.StartsWith("xRBX"))
                    return (txResult, "Reserve accounts cannot send to another Reserve Account.");
            }

            //REMOVE AFTER ENABLED!
            //if (txRequest.ToAddress.StartsWith("xRBX"))
            //    return (txResult, "Reserve accounts are not unlocked and you may not send transactions to them yet.");

            //if (txRequest.ToAddress != "Adnr_Base" && txRequest.ToAddress != "DecShop_Base" && txRequest.ToAddress != "Topic_Base" && txRequest.ToAddress != "Vote_Base")

            if (txRequest.ToAddress != "Adnr_Base" && 
                txRequest.ToAddress != "DecShop_Base" && 
                txRequest.ToAddress != "Topic_Base" && 
                txRequest.ToAddress != "Vote_Base" && 
                txRequest.ToAddress != "Reserve_Base" &&
                txRequest.ToAddress != "Token_Base")
            {
                if (!AddressValidateUtility.ValidateAddress(txRequest.ToAddress))
                    return (txResult, "To Address failed to validate");

                if(txRequest.ToAddress.Length < 32)
                    return (txResult, "Address length is too short.");
            }

            if (Globals.LastBlock.Height > Globals.TXHeightRule1) //March 31th, 2023 at 03:44 UTC
            {
                if (txRequest.Amount < 0.0M)
                {
                    return (txResult, "Amount cannot be less than or equal to zero.");
                }
            }

            //Timestamp Check
            if (Globals.BlocksDownloadSlim.CurrentCount != 0 && Globals.BlocksDownloadV2Slim.CurrentCount != 0)
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
                                                return (txResult, "This smart contract has already been minted.");
                                            
                                            if(txRequest.FromAddress.StartsWith("xRBX"))
                                                return (txResult, "A reserve account may not mint a smart contract.");
                                            break;
                                        }

                                    case "TokenDeploy()":
                                        {
                                            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
                                            if (scStateTreiRec != null)
                                                return (txResult, "This smart contract has already been minted.");
                                            
                                            if (txRequest.FromAddress.StartsWith("xRBX"))
                                                return (txResult, "A reserve account may not mint a smart contract.");

                                            break;
                                        }

                                    case "TokenMint()":
                                        {
                                            var jobj = JObject.Parse(txData);
                                            var fromAddress = jobj["FromAddress"]?.ToObject<string?>();
                                            var amount = jobj["Amount"]?.ToObject<decimal?>();
                                            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);

                                            if (scStateTreiRec == null)
                                                return (txResult, "Could not find smart contract at state level.");

                                            if(fromAddress == null || amount == null)
                                                return (txResult, "Missing TX Data Fields.");

                                            if (txRequest.FromAddress != fromAddress)
                                                return (txResult, "From Addresses Do not match.");

                                            if (scStateTreiRec.TokenDetails == null)
                                                return (txResult, "Token details for this SC are null.");

                                            if(scStateTreiRec.TokenDetails.StartingSupply > 0.0M)
                                                return (txResult, "Token supply was not set to infinite.");

                                            if (scStateTreiRec.TokenDetails.ContractOwner != txRequest.FromAddress)
                                                return (txResult, "TX From address is not the owner of this Token SC.");

                                            if(amount.Value < 1.0M)
                                                return (txResult, "You must mint at least 1 token.");

                                            if (txRequest.ToAddress != "Token_Base")
                                                return (txResult, "To Address must be 'Token_Base'.");

                                            break;
                                        }

                                    case "TokenPause()":
                                        {
                                            var jobj = JObject.Parse(txData);
                                            var fromAddress = jobj["FromAddress"]?.ToObject<string?>();
                                            var pause = jobj["Pause"]?.ToObject<bool?>();

                                            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);

                                            if (scStateTreiRec == null)
                                                return (txResult, "Could not find smart contract at state level.");

                                            if(txRequest.FromAddress != fromAddress)
                                                return (txResult, "From Addresses Do not match.");

                                            if(scStateTreiRec.TokenDetails == null)
                                                return (txResult, "Token details for this SC are null.");

                                            if(scStateTreiRec.TokenDetails.ContractOwner != txRequest.FromAddress)
                                                return (txResult, "TX From address is not the owner of this Token SC.");

                                            if (txRequest.ToAddress != "Token_Base")
                                                return (txResult, "To Address must be 'Token_Base'.");

                                            break;
                                        }

                                    case "TokenContractOwnerChange()":
                                        {
                                            var jobj = JObject.Parse(txData);
                                            var fromAddress = jobj["FromAddress"]?.ToObject<string?>();
                                            var toAddress = jobj["ToAddress"]?.ToObject<string?>();

                                            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);

                                            if (scStateTreiRec == null)
                                                return (txResult, "Could not find smart contract at state level.");

                                            if (txRequest.FromAddress != fromAddress)
                                                return (txResult, "From Addresses Do not match.");

                                            if (scStateTreiRec.TokenDetails == null)
                                                return (txResult, "Token details for this SC are null.");

                                            if (scStateTreiRec.TokenDetails.ContractOwner != txRequest.FromAddress)
                                                return (txResult, "TX From address is not the owner of this Token SC.");

                                            if (scStateTreiRec.TokenDetails.IsPaused)
                                                return (txResult, "Contract is paused. NO TXs may go through.");

                                            break;
                                        }

                                    case "TokenBanAddress()" :
                                        {
                                            var jobj = JObject.Parse(txData);
                                            var fromAddress = jobj["FromAddress"]?.ToObject<string?>();
                                            var banAddress = jobj["BanAddress"]?.ToObject<string?>();

                                            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);

                                            if (scStateTreiRec == null)
                                                return (txResult, "Could not find smart contract at state level.");

                                            if (txRequest.FromAddress != fromAddress)
                                                return (txResult, "From Addresses Do not match.");

                                            if (scStateTreiRec.TokenDetails == null)
                                                return (txResult, "Token details for this SC are null.");

                                            if (scStateTreiRec.TokenDetails.ContractOwner != txRequest.FromAddress)
                                                return (txResult, "TX From address is not the owner of this Token SC.");

                                            if (scStateTreiRec.TokenDetails.IsPaused)
                                                return (txResult, "Contract is paused. NO TXs may go through.");

                                            if (txRequest.ToAddress != "Token_Base")
                                                return (txResult, "To Address must be 'Token_Base'.");

                                            break;
                                        }

                                    case "TokenTransfer()" :
                                        {
                                            var jobj = JObject.Parse(txData);

                                            var fromAddress = jobj["FromAddress"]?.ToObject<string?>();
                                            var toAddress = jobj["ToAddress"]?.ToObject<string?>();
                                            var amount = jobj["Amount"]?.ToObject<decimal?>();

                                            if(amount == null || toAddress == null || fromAddress == null)
                                                return (txResult, $"TX Data was missing items.");

                                            var stateAccount = StateData.GetSpecificAccountStateTrei(fromAddress);
                                            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);

                                            if (scStateTreiRec == null)
                                                return (txResult, "Could not find smart contract at state level.");

                                            if (stateAccount == null)
                                                return (txResult, "Could not find account at state level.");

                                            var tokenDetails = scStateTreiRec.TokenDetails;

                                            if(tokenDetails == null)
                                                return (txResult, "Could not find token details for contract at state level.");

                                            if(tokenDetails.IsPaused)
                                                return (txResult, "Contract is paused. NO TXs may go through.");

                                            if(tokenDetails.AddressBlackList?.Count > 0)
                                            {
                                                if(tokenDetails.AddressBlackList.Exists(x => x == txRequest.FromAddress))
                                                    return (txResult, "This address has been blacklisted and may no longer perform transfers.");
                                            }

                                            var tokenAccounts = stateAccount.TokenAccounts;

                                            if(tokenAccounts?.Count == 0)
                                                return (txResult, "Could not find token accounts for account at state level.");

                                            var tokenAccount = tokenAccounts?.Where(x => x.SmartContractUID == scUID).FirstOrDefault();

                                            if(tokenAccount == null)
                                                return (txResult, "No tokens exist for this account at state level.");

                                            if(tokenAccount.Balance < amount.Value)
                                                return (txResult, "Insufficient Balance.");

                                            var decimalsUsed = BitConverter.GetBytes(decimal.GetBits(amount.Value)[3])[2];

                                            if(decimalsUsed > tokenDetails.DecimalPlaces)
                                                return (txResult, $"Too many decimals used. Amount used: {decimalsUsed} - Amount Allowed: {tokenDetails.DecimalPlaces}.");

                                            break;
                                        }

                                    case "TokenBurn()":
                                        {
                                            var jobj = JObject.Parse(txData);

                                            var fromAddress = jobj["FromAddress"]?.ToObject<string?>();
                                            var amount = jobj["Amount"]?.ToObject<decimal?>();

                                            if (amount == null ||fromAddress == null)
                                                return (txResult, $"TX Data was missing items.");

                                            var stateAccount = StateData.GetSpecificAccountStateTrei(fromAddress);
                                            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);

                                            if (scStateTreiRec == null)
                                                return (txResult, "Could not find smart contract at state level.");

                                            if (stateAccount == null)
                                                return (txResult, "Could not find account at state level.");

                                            var tokenDetails = scStateTreiRec.TokenDetails;

                                            if (tokenDetails == null)
                                                return (txResult, "Could not find token details for contract at state level.");

                                            if (tokenDetails.IsPaused)
                                                return (txResult, "Contract is paused. NO TXs may go through.");

                                            var tokenAccounts = stateAccount.TokenAccounts;

                                            if (tokenAccounts?.Count == 0)
                                                return (txResult, "Could not find token accounts for account at state level.");

                                            var tokenAccount = tokenAccounts?.Where(x => x.SmartContractUID == scUID).FirstOrDefault();

                                            if (tokenAccount == null)
                                                return (txResult, "No tokens exist for this account at state level.");

                                            if (tokenAccount.Balance < amount.Value)
                                                return (txResult, "Insufficient Balance.");

                                            var decimalsUsed = BitConverter.GetBytes(decimal.GetBits(amount.Value)[3])[2];

                                            if (decimalsUsed > tokenDetails.DecimalPlaces)
                                                return (txResult, $"Too many decimals used. Amount used: {decimalsUsed} - Amount Allowed: {tokenDetails.DecimalPlaces}.");

                                            if (txRequest.ToAddress != "Token_Base")
                                                return (txResult, "To Address must be 'Token_Base'.");

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

                                                if(scStateTreiRec.IsToken != null && scStateTreiRec.IsToken.Value == true)
                                                    return (txResult, "You are attempting to transfer a Token Smart contract, which is not allowed.");
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
                                                if (scStateTreiRec.IsToken != null && scStateTreiRec.IsToken.Value == true)
                                                    return (txResult, "You are attempting to burn a Token Smart contract, which is not allowed.");
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

                if(txRequest.TransactionType == TransactionType.NFT_SALE)
                {
                    var txData = txRequest.Data;
                    try
                    {
                        var jobj = JObject.Parse(txData);
                        var function = (string?)jobj["Function"];
                        if (function == null)
                            return (txResult, "SC Function cannot be null.");

                        if (txRequest.ToAddress.StartsWith("xRBX") || txRequest.FromAddress.StartsWith("xRBX"))
                            return (txResult, "You cannot perform a sale with a Reserve Account.");

                        var mempool = TransactionData.GetPool();

                        if(function == "Sale_Cancel()")
                        {
                            var scUID = jobj["ContractUID"]?.ToObject<string?>();
                            var keySign = jobj["KeySign"]?.ToObject<string?>();

                            if(keySign == null)
                                return (txResult, "Keysign cannot be null.");

                            if (scUID == null)
                                return (txResult, "SCUID cannot be null.");

                            var mempoolList = mempool.Query().Where(x =>
                            x.FromAddress == txRequest.FromAddress &&
                            x.Hash != txRequest.Hash &&
                            (x.TransactionType == TransactionType.NFT_SALE ||
                            x.TransactionType == TransactionType.NFT_TX ||
                            x.TransactionType == TransactionType.NFT_BURN)).ToList();

                            if (mempoolList?.Count > 0)
                            {
                                var reject = false;

                                foreach (var tx in mempoolList)
                                {
                                    var txObjData = JObject.Parse(txData);
                                    var mTXSCUID = txObjData["ContractUID"]?.ToObject<string?>();
                                    if (mTXSCUID == scUID)
                                    {
                                        reject = true;
                                        break;
                                    }
                                }

                                if (reject)
                                    return (txResult, "There is already a TX for this smart contract here.");
                            }

                            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);

                            if(scStateTreiRec == null)
                                return (txResult, "SC does not exist.");

                            if (txRequest.FromAddress != scStateTreiRec.OwnerAddress)
                                return (txResult, "You are attempting to transfer a Smart contract you don't own.");

                            if (!scStateTreiRec.IsLocked)
                                return (txResult, "You are attempting to Cancel a Smart contract sale that is not locked.");

                            if (scStateTreiRec.NextOwner == null)
                                return (txResult, "You are attempting to Cancel a Smart contract sale that has no next owner assigned to it.");
                            if (scStateTreiRec.IsToken == true)

                                return (txResult, "You are attempting to sell a Token Smart contract, which is not allowed.");

                            if (scStateTreiRec.PurchaseKeys != null)
                            {
                                if (scStateTreiRec.PurchaseKeys.Contains(keySign))
                                    return (txResult, "This purchase key has already been used for a previous purchase and may not be used again.");
                            }
                        }

                        if (function == "Sale_Start()" || function == "M_Sale_Start()")
                        {
                            var scUID = jobj["ContractUID"]?.ToObject<string?>();
                            var toAddress = jobj["NextOwner"]?.ToObject<string?>();
                            var keySign = jobj["KeySign"]?.ToObject<string?>();
                            var amountSoldFor = jobj["SoldFor"]?.ToObject<decimal?>();
                            var bidSignature = jobj["BidSignature"]?.ToObject<string?>();

                            if (keySign == null)
                                return (txResult, "Keysign cannot be null.");

                            if (scUID == null)
                                return (txResult, "SCUID cannot be null.");

                            if (toAddress == null)
                                return (txResult, "To Address cannot be null.");

                            if (amountSoldFor == null)
                                return (txResult, "Amount Sold For cannot be null.");

                            var mempoolList = mempool.Query().Where(x => 
                            x.FromAddress == txRequest.FromAddress && 
                            x.Hash != txRequest.Hash && 
                            (x.TransactionType == TransactionType.NFT_SALE || 
                            x.TransactionType == TransactionType.NFT_TX || 
                            x.TransactionType == TransactionType.NFT_BURN)).ToList();

                            if (mempoolList?.Count > 0)
                            {
                                var reject = false;

                                foreach (var tx in mempoolList)
                                {
                                    var txObjData = JObject.Parse(txData);
                                    var mTXSCUID = txObjData["ContractUID"]?.ToObject<string?>();
                                    if(mTXSCUID == scUID)
                                    {
                                        reject = true;
                                        break;
                                    }
                                }

                                if (reject)
                                    return (txResult, "There is already a TX for this smart contract here.");
                            }

                            if (function == "M_Sale_Start()")
                                bidSignature = "manual";

                            if (scUID != null && toAddress != null && keySign != null && amountSoldFor != null && bidSignature != null)
                            {
                                var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
                                if (scStateTreiRec != null)
                                {
                                    if (txRequest.FromAddress != scStateTreiRec.OwnerAddress)
                                        return (txResult, "You are attempting to transfer a Smart contract you don't own.");

                                    if (scStateTreiRec.IsLocked)
                                        return (txResult, "You are attempting to transfer a Smart contract that is locked.");

                                    if (scStateTreiRec.NextOwner != null)
                                        return (txResult, "You are attempting to transfer a Smart contract that has a new owner assigned to it.");

                                    if (scStateTreiRec.IsToken == true)
                                        return (txResult, "You are attempting to sell a Token Smart contract, which is not allowed.");

                                    if (scStateTreiRec.PurchaseKeys != null)
                                    {
                                        if(scStateTreiRec.PurchaseKeys.Contains(keySign))
                                            return (txResult, "This purchase key has already been used for a previous purchase and may not be used again.");
                                    }
                                    
                                    var signatureVerify = Bid.VerifyBidSignature(keySign, amountSoldFor.Value, toAddress, bidSignature);

                                    if (bidSignature == "manual")
                                        signatureVerify = true;

                                    if(!signatureVerify)
                                        NFTLogUtility.Log($"Sig Bad. Key: {keySign} | Amount Sold For: {amountSoldFor.Value} | ToAddress {toAddress} | Sig Script: {bidSignature}", "TransactionValidatorService.VerifyTX");

                                    if (!signatureVerify)
                                        return (txResult, "Bid signature did not verify.");
                                }
                                else
                                {
                                    return (txResult, "SC does not exist.");
                                }
                            }
                            else
                            {
                                return (txResult, "TX Data has a null value in ContractUID, NextOwner, Bid Signature, and/or KeySign");
                            }
                        }

                        if (function == "Sale_Complete()" || function == "M_Sale_Complete()")
                        {
                            //Complete sale logic.
                            var scUID = jobj["ContractUID"]?.ToObject<string?>();
                            var royalty = jobj["Royalty"]?.ToObject<bool?>();
                            var royaltyAmount = jobj["RoyaltyAmount"]?.ToObject<decimal?>();
                            var royaltyPayTo = jobj["RoyaltyPayTo"]?.ToObject<string?>();
                            var transactions = jobj["Transactions"]?.ToObject<List<Transaction>?>();
                            var keySign = jobj["KeySign"]?.ToObject<string?>();

                            var mempoolList = mempool.Query().Where(x =>
                            x.FromAddress == txRequest.FromAddress &&
                            x.Hash != txRequest.Hash &&
                            (x.TransactionType == TransactionType.NFT_SALE ||
                            x.TransactionType == TransactionType.NFT_TX ||
                            x.TransactionType == TransactionType.NFT_BURN)).ToList();

                            if (mempoolList?.Count > 0)
                            {
                                var reject = false;

                                foreach (var tx in mempoolList)
                                {
                                    var txObjData = JObject.Parse(txData);
                                    var mTXSCUID = txObjData["ContractUID"]?.ToObject<string?>();
                                    if (mTXSCUID == scUID)
                                    {
                                        reject = true;
                                        break;
                                    }
                                }

                                if (reject)
                                    return (txResult, "There is already a TX for this smart contract here.");
                            }

                            if (scUID != null && transactions != null && keySign != null)
                            {
                                var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
                                if (scStateTreiRec != null)
                                {
                                    var scMain = SmartContractMain.GenerateSmartContractInMemory(scStateTreiRec.ContractData);

                                    if(!scStateTreiRec.IsLocked)
                                        return (txResult, "This NFT has not been locked for purchase.");

                                    if (scStateTreiRec.NextOwner == null)
                                        return (txResult, "There is no next owner specified for this NFT.");

                                    if (scStateTreiRec.IsToken == true)
                                        return (txResult, "You are attempting to sell/buy a Token Smart contract, which is not allowed.");

                                    if (scStateTreiRec.PurchaseKeys != null)
                                    {
                                        if (scStateTreiRec.PurchaseKeys.Contains(keySign))
                                            return (txResult, "This purchase key has already been used for a previous purchase and may not be used again.");
                                    }

                                    if (txRequest.FromAddress != scStateTreiRec.NextOwner)
                                        return (txResult, "You are attempting to purchase a smart contract that does is not locked for you.");

                                    if ((txRequest.Amount + txRequest.Fee + transactions.Select(x => x.Amount + x.Fee).Sum()) > from.Balance)  // not sure about this... double check.
                                        return (txResult, "Amount exceeds balance.");

                                    if (scMain == null)
                                        return (txResult, "Failed to decompile smart contract.");

                                    if(scMain.Features != null)
                                    {
                                        var isRoyalty = scMain.Features.Exists(x => x.FeatureName == FeatureName.Royalty);
                                        if (isRoyalty)
                                        {
                                            if (royalty == null)
                                                return (txResult, $"Royalty Data was missing! Royalty");

                                            if (royaltyAmount == null)
                                                return (txResult, $"Royalty Data was missing! Royalty Amount");

                                            if (royaltyPayTo == null)
                                                return (txResult, $"Royalty Data was missing! Royalty Pay To");

                                            var royaltyFeat = scMain.Features?.Where(x => x.FeatureName == FeatureName.Royalty).FirstOrDefault();
                                            if (royaltyFeat != null)
                                            {
                                                var royaltyDetails = (RoyaltyFeature)royaltyFeat.FeatureFeatures;

                                                var stRoyaltyAmount = royaltyDetails.RoyaltyAmount;
                                                var stRoyaltyPayTo = royaltyDetails.RoyaltyPayToAddress;
                                                if (royaltyAmount != stRoyaltyAmount)
                                                    return (txResult, "Royalty Amounts do not match up.");

                                                if (royaltyAmount >= 1.0M)
                                                    return (txResult, "Royalty Amount may not exceed 99.99%");

                                                if (stRoyaltyPayTo != royaltyPayTo)
                                                    return (txResult, "Royalty Pay to does not match up.");

                                                var amountPaid = transactions.Sum(x => x.Amount);
                                                var payChecked = scStateTreiRec.PurchaseAmount - amountPaid > 1.0M ? false : true;
                                                if (!payChecked)
                                                    return (txResult, "Purchase amount does not match up with TX amounts.");

                                                if (transactions.Any(x => x.Data == null))
                                                    return (txResult, "Data cannot be missing for any TX.");

                                                var txToSeller = transactions.Where(x => x.Data.Contains("1/2")).FirstOrDefault();

                                                if (txToSeller == null)
                                                    return (txResult, "Could not find TX to seller.");

                                                var txToRoyaltyPayee = transactions.Where(x => x.Data.Contains("2/2")).FirstOrDefault();

                                                if (txToRoyaltyPayee == null)
                                                    return (txResult, "Could not find TX to royalty owner.");

                                                var txToSellerAmountCheck = txToSeller.Amount - (amountPaid * (1.0M - stRoyaltyAmount)) > 1 ? false : true;
                                                var txToRoyaltyAmountCheck = txToRoyaltyPayee.Amount - (amountPaid * stRoyaltyAmount) > 1 ? false : true;

                                                if (!txToSellerAmountCheck)
                                                    return (txResult, "Amount to seller does not match.");

                                                if (!txToRoyaltyAmountCheck)
                                                    return (txResult, "Amount to royalty owner does not match.");

                                                if (txToSeller.FromAddress != scStateTreiRec.NextOwner)
                                                    return (txResult, "You are attempting to purchase a smart contract that does is not locked for you.");
                                                if (txToRoyaltyPayee.FromAddress != scStateTreiRec.NextOwner)
                                                    return (txResult, "You are attempting to purchase a smart contract that does is not locked for you.");

                                                if (txToSeller.ToAddress != scStateTreiRec.OwnerAddress)
                                                    return (txResult, $"Funds are being sent to the wrong owner. You are sending here: {txToSeller.ToAddress}, but should be sending here {scStateTreiRec.OwnerAddress}");
                                                if (txToRoyaltyPayee.ToAddress != stRoyaltyPayTo)
                                                    return (txResult, $"Funds are being sent to the wrong Royalty Address. You are sending here: {txToRoyaltyPayee.ToAddress}, but should be sending here {stRoyaltyPayTo}");

                                                if (!string.IsNullOrEmpty(txToSeller.Signature))
                                                {
                                                    var isTxValid = SignatureService.VerifySignature(txToSeller.FromAddress, txToSeller.Hash, txToSeller.Signature);
                                                    if (!isTxValid)
                                                        return (txResult, "Signature Failed to verify for tx to seller.");
                                                }
                                                else
                                                {
                                                    return (txResult, "Signature to from seller tx cannot be null.");
                                                }

                                                if (!string.IsNullOrEmpty(txToRoyaltyPayee.Signature))
                                                {
                                                    var isTxValid = SignatureService.VerifySignature(txToRoyaltyPayee.FromAddress, txToRoyaltyPayee.Hash, txToRoyaltyPayee.Signature);
                                                    if (!isTxValid)
                                                        return (txResult, "Signature Failed to verify for tx to royalty payee.");
                                                }
                                                else
                                                {
                                                    return (txResult, "Signature cannot be null for royalty tx.");
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        var sellerTx = transactions.FirstOrDefault();
                                        if(sellerTx == null) 
                                            return (txResult, "Seller TX cannot be missing.");

                                        var sellerPayAmountCheck = scStateTreiRec.PurchaseAmount - sellerTx.Amount > 1.0M ? false : true;

                                        if(!sellerPayAmountCheck)
                                            return (txResult, "Amount in transaction does not match the state amount.");

                                        if (sellerTx.FromAddress != scStateTreiRec.NextOwner)
                                            return (txResult, "You are attempting to purchase a smart contract that does is not locked for you.");

                                        if (sellerTx.ToAddress != scStateTreiRec.OwnerAddress)
                                            return (txResult, $"Funds are being sent to the wrong owner. You are sending here: {sellerTx.ToAddress}, but should be sending here {scStateTreiRec.OwnerAddress}");

                                        if (!string.IsNullOrEmpty(sellerTx.Signature))
                                        {
                                            var isTxValid = SignatureService.VerifySignature(sellerTx.FromAddress, sellerTx.Hash, sellerTx.Signature);
                                            if (!isTxValid)
                                                return (txResult, "Signature Failed to verify for tx to seller.");
                                        }
                                        else
                                        {
                                            return (txResult, "Signature to from seller tx cannot be null.");
                                        }
                                    }

                                }
                            }
                            else
                            {
                                return (txResult, "Missing the proper purchase data.");
                            }
                        }

                    }
                    catch(Exception ex)
                    {
                        return (txResult, $"Unknown TX Error: {ex.ToString()}");
                    }
                    
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

                            if(Globals.LastBlock.Height >= Globals.V1ValHeight)
                            {
                                if (txRequest.Amount < Globals.ADNRRequiredRBX)
                                    return (txResult, $"There must be at least {Globals.ADNRRequiredRBX} RBX to perform an ADNR Function.");
                            }
                            else
                            {
                                if (txRequest.Amount < 1.0M)
                                    return (txResult, $"There must be at least 1 RBX to perform an ADNR Function.");
                            }
                            

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

                                    if (txRequest.Amount < Globals.TopicRequiredRBX)
                                        return (txResult, $"There must be at least {Globals.TopicRequiredRBX} RBX to create a Topic.");

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
                                                
                                                var validatorList = Globals.ActiveValidatorDict;
                                                var valExist = validatorList.ContainsKey(txRequest.FromAddress);
                                                if (!valExist)
                                                    return (txResult, "Validator has not crafted a block. Please wait til you craft a block to create a topic.");

                                                if (topic.VoterType == TopicVoterType.Validator)
                                                {
                                                    var stAcct = StateData.GetSpecificAccountStateTrei(txRequest.FromAddress);
                                                    if (stAcct != null)
                                                    {
                                                        var balance = (stAcct.Balance - (txRequest.Amount + txRequest.Fee));
                                                        if (balance < ValidatorService.ValidatorRequiredAmount())
                                                        {
                                                            return (txResult, $"Balance is under {ValidatorService.ValidatorRequiredAmount()}. Topic will not be allowed.");
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

                                    if(Globals.BlocksDownloadSlim.CurrentCount != 0 && Globals.BlocksDownloadV2Slim.CurrentCount != 0)
                                    {
                                        var stAcct = StateData.GetSpecificAccountStateTrei(txRequest.FromAddress);
                                        if (stAcct != null)
                                        {
                                            var balance = (stAcct.Balance - (txRequest.Amount + txRequest.Fee));
                                            if (balance < ValidatorService.ValidatorRequiredAmount())
                                            {
                                                return (txResult, $"Balance is under {ValidatorService.ValidatorRequiredAmount()}. Vote will not be allowed.");
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
                                    if (Globals.LastBlock.Height < Globals.TXHeightRule3)
                                    {
                                        if (txRequest.Amount < Globals.DecShopRequiredRBX)
                                            return (txResult, $"There must be at least {Globals.DecShopRequiredRBX} RBX to delete a Auction House.");
                                    }
                                    else
                                    {
                                        if (txRequest.Amount < Globals.DecShopDeleteRequiredRBX)
                                            return (txResult, $"There must be at least {Globals.DecShopDeleteRequiredRBX} RBX to delete a Auction House.");
                                    }
                                    
                                    string dsUID = jobj["UniqueId"].ToObject<string?>();
                                    if (!string.IsNullOrEmpty(dsUID))
                                    {
                                        //ensure they own the shop
                                        var treiRec = DecShop.GetDecShopStateTreiLeaf(dsUID);
                                        if (treiRec != null)
                                        {
                                            if (treiRec.OwnerAddress != txRequest.FromAddress)
                                                return (txResult, "You must be the valid owner of this shop.");

                                            //if (txRequest.Amount < Globals.DecShopDeleteRequiredRBX)
                                            //    return (txResult, $"There must be at least {Globals.DecShopDeleteRequiredRBX} RBX to delete a Auction House.");
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
                                        if (txRequest.Amount < Globals.DecShopRequiredRBX)
                                            return (txResult, $"There must be at least {Globals.DecShopRequiredRBX} RBX to create a Auction House.");

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

                                            if (lastUpdateTime < 43200)
                                            {
                                                if (txRequest.Amount < Globals.DecShopUpdateRequiredRBX)
                                                    return (txResult, $"There must be at least {Globals.DecShopUpdateRequiredRBX} RBX to Update an Auction House more than 1 time in 12 hours.");
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

                                        if (txRequest.Amount < Globals.RSRVAccountRegisterRBX)
                                            return (txResult, $"There must be at least {Globals.RSRVAccountRegisterRBX} RBX to register a Reserve Account on network.");

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
                                            {
                                                if(!blockVerify)
                                                    return (txResult, "Could not find a reserve transaction with that hash.");

                                                var txSearchResult = await TransactionData.GetNetworkTXByHash(hash, 0, false, true);

                                                if(txSearchResult != null)
                                                {
                                                    if(txSearchResult.UnlockTime == null)
                                                        return (txResult, "This TX has no unlock time.");

                                                    if (txSearchResult.UnlockTime < txRequest.Timestamp)
                                                        return (txResult, "This TX has already passed and can no longer be called back.");

                                                    rTx = new ReserveTransactions { 
                                                        FromAddress = txSearchResult.FromAddress, 
                                                        ConfirmTimestamp = (long)txSearchResult.UnlockTime,
                                                        Hash = hash,
                                                        ToAddress = txSearchResult.ToAddress,
                                                        Amount = txSearchResult.Amount,
                                                        Data = txSearchResult.Data,
                                                        Fee = txSearchResult.Fee,
                                                        Height = txSearchResult.Height,
                                                        Nonce = txSearchResult.Nonce,
                                                        ReserveTransactionStatus = ReserveTransactionStatus.Pending,
                                                        Signature = txSearchResult.Signature,
                                                        Timestamp = txSearchResult.Timestamp,
                                                        TransactionType = txSearchResult.TransactionType,
                                                        UnlockTime = txSearchResult.UnlockTime
                                                    };

                                                    ReserveTransactions.SaveReserveTx(rTx);
                                                }
                                            }

                                            if (rTx == null)
                                                return (txResult, "Could not find a reserve transaction with that hash.");

                                            if(rTx.ReserveTransactionStatus != ReserveTransactionStatus.Pending)
                                                return (txResult, $"This TX already has a status of: {rTx.ReserveTransactionStatus}");

                                            if (rTx.FromAddress != txRequest.FromAddress)
                                                return (txResult, "From address does not match the reserve tx from address. Cannot call back.");

                                            if (Globals.BlocksDownloadSlim.CurrentCount != 0 && Globals.BlocksDownloadV2Slim.CurrentCount != 0)
                                            {
                                                if (rTx.ConfirmTimestamp <= currentTime)
                                                    return (txResult, "This TX has already passed and can no longer be called back.");
                                            }
                                        }
                                    }

                                    if (function == "Recover()")
                                    {
                                        runReserveCheck = false;
                                        string recoveryAddress = jobj["RecoveryAddress"].ToObject<string>();
                                        string recoverySigScript = jobj["RecoverySigScript"].ToObject<string>();
                                        long sigTime = jobj["SignatureTime"].ToObject<long>();

                                        if (!string.IsNullOrEmpty(recoveryAddress) && !string.IsNullOrEmpty(recoverySigScript))
                                        {
                                            var currentTime = TimeUtil.GetTime(-600);

                                            if (Globals.BlocksDownloadSlim.CurrentCount != 0 && Globals.BlocksDownloadV2Slim.CurrentCount != 0)
                                            {
                                                if (currentTime > sigTime)
                                                return (txResult, "Recover request has expired.");
                                            }

                                            var stateRec = StateData.GetSpecificAccountStateTrei(txRequest.FromAddress);

                                            if (stateRec == null) 
                                                return (txResult, "State record cannot be null.");
                                            
                                            if (stateRec.RecoveryAccount == null)
                                                return (txResult, $"Reserve account does not have a recovery address.");

                                            if(stateRec.RecoveryAccount != recoveryAddress)
                                                return (txResult, $"Reserve account state record does not match the tx record.");

                                            string message = $"{sigTime}{recoveryAddress}";

                                            var sigVerify = SignatureService.VerifySignature(recoveryAddress, message, recoverySigScript);

                                            if(!sigVerify)
                                                return (txResult, $"Recovery account signature did not verify.");
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

                if (Globals.BlocksDownloadSlim.CurrentCount != 0 && Globals.BlocksDownloadV2Slim.CurrentCount != 0)
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
