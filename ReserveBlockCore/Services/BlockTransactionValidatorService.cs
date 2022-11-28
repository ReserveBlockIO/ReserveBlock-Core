using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.Utilities;
using System.Security.Principal;

namespace ReserveBlockCore.Services
{
    public class BlockTransactionValidatorService
    {
        #region Process To Transactions
        public static async Task ProcessIncomingTransactions(Transaction tx, Account account)
        {
            if (tx.TransactionType == TransactionType.TX)
            {
                AccountData.UpdateLocalBalanceAdd(tx.ToAddress, tx.Amount);

                TransactionData.UpdateTxStatus(tx, TransactionStatus.Success);
            }
            if (tx.TransactionType == TransactionType.NFT_TX || tx.TransactionType == TransactionType.NFT_SALE)
            {
                var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
                if (scDataArray != null)
                {
                    var scData = scDataArray[0];
                    if (scData != null)
                    {
                        var function = (string?)scData["Function"];
                        if (!string.IsNullOrWhiteSpace(function))
                        {
                            if (function == "Transfer()")
                            {
                                var txdata = TransactionData.GetAll();
                                tx.TransactionStatus = TransactionStatus.Success;
                                txdata.InsertSafe(tx);
                            }
                        }
                    }
                }

            }
            if (Globals.IsChainSynced == true)//this is here so someone doesn't get spammed with API calls when starting wallet or syncing
            {
                //Call out to custom URL from config file with TX details
                if (!string.IsNullOrWhiteSpace(Globals.APICallURL))
                {
                    APICallURLService.CallURL(tx);
                }
            }
            if (tx.TransactionType != TransactionType.TX)
            {


                if (tx.TransactionType == TransactionType.NFT_MINT)
                {
                    var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
                    var scData = scDataArray[0];

                    if (scData != null)
                    {
                        var function = (string?)scData["Function"];
                        if (!string.IsNullOrWhiteSpace(function))
                        {
                            if (function == "Mint()")
                            {
                                var scUID = (string?)scData["ContractUID"];
                                if (!string.IsNullOrWhiteSpace(scUID))
                                {
                                    SmartContractMain.SmartContractData.SetSmartContractIsPublished(scUID);//flags local SC to isPublished now
                                }
                            }
                        }
                    }
                }

                if (tx.TransactionType == TransactionType.NFT_TX)
                {
                    var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
                    var scData = scDataArray[0];

                    var data = (string?)scData["Data"];
                    var function = (string?)scData["Function"];
                    if (!string.IsNullOrWhiteSpace(function))
                    {
                        switch (function)
                        {
                            case "Transfer()":
                                if (!string.IsNullOrWhiteSpace(data))
                                {
                                    var localFromAddress = AccountData.GetSingleAccount(tx.FromAddress);

                                    var locators = (string?)scData["Locators"];
                                    var md5List = (string?)scData["MD5List"];
                                    var scUID = (string?)scData["ContractUID"];

                                    var sc = SmartContractMain.SmartContractData.GetSmartContract(scUID);

                                    if (sc != null)
                                    {
                                        if (localFromAddress == null)
                                        {
                                            if (locators != "NA")
                                            {
                                                var assetList = await MD5Utility.GetAssetList(md5List);
                                                var aqResult = AssetQueue.CreateAssetQueueItem(scUID, account.Address, locators, md5List, assetList, AssetQueue.TransferType.Download, true);
                                                //await NFTAssetFileUtility.DownloadAssetFromBeacon(scUID, locators, md5List);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        var transferTask = Task.Run(() => { SmartContractMain.SmartContractData.CreateSmartContract(data); });
                                        bool isCompletedSuccessfully = transferTask.Wait(TimeSpan.FromMilliseconds(Globals.NFTTimeout * 1000));
                                        //testing
                                        //bool isCompletedSuccessfully = true;
                                        //transferTask.Wait();
                                        if (!isCompletedSuccessfully)
                                        {
                                            NFTLogUtility.Log("Failed to decompile smart contract for transfer in time.", "BlockValidatorService.ValidateBlock()");
                                        }
                                        else
                                        {
                                            //download files here.
                                            if (localFromAddress == null)
                                            {
                                                if (locators != "NA")
                                                {
                                                    var assetList = await MD5Utility.GetAssetList(md5List);
                                                    var aqResult = AssetQueue.CreateAssetQueueItem(scUID, account.Address, locators, md5List, assetList, AssetQueue.TransferType.Download, true);
                                                    //await NFTAssetFileUtility.DownloadAssetFromBeacon(scUID, locators, md5List);
                                                }

                                            }
                                        }
                                    }

                                }
                                break;
                            case "Evolve()":
                                if (!string.IsNullOrWhiteSpace(data))
                                {
                                    var evolveTask = Task.Run(() => { EvolvingFeature.EvolveNFT(tx); });
                                    bool isCompletedSuccessfully = evolveTask.Wait(TimeSpan.FromMilliseconds(Globals.NFTTimeout * 1000));
                                    if (!isCompletedSuccessfully)
                                    {
                                        NFTLogUtility.Log("Failed to decompile smart contract for evolve in time.", "BlockValidatorService.ValidateBlock() - line 224");
                                    }
                                }
                                break;
                            case "Devolve()":
                                if (!string.IsNullOrWhiteSpace(data))
                                {
                                    var devolveTask = Task.Run(() => { EvolvingFeature.DevolveNFT(tx); });
                                    bool isCompletedSuccessfully = devolveTask.Wait(TimeSpan.FromMilliseconds(Globals.NFTTimeout * 1000));
                                    if (!isCompletedSuccessfully)
                                    {
                                        NFTLogUtility.Log("Failed to decompile smart contract for devolve in time.", "BlockValidatorService.ValidateBlock() - line 235");
                                    }
                                }
                                break;
                            case "ChangeEvolveStateSpecific()":
                                if (!string.IsNullOrWhiteSpace(data))
                                {
                                    var evoSpecificTask = Task.Run(() => { EvolvingFeature.EvolveToSpecificStateNFT(tx); });
                                    bool isCompletedSuccessfully = evoSpecificTask.Wait(TimeSpan.FromMilliseconds(Globals.NFTTimeout * 1000));
                                    if (!isCompletedSuccessfully)
                                    {
                                        NFTLogUtility.Log("Failed to decompile smart contract for evo/devo specific in time.", "BlockValidatorService.ValidateBlock() - line 246");
                                    }
                                }
                                break;
                            default:
                                break;
                        }
                    }
                }

                if (tx.TransactionType == TransactionType.ADNR)
                {
                    var scData = JObject.Parse(tx.Data);

                    if (scData != null)
                    {
                        var function = (string?)scData["Function"];
                        if (!string.IsNullOrWhiteSpace(function))
                        {
                            if (function == "AdnrTransfer()")
                            {
                                await Account.TransferAdnrToAccount(tx.FromAddress, tx.ToAddress);
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region Process From Transactions

        public static async Task ProcessOutgoingTransaction(Transaction tx, Account account)
        {
            var fromTx = tx;
            fromTx.Amount = tx.Amount * -1M;
            fromTx.Fee = tx.Fee * -1M;

            TransactionData.UpdateTxStatus(fromTx, TransactionStatus.Success);

            if (tx.TransactionType != TransactionType.TX)
            {
                if (tx.TransactionType == TransactionType.NFT_TX)
                {
                    var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
                    var scData = scDataArray[0];

                    //do transfer logic here! This is for person giving away or feature actions
                    var scUID = (string?)scData["ContractUID"];
                    var function = (string?)scData["Function"];

                    if (!string.IsNullOrWhiteSpace(function))
                    {
                        if (function == "Transfer()")
                        {
                            if (!string.IsNullOrWhiteSpace(scUID))
                            {
                                var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
                                var scs = SmartContractMain.SmartContractData.GetSmartContract(scUID);

                                if (scs != null)
                                {
                                    if (scs.Features != null)
                                    {
                                        if (scs.Features.Exists(x => x.FeatureName == FeatureName.Evolving))
                                        {
                                            if (scStateTreiRec != null)
                                            {
                                                if (scStateTreiRec.MinterAddress != null)
                                                {
                                                    var evoOwner = AccountData.GetAccounts().FindOne(x => x.Address == scStateTreiRec.MinterAddress);
                                                    if (evoOwner == null)
                                                    {
                                                        SmartContractMain.SmartContractData.DeleteSmartContract(scUID);//deletes locally if they transfer it.
                                                    }
                                                }
                                                else
                                                {
                                                    SmartContractMain.SmartContractData.DeleteSmartContract(scUID);//deletes locally if they transfer it.
                                                }
                                            }
                                            else
                                            {
                                                SmartContractMain.SmartContractData.DeleteSmartContract(scUID);//deletes locally if they transfer it.
                                            }
                                        }
                                        else
                                        {
                                            SmartContractMain.SmartContractData.DeleteSmartContract(scUID);//deletes locally if they transfer it.
                                        }
                                    }
                                    else
                                    {
                                        SmartContractMain.SmartContractData.DeleteSmartContract(scUID);//deletes locally if they transfer it.
                                    }
                                }
                            }
                        }
                    }
                }
                if (tx.TransactionType == TransactionType.NFT_BURN)
                {
                    var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
                    var scData = scDataArray[0];
                    //do burn logic here! This is for person giving away or feature actions
                    var scUID = (string?)scData["ContractUID"];
                    var function = (string?)scData["Function"];
                    if (!string.IsNullOrWhiteSpace(function))
                    {
                        if (function == "Burn()")
                        {
                            if (!string.IsNullOrWhiteSpace(scUID))
                            {
                                SmartContractMain.SmartContractData.DeleteSmartContract(scUID);//deletes locally if they burn it.
                            }
                        }
                    }

                }

                if (tx.TransactionType == TransactionType.ADNR)
                {
                    var scData = JObject.Parse(tx.Data);

                    var function = (string?)scData["Function"];
                    var name = (string?)scData["Name"];
                    if (!string.IsNullOrWhiteSpace(function))
                    {
                        if (function == "AdnrCreate()")
                        {
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                if (!name.Contains(".rbx"))
                                    name = name + ".rbx";
                                await Account.AddAdnrToAccount(tx.FromAddress, name);
                            }
                        }
                        if (function == "AdnrDelete()")
                        {
                            await Account.DeleteAdnrFromAccount(tx.FromAddress);
                        }
                        if (function == "AdnrTransfer()")
                        {
                            await Account.DeleteAdnrFromAccount(tx.FromAddress);
                        }
                    }
                }
            }
        }

        #endregion
    }
}
