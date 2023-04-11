using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.Utilities;
using System.Security.Principal;
using System;
using System.Xml.Linq;

namespace ReserveBlockCore.Services
{
    public class BlockTransactionValidatorService
    {
        #region Process Incoming (to) Transactions
        public static async Task ProcessIncomingTransactions(Transaction tx, Account account, long blockHeight)
        {
            if (tx.TransactionType == TransactionType.TX)
            {
                var isReserveSend = tx.FromAddress.StartsWith("xRBX") ? true : false;

                AccountData.UpdateLocalBalanceAdd(tx.ToAddress, tx.Amount, isReserveSend);

                var fromAccount = AccountData.GetSingleAccount(tx.FromAddress);
                var fromAccountReserve = ReserveAccount.GetReserveAccountSingle(tx.FromAddress);
                
                var status = tx.FromAddress.StartsWith("xRBX") ? TransactionStatus.Reserved : TransactionStatus.Success;
                if(fromAccount == null && fromAccountReserve == null)
                {
                    TransactionData.UpdateTxStatusAndHeight(tx, status, blockHeight, false);
                }
                else
                {
                    //same wallet TX detected. This will ensure the To TX is also added.
                    TransactionData.UpdateTxStatusAndHeight(tx, status, blockHeight, true);
                }
                
            }
            if (tx.TransactionType == TransactionType.NFT_TX)
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
            if (tx.TransactionType == TransactionType.NFT_SALE)
            {
                var jobj = JObject.Parse(tx.Data);
                if (jobj != null)
                {
                    var function = jobj["Function"]?.ToObject<string?>();

                    if (!string.IsNullOrWhiteSpace(function))
                    {
                        if (function == "Sale_Start()")
                        {
                            var txdata = TransactionData.GetAll();
                            tx.TransactionStatus = TransactionStatus.Success;
                            txdata.InsertSafe(tx);
                        }

                        if(function == "Sale_Complete()")
                        {
                            var txdata = TransactionData.GetAll();
                            tx.TransactionStatus = TransactionStatus.Success;
                            txdata.InsertSafe(tx);

                            AccountData.UpdateLocalBalanceAdd(tx.ToAddress, tx.Amount, false);
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
                if(!Globals.IgnoreIncomingNFTs)
                {
                    if (tx.TransactionType == TransactionType.NFT_MINT)
                    {
                        NFTLogUtility.Log($"NFT TX Detected (Mint): {tx.Hash}", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
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
                                        NFTLogUtility.Log($"NFT Mint Completed: {scUID}", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
                                    }
                                }
                            }
                        }
                    }

                    if (tx.TransactionType == TransactionType.NFT_TX)
                    {
                        NFTLogUtility.Log($"NFT TX Detected (TX): {tx.Hash}", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
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

                                        NFTLogUtility.Log($"NFT Transfer: {scUID}", "BlockTransactionValidatorService.ProcessIncomingTransactions()");

                                        var sc = SmartContractMain.SmartContractData.GetSmartContract(scUID);

                                        if (sc != null)
                                        {
                                            NFTLogUtility.Log($"NFT Transfer - SC has been generated.", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
                                            if (localFromAddress == null)
                                            {
                                                if (locators != "NA")
                                                {
                                                    var assetList = await MD5Utility.GetAssetList(md5List);
                                                    var aqResult = AssetQueue.CreateAssetQueueItem(scUID, account.Address, locators, md5List, assetList, AssetQueue.TransferType.Download, true);
                                                    NFTLogUtility.Log($"NFT Transfer - Asset Queue items created.", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
                                                    //await NFTAssetFileUtility.DownloadAssetFromBeacon(scUID, locators, md5List);
                                                }
                                                else
                                                {
                                                    NFTLogUtility.Log($"NFT Transfer - No Locators in TX.", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
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
                                        NFTLogUtility.Log($"NFT Evolve: {tx.Hash}", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
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
                                        NFTLogUtility.Log($"NFT Devolve: {tx.Hash}", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
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
                                        NFTLogUtility.Log($"NFT ChangeEvolveStateSpecific: {tx.Hash}", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
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

        #region Process Outgoing (from) Transactions

        public static async Task ProcessOutgoingTransaction(Transaction tx, Account account, long blockHeight)
        {
            var fromTx = tx;
            fromTx.Amount = tx.Amount * -1M;
            fromTx.Fee = tx.Fee * -1M;

            TransactionData.UpdateTxStatusAndHeight(fromTx, TransactionStatus.Success, blockHeight);

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

                if(tx.TransactionType == TransactionType.DSTR)
                {
                    if(!string.IsNullOrEmpty(tx.Data))
                    {
                        var jobj = JObject.Parse(tx.Data);
                        var function = (string?)jobj["Function"];
                        if (!string.IsNullOrWhiteSpace(function))
                        {
                            if (function == "DecShopCreate()")
                            {
                                DecShop? decshop = jobj["DecShop"]?.ToObject<DecShop>();
                                if (decshop != null)
                                {
                                    var myDecShop = DecShop.GetMyDecShopInfo();
                                    if(myDecShop != null)
                                    {
                                        if (decshop.UniqueId == myDecShop.UniqueId)
                                        {
                                            myDecShop.OriginalBlockHeight = tx.Height;
                                            myDecShop.OriginalTXHash = tx.Hash;
                                            myDecShop.LatestBlockHeight = tx.Height;
                                            myDecShop.LatestTXHash = tx.Hash;
                                            myDecShop.NeedsPublishToNetwork = false;
                                            myDecShop.IsPublished = true;
                                            await DecShop.SaveMyDecShopLocal(myDecShop, false);
                                        }
                                    }
                                }
                            }
                            if (function == "DecShopUpdate()")
                            {
                                DecShop? decshop = jobj["DecShop"]?.ToObject<DecShop>();
                                if (decshop != null)
                                {
                                    var myDecShop = DecShop.GetMyDecShopInfo();
                                    if (myDecShop != null)
                                    {
                                        if (decshop.UniqueId == myDecShop.UniqueId)
                                        {
                                            myDecShop.LatestBlockHeight = tx.Height;
                                            myDecShop.LatestTXHash = tx.Hash;
                                            myDecShop.UpdateTimestamp = TimeUtil.GetTime();
                                            myDecShop.NeedsPublishToNetwork = false;
                                            myDecShop.IsPublished = true;
                                            await DecShop.SaveMyDecShopLocal(myDecShop, false);
                                        }
                                    }
                                }
                            }
                            if (function == "DecShopDelete()")
                            {
                                var myDecShop = DecShop.GetMyDecShopInfo();
                                if (myDecShop != null)
                                {
                                    var uId = (string?)jobj["UniqueId"];
                                    if (!string.IsNullOrEmpty(uId))
                                    {
                                        if(myDecShop.UniqueId == uId)
                                        {
                                            var db = DecShop.DecShopLocalDB();
                                            if(db != null)
                                            {
                                                db.Delete(myDecShop.Id);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region Process Incoming (to) ReserveTransactions
        public static async Task ProcessIncomingReserveTransactions(Transaction tx, ReserveAccount account, long blockHeight)
        {
            if (tx.TransactionType == TransactionType.TX)
            {
                var isReserveSend = tx.FromAddress.StartsWith("xRBX") ? true : false;

                ReserveAccount.UpdateOnlyBalanceAdd(tx.ToAddress, tx.Amount, isReserveSend);

                var fromAccount = AccountData.GetSingleAccount(tx.FromAddress);
                var fromReserveAccount = ReserveAccount.GetReserveAccountSingle(tx.FromAddress);
                if (fromAccount == null && fromReserveAccount == null)
                {
                    TransactionData.UpdateTxStatusAndHeight(tx, TransactionStatus.Success, blockHeight);
                }
                else
                {
                    //same wallet TX detected. This will ensure the To TX is also added.
                    TransactionData.UpdateTxStatusAndHeight(tx, TransactionStatus.Success, blockHeight, true);
                }

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
                                tx.TransactionStatus = tx.FromAddress.StartsWith("xRBX") ? TransactionStatus.Reserved : TransactionStatus.Success;
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
                if (!Globals.IgnoreIncomingNFTs)
                {

                    if (tx.TransactionType == TransactionType.NFT_TX)
                    {
                        NFTLogUtility.Log($"NFT TX Detected (TX): {tx.Hash}", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
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

                                        NFTLogUtility.Log($"NFT Transfer: {scUID}", "BlockTransactionValidatorService.ProcessIncomingTransactions()");

                                        var sc = SmartContractMain.SmartContractData.GetSmartContract(scUID);

                                        if (sc != null)
                                        {
                                            NFTLogUtility.Log($"NFT Transfer - SC has been generated.", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
                                            if (localFromAddress == null)
                                            {
                                                if (locators != "NA")
                                                {
                                                    var assetList = await MD5Utility.GetAssetList(md5List);
                                                    var aqResult = AssetQueue.CreateAssetQueueItem(scUID, account.Address, locators, md5List, assetList, AssetQueue.TransferType.Download, true);
                                                    NFTLogUtility.Log($"NFT Transfer - Asset Queue items created.", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
                                                    //await NFTAssetFileUtility.DownloadAssetFromBeacon(scUID, locators, md5List);
                                                }
                                                else
                                                {
                                                    NFTLogUtility.Log($"NFT Transfer - No Locators in TX.", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
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
                                        NFTLogUtility.Log($"NFT Evolve: {tx.Hash}", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
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
                                        NFTLogUtility.Log($"NFT Devolve: {tx.Hash}", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
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
                                        NFTLogUtility.Log($"NFT ChangeEvolveStateSpecific: {tx.Hash}", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
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

        #region Process Outgoing (from) Reserve Transactions

        public static async Task ProcessOutgoingReserveTransaction(Transaction tx, ReserveAccount account, long blockHeight)
        {
            var fromTx = tx;
            fromTx.Amount = tx.Amount * -1M;
            fromTx.Fee = tx.Fee * -1M;

            var status = tx.TransactionType == TransactionType.RESERVE ? TransactionStatus.Success : TransactionStatus.Reserved;
            TransactionData.UpdateTxStatusAndHeight(fromTx, status, blockHeight);

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

                if (tx.TransactionType == TransactionType.RESERVE)
                {
                    try
                    {
                        if (tx.Data != null)
                        {
                            var jobj = JObject.Parse(tx.Data);
                            var function = (string?)jobj["Function"];

                            if (function == "Register()")
                            {
                                account.IsNetworkProtected = true;
                                ReserveAccount.SaveReserveAccount(account);
                            }
                            if (function == "CallBack()")
                            {
                                var callBackHash = (string?)jobj["Hash"];
                                if (callBackHash != null)
                                {
                                    var rTX = ReserveTransactions.GetTransactions(callBackHash);
                                    if (rTX != null)
                                    {
                                        var ctx = rTX.Transaction;

                                        var rtxDb = ReserveTransactions.GetReserveTransactionsDb();

                                        var scDataArray = JsonConvert.DeserializeObject<JArray>(ctx.Data);
                                        var scData = scDataArray[0];
                                        var cfunction = (string?)scData["Function"];
                                        var scUID = (string?)scData["ContractUID"];

                                        var data = (string?)scData["Data"];
                                        if (data != null)
                                        {
                                            var sc = SmartContractMain.SmartContractData.GetSmartContract(scUID);
                                            if (sc == null)
                                            {
                                                var transferTask = Task.Run(() => { SmartContractMain.SmartContractData.CreateSmartContract(data); });
                                                bool isCompletedSuccessfully = transferTask.Wait(TimeSpan.FromMilliseconds(Globals.NFTTimeout * 1000));

                                                if (!isCompletedSuccessfully)
                                                {
                                                    NFTLogUtility.Log("Failed to decompile smart contract for transfer in time.", "BlockTransactionValidatorService.ProcessOutgoingReserveTransaction()");
                                                }
                                            }
                                            else
                                            {
                                                NFTLogUtility.Log("SC was not null. Contract already exist.", "BlockTransactionValidatorService.ProcessOutgoingReserveTransaction()");
                                            }
                                        }
                                        else
                                        {
                                            NFTLogUtility.Log("SC Data from TX was null.", "BlockTransactionValidatorService.ProcessOutgoingReserveTransaction()");
                                        }

                                        if (rtxDb != null)
                                            rtxDb.DeleteSafe(rTX.Id);
                                    }
                                }
                            }
                            if (function == "Recover()")
                            {
                                //var rAccount = ReserveAccount.GetReserveAccountSingle(tx.FromAddress);
                                //if (rAccount != null)
                                //{
                                //    var rStateTrei = StateData.GetSpecificAccountStateTrei(tx.FromAddress);
                                //    if (rStateTrei != null)
                                //    {
                                //        rAccount.AvailableBalance = rStateTrei.Balance;
                                //        rAccount.LockedBalance = rStateTrei.LockedBalance;
                                //    }
                                //}
                            }

                        }
                    }
                    catch(Exception ex)
                    {
                        ErrorLogUtility.LogError($"Error performing callback function. Error: {ex.ToString()}", "BlockTransactionValidatorService.ProcessOutgoingReserveTransaction()");
                    }
                }
            }


        }

        #endregion

    }
}
