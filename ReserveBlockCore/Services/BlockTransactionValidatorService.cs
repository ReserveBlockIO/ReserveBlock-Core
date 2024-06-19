using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.Utilities;
using System.Security.Principal;
using System;
using System.Xml.Linq;
using Spectre.Console;
using ReserveBlockCore.Bitcoin.Models;

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
            if (tx.TransactionType == TransactionType.NFT_TX || 
                tx.TransactionType == TransactionType.FTKN_TX ||
                tx.TransactionType == TransactionType.TKNZ_TX ||
                tx.TransactionType == TransactionType.SC_TX)
            {
                string scUID = "";
                string function = "";
                bool skip = false;
                bool isToken = false;
                JToken? scData = null;
                JArray? scDataArray = null;
                try
                {
                    scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
                    scData = scDataArray[0];
                    skip = true;
                }
                catch { }

                try
                {
                    if (!skip)
                    {
                        var jobj = JObject.Parse(tx.Data);
                        function = jobj["Function"]?.ToObject<string?>();
                        isToken = true;
                    }
                }
                catch { }

                if (scDataArray != null && skip)
                {
                    scData = scDataArray[0];
                    if (scData != null)
                    {
                        function = (string?)scData["Function"];
                        if (!string.IsNullOrWhiteSpace(function))
                        {
                            if (function == "Transfer()")
                            {
                                var txdata = TransactionData.GetAll();
                                tx.TransactionStatus = TransactionStatus.Success;
                                txdata.InsertSafe(tx);
                            }
                            if (function == "TransferCoin()")
                            {
                                var txdata = TransactionData.GetAll();
                                tx.TransactionStatus = TransactionStatus.Success;
                                txdata.InsertSafe(tx);
                            }
                        }
                    }
                }

                if(isToken && !skip)
                {
                    if (function == "TokenTransfer()")
                    {
                        var txdata = TransactionData.GetAll();
                        tx.TransactionStatus = TransactionStatus.Success;
                        txdata.InsertSafe(tx);
                    }

                    if (function == "TokenContractOwnerChange()")
                    {
                        var txdata = TransactionData.GetAll();
                        tx.TransactionStatus = TransactionStatus.Success;
                        txdata.InsertSafe(tx);
                    }

                    if (function == "TokenVoteTopicCast()")
                    {
                        var jobj = JObject.Parse(tx.Data);
                        scUID = jobj["ContractUID"]?.ToObject<string?>();
                        var topicUID = jobj["TopicUID"]?.ToObject<string?>();
                        var voteType = jobj["VoteType"]?.ToObject<VoteType?>();

                        var vote = new TokenVote
                        {
                            Address = tx.FromAddress,
                            BlockHeight = blockHeight,
                            TopicUID = topicUID,
                            TransactionHash = tx.Hash,
                            VoteType = voteType.Value,
                            SmartContractUID = scUID
                        };

                        TokenVote.SaveVote(vote);
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

                            var scUID = jobj["ContractUID"]?.ToObject<string?>();
                            var toAddress = jobj["NextOwner"]?.ToObject<string?>();
                            var keySign = jobj["KeySign"]?.ToObject<string?>();
                            var amountSoldFor = jobj["SoldFor"]?.ToObject<decimal?>();

                            //if you have a bid or buy now this should auto start the process.
                            _ = SmartContractService.CompleteSaleSmartContractTX(scUID, tx.FromAddress, amountSoldFor.Value, keySign);
                        }

                        if(function == "Sale_Complete()")
                        {
                            var txdata = TransactionData.GetAll();
                            tx.TransactionStatus = TransactionStatus.Success;
                            txdata.InsertSafe(tx);

                            AccountData.UpdateLocalBalanceAdd(tx.ToAddress, tx.Amount, false);
                        }

                        if(function == "M_Sale_Start()")
                        {
                            var txdata = TransactionData.GetAll();
                            tx.TransactionStatus = TransactionStatus.Success;
                            txdata.InsertSafe(tx);
                        }

                        if (function == "M_Sale_Complete()")
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
                    if (tx.TransactionType == TransactionType.NFT_MINT || 
                        tx.TransactionType == TransactionType.FTKN_MINT || 
                        tx.TransactionType == TransactionType.TKNZ_MINT ||
                        tx.TransactionType == TransactionType.SC_MINT)
                    {
                        SCLogUtility.Log($"SC TX Detected (Mint): {tx.Hash}", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
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
                                        SCLogUtility.Log($"SC Mint Completed: {scUID}", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
                                    }
                                }
                            }
                        }
                    }

                    if (tx.TransactionType == TransactionType.NFT_TX || 
                        tx.TransactionType == TransactionType.FTKN_TX ||
                        tx.TransactionType == TransactionType.TKNZ_TX ||
                        tx.TransactionType == TransactionType.SC_TX)
                    {
                        SCLogUtility.Log($"SC TX Detected (TX): {tx.Hash}", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
                        string function = "";
                        string data = "";
                        bool skip = false;
                        JToken? scData = null;
                        try
                        {
                            var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
                            scData = scDataArray[0];
                            function = (string?)scData["Function"];
                            data = (string?)scData["Data"];

                            skip = true;
                        }
                        catch { }

                        try
                        {
                            if (!skip)
                            {
                                var jobj = JObject.Parse(tx.Data);
                                function = jobj["Function"]?.ToObject<string?>();
                            }
                        }
                        catch { }
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

                                        SCLogUtility.Log($"SC Transfer: {scUID}", "BlockTransactionValidatorService.ProcessIncomingTransactions()");

                                        var sc = SmartContractMain.SmartContractData.GetSmartContract(scUID);

                                        if (sc != null)
                                        {
                                            SCLogUtility.Log($"SC Transfer - SC has been generated.", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
                                            if (localFromAddress == null)
                                            {
                                                if (locators != "NA")
                                                {
                                                    var assetList = await MD5Utility.GetAssetList(md5List);
                                                    var aqResult = AssetQueue.CreateAssetQueueItem(scUID, account.Address, locators, md5List, assetList, AssetQueue.TransferType.Download, true);
                                                    SCLogUtility.Log($"SC Transfer - Asset Queue items created.", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
                                                    //await NFTAssetFileUtility.DownloadAssetFromBeacon(scUID, locators, md5List);
                                                }
                                                else
                                                {
                                                    SCLogUtility.Log($"SC Transfer - No Locators in TX.", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
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
                                                SCLogUtility.Log("Failed to decompile smart contract for transfer in time.", "BlockValidatorService.ValidateBlock()");
                                            }
                                            else
                                            {

                                                sc = SmartContractMain.SmartContractData.GetSmartContract(scUID);
                                                if(sc?.Features != null)
                                                {
                                                    if(sc.Features.Exists(x => x.FeatureName == FeatureName.Tokenization))
                                                    {
                                                        await TokenizedBitcoin.SaveSmartContract(sc, null, tx.ToAddress);
                                                    }
                                                }
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
                                        SCLogUtility.Log($"NFT Evolve: {tx.Hash}", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
                                        var evolveTask = Task.Run(() => { EvolvingFeature.EvolveNFT(tx); });
                                        bool isCompletedSuccessfully = evolveTask.Wait(TimeSpan.FromMilliseconds(Globals.NFTTimeout * 1000));
                                        if (!isCompletedSuccessfully)
                                        {
                                            SCLogUtility.Log("Failed to decompile smart contract for evolve in time.", "BlockValidatorService.ValidateBlock() - line 224");
                                        }
                                    }
                                    break;
                                case "Devolve()":
                                    if (!string.IsNullOrWhiteSpace(data))
                                    {
                                        SCLogUtility.Log($"NFT Devolve: {tx.Hash}", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
                                        var devolveTask = Task.Run(() => { EvolvingFeature.DevolveNFT(tx); });
                                        bool isCompletedSuccessfully = devolveTask.Wait(TimeSpan.FromMilliseconds(Globals.NFTTimeout * 1000));
                                        if (!isCompletedSuccessfully)
                                        {
                                            SCLogUtility.Log("Failed to decompile smart contract for devolve in time.", "BlockValidatorService.ValidateBlock() - line 235");
                                        }
                                    }
                                    break;
                                case "ChangeEvolveStateSpecific()":
                                    if (!string.IsNullOrWhiteSpace(data))
                                    {
                                        SCLogUtility.Log($"NFT ChangeEvolveStateSpecific: {tx.Hash}", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
                                        var evoSpecificTask = Task.Run(() => { EvolvingFeature.EvolveToSpecificStateNFT(tx); });
                                        bool isCompletedSuccessfully = evoSpecificTask.Wait(TimeSpan.FromMilliseconds(Globals.NFTTimeout * 1000));
                                        if (!isCompletedSuccessfully)
                                        {
                                            SCLogUtility.Log("Failed to decompile smart contract for evo/devo specific in time.", "BlockValidatorService.ValidateBlock() - line 246");
                                        }
                                    }
                                    break;
                                case "TokenContractOwnerChange()":
                                    {
                                        var jobj = JObject.Parse(tx.Data);
          
                                        var localFromAddress = AccountData.GetSingleAccount(tx.FromAddress);
                                        var scUID = jobj["ContractUID"]?.ToObject<string?>();


                                        SCLogUtility.Log($"NFT Transfer: {scUID}", "BlockTransactionValidatorService.ProcessIncomingTransactions()");

                                        var sc = SmartContractMain.SmartContractData.GetSmartContract(scUID);
                                        if (sc != null)
                                        {
                                            
                                        }
                                        else
                                        {
                                            var scState = SmartContractStateTrei.GetSmartContractState(scUID);
                                            if(scState != null)
                                            {
                                                var trill = scState.ContractData;
                                                var transferTask = Task.Run(() => { SmartContractMain.SmartContractData.CreateSmartContract(trill); });
                                                bool isCompletedSuccessfully = transferTask.Wait(TimeSpan.FromMilliseconds(Globals.NFTTimeout * 1000));
                                                //testing
                                                //bool isCompletedSuccessfully = true;
                                                //transferTask.Wait();
                                                if (!isCompletedSuccessfully)
                                                {
                                                    SCLogUtility.Log("Failed to decompile smart contract for transfer in time.", "BlockValidatorService.ValidateBlock()");
                                                }
                                                else
                                                {
                                                    //download files here.

                                                }

                                            }
                                        }
                                        break;
                                    }
                                case "TransferCoin()":
                                    {
                                        var jobj = JObject.Parse(tx.Data);
                                        var scUID = jobj["ContractUID"]?.ToObject<string?>();

                                        var scState = SmartContractStateTrei.GetSmartContractState(scUID);

                                        if (scState != null)
                                        {
                                            var transferTask = Task.Run(() => { SmartContractMain.SmartContractData.CreateSmartContract(scState.ContractData); });
                                            bool isCompletedSuccessfully = transferTask.Wait(TimeSpan.FromMilliseconds(Globals.NFTTimeout * 1000));
                                            //testing
                                            //bool isCompletedSuccessfully = true;
                                            //transferTask.Wait();
                                            if (!isCompletedSuccessfully)
                                            {
                                                SCLogUtility.Log("Failed to decompile smart contract for transfer in time.", "BlockValidatorService.ValidateBlock()");
                                            }
                                            else
                                            {
                                                var sc = SmartContractMain.SmartContractData.GetSmartContract(scUID);
                                                if (sc?.Features != null)
                                                {
                                                    if (sc.Features.Exists(x => x.FeatureName == FeatureName.Tokenization))
                                                    {
                                                        await TokenizedBitcoin.SaveSmartContract(sc, null, tx.ToAddress);

                                                        await Bitcoin.Bitcoin.TransferCoinAudit(scUID);

                                                        var postAuditTknz = await TokenizedBitcoin.GetTokenizedBitcoin(scUID);

                                                        if(scState.SCStateTreiTokenizationTXes != null && scState.SCStateTreiTokenizationTXes.Any())
                                                        {
                                                            var balanceList = scState.SCStateTreiTokenizationTXes.ToList();
                                                            if(balanceList.Any())
                                                            {
                                                                var stateBalance = balanceList.Sum(x => x.Amount);
                                                                var totalBalance = postAuditTknz.Balance;
                                                                if(stateBalance > totalBalance)
                                                                {
                                                                    var txdata = TransactionData.GetAll();
                                                                    tx.TransactionStatus = TransactionStatus.Invalid;
                                                                    txdata.UpdateSafe(tx);

                                                                    //Delete because its invalid.
                                                                    TokenizedBitcoin.DeleteSmartContract(scUID);
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        
                                        break;
                                    }
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
                            if (function == "BTCAdnrTransfer()")
                            {
                                var BTCToAddress = (string?)scData["BTCToAddress"];
                                if (!string.IsNullOrWhiteSpace(BTCToAddress))
                                {
                                    await BitcoinAccount.TransferAdnrToAccount(BTCToAddress);
                                }
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
                if (tx.TransactionType == TransactionType.NFT_TX || 
                    tx.TransactionType == TransactionType.FTKN_TX ||
                    tx.TransactionType == TransactionType.FTKN_MINT ||
                    tx.TransactionType == TransactionType.FTKN_BURN ||
                    tx.TransactionType == TransactionType.TKNZ_TX)
                {
                    string scUID = "";
                    string function = "";
                    bool skip = false;
                    bool isToken = false;
                    JToken? scData = null;
                    JArray? scDataArray = null;
                    try
                    {
                        scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
                        scData = scDataArray[0];
                        scUID = (string?)scData["ContractUID"];
                        function = (string?)scData["Function"];
                        skip = true;
                    }
                    catch { }

                    try
                    {
                        if (!skip)
                        {
                            var jobj = JObject.Parse(tx.Data);
                            function = jobj["Function"]?.ToObject<string?>();
                            scUID = jobj["ContractUID"]?.ToObject<string?>();
                            isToken = true;
                        }
                    }
                    catch { }

                    //do transfer logic here! This is for person giving away or feature actions
                    
                    

                    if (!string.IsNullOrWhiteSpace(function))
                    {
                        if(function == "TokenDeploy()")
                        {
                            if(scUID != null)
                            {
                                if (!Globals.Tokens.TryGetValue(scUID, out var token))
                                {
                                    var stateToken = SmartContractStateTrei.GetSmartContractState(scUID);
                                    if(stateToken != null)
                                    {
                                        var tokenDetails = stateToken.TokenDetails;
                                        if(tokenDetails != null)
                                        {
                                            Globals.Tokens.TryAdd(scUID, tokenDetails); 
                                        }
                                    }
                                }
                            }
                            
                        }
                        if (function == "TokenMint()")
                        {
                            if (scUID != null)
                            {
                                if (!Globals.Tokens.TryGetValue(scUID, out var token))
                                {
                                    var stateToken = SmartContractStateTrei.GetSmartContractState(scUID);
                                    if (stateToken != null)
                                    {
                                        var tokenDetails = stateToken.TokenDetails;
                                        if (tokenDetails != null)
                                        {
                                            Globals.Tokens.TryAdd(scUID, tokenDetails);
                                        }
                                    }
                                }
                            }
                        }

                        if(function == "TokenBurn()")
                        {
                            if (scUID != null)
                            {
                                if (!Globals.Tokens.TryGetValue(scUID, out var token))
                                {
                                    var stateToken = SmartContractStateTrei.GetSmartContractState(scUID);
                                    if (stateToken != null)
                                    {
                                        var tokenDetails = stateToken.TokenDetails;
                                        if (tokenDetails != null)
                                        {
                                            Globals.Tokens.TryAdd(scUID, tokenDetails);
                                        }
                                    }
                                }
                            }
                        }

                        if(function == "TokenVoteTopicCast()")
                        {
                            var jobj = JObject.Parse(tx.Data);
                            var topicUID = jobj["TopicUID"]?.ToObject<string?>();
                            var voteType = jobj["VoteType"]?.ToObject<VoteType?>();

                            var vote = new TokenVote { 
                                Address = tx.FromAddress,
                                BlockHeight = blockHeight,
                                TopicUID = topicUID,
                                TransactionHash = tx.Hash,
                                VoteType = voteType.Value,
                                SmartContractUID = scUID
                            };

                            TokenVote.SaveVote(vote);
                        }

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
                                        if (scs.Features.Exists(x => x.FeatureName == FeatureName.Tokenization && x.FeatureName != FeatureName.Evolving))
                                        {
                                            SmartContractMain.SmartContractData.DeleteSmartContract(scUID);//deletes locally if they transfer it.
                                            TokenizedBitcoin.DeleteSmartContract(scUID);
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

                if(tx.TransactionType == TransactionType.NFT_SALE)
                {
                    var jobj = JObject.Parse(tx.Data);
                    if (jobj != null)
                    {
                        var function = jobj["Function"]?.ToObject<string?>();

                        if (!string.IsNullOrWhiteSpace(function))
                        {
                            if (function == "Sale_Complete()" || function == "M_Sale_Complete()")
                            {
                                var localFromAddress = AccountData.GetSingleAccount(tx.FromAddress);

                                var scUID = (string?)jobj["ContractUID"];
                                if(scUID != null)
                                {
                                    var scStateTrei = SmartContractStateTrei.GetSmartContractState(scUID);
                                    if(scStateTrei != null)
                                    {
                                        var locators = scStateTrei.Locators;
                                        var md5List = scStateTrei.MD5List;

                                        SCLogUtility.Log($"NFT Transfer: {scUID}", "BlockTransactionValidatorService.ProcessIncomingTransactions()");

                                        var sc = SmartContractMain.SmartContractData.GetSmartContract(scUID);

                                        if (sc != null)
                                        {
                                            SCLogUtility.Log($"NFT Transfer - SC has been generated.", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
                                            if (localFromAddress == null)
                                            {
                                                if (locators != "NA")
                                                {
                                                    var assetList = await MD5Utility.GetAssetList(md5List);
                                                    var aqResult = AssetQueue.CreateAssetQueueItem(scUID, account.Address, locators, md5List, assetList, AssetQueue.TransferType.Download, true);
                                                    SCLogUtility.Log($"NFT Transfer - Asset Queue items created.", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
                                                }
                                                else
                                                {
                                                    SCLogUtility.Log($"NFT Transfer - No Locators in TX.", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
                                                }
                                            }
                                        }
                                        else
                                        {
                                            SCLogUtility.Log($"NFT XFER LOG -1", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
                                            var transferTask = Task.Run(() => { SmartContractMain.SmartContractData.CreateSmartContract(scStateTrei.ContractData); });
                                            bool isCompletedSuccessfully = transferTask.Wait(TimeSpan.FromMilliseconds(Globals.NFTTimeout * 1000));

                                            if (!isCompletedSuccessfully)
                                            {
                                                SCLogUtility.Log("Failed to decompile smart contract for transfer in time.", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
                                            }
                                            else
                                            {
                                                SCLogUtility.Log($"NFT XFER LOG -2", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
                                                //download files here.
                                                if (localFromAddress != null)
                                                {
                                                    SCLogUtility.Log($"NFT XFER LOG -3", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
                                                    if (!string.IsNullOrEmpty(locators))
                                                    {
                                                        SCLogUtility.Log($"NFT XFER LOG -4", "BlockTransactionValidatorService.ProcessIncomingTransactions()");
                                                        var assetList = await MD5Utility.GetAssetList(md5List);
                                                        var aqResult = AssetQueue.CreateAssetQueueItem(scUID, account.Address, locators, md5List, assetList, AssetQueue.TransferType.Download, true);
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
                        if (function == "BTCAdnrCreate()")
                        {
                            var btcAddress = (string?)scData["BTCAddress"];

                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                if (!name.Contains(".btc"))
                                    name = name.ToLower() + ".btc";
                                if(btcAddress != null)
                                    await BitcoinAccount.AddAdnrToAccount(btcAddress, name);
                            }
                        }
                        if (function == "BTCAdnrTransfer()")
                        {
                            var BTCFromAddress = (string?)scData["BTCFromAddress"];
                            if(!string.IsNullOrWhiteSpace(BTCFromAddress))
                            {
                                await BitcoinAccount.RemoveAdnrFromAccount(BTCFromAddress);
                            }
                        }
                        if (function == "BTCAdnrDelete()")
                        {
                            var BTCFromAddress = (string?)scData["BTCFromAddress"];
                            if (!string.IsNullOrWhiteSpace(BTCFromAddress))
                            {
                                await BitcoinAccount.RemoveAdnrFromAccount(BTCFromAddress);
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
                string scUID = "";
                string function = "";
                bool skip = false;
                JToken? scData = null;
                JArray? scDataArray = null;
                try
                {
                    scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
                    function = (string?)scData["Function"];
                    scData = scDataArray[0];
                    skip = true;
                }
                catch { }

                try
                {
                    if (!skip)
                    {
                        var jobj = JObject.Parse(tx.Data);
                        function = jobj["Function"]?.ToObject<string?>();
                    }
                }
                catch { }

                if (scDataArray != null && skip)
                {
                    scData = scDataArray[0];
                    if (scData != null)
                    {
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

                    if (tx.TransactionType == TransactionType.NFT_TX ||
                        tx.TransactionType == TransactionType.FTKN_TX ||
                        tx.TransactionType == TransactionType.TKNZ_TX ||
                        tx.TransactionType == TransactionType.SC_TX)
                    {
                        SCLogUtility.Log($"NFT TX Detected (TX): {tx.Hash}", "BlockTransactionValidatorService.ProcessIncomingReserveTransactions()");
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

                                        SCLogUtility.Log($"SC Transfer: {scUID}", "BlockTransactionValidatorService.ProcessIncomingReserveTransactions()");

                                        var sc = SmartContractMain.SmartContractData.GetSmartContract(scUID);

                                        if (sc != null)
                                        {
                                            SCLogUtility.Log($"SC Transfer - SC has been generated.", "BlockTransactionValidatorService.ProcessIncomingReserveTransactions()");
                                            if (localFromAddress == null)
                                            {
                                                if (locators != "NA")
                                                {
                                                    var assetList = await MD5Utility.GetAssetList(md5List);
                                                    var aqResult = AssetQueue.CreateAssetQueueItem(scUID, account.Address, locators, md5List, assetList, AssetQueue.TransferType.Download, true);
                                                    SCLogUtility.Log($"SC Transfer - Asset Queue items created.", "BlockTransactionValidatorService.ProcessIncomingReserveTransactions()");
                                                    //await NFTAssetFileUtility.DownloadAssetFromBeacon(scUID, locators, md5List);
                                                }
                                                else
                                                {
                                                    SCLogUtility.Log($"SC Transfer - No Locators in TX.", "BlockTransactionValidatorService.ProcessIncomingReserveTransactions()");
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
                                                SCLogUtility.Log("Failed to decompile smart contract for transfer in time.", "BlockTransactionValidatorService.ProcessIncomingReserveTransactions()");
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
                                case "TokenContractOwnerChange()":
                                    {
                                        var jobj = JObject.Parse(tx.Data);

                                        var localFromAddress = AccountData.GetSingleAccount(tx.FromAddress);
                                        var scUID = jobj["ContractUID"]?.ToObject<string?>();


                                        SCLogUtility.Log($"SC Transfer: {scUID}", "BlockTransactionValidatorService.ProcessIncomingReserveTransactions()");

                                        var sc = SmartContractMain.SmartContractData.GetSmartContract(scUID);
                                        if (sc != null)
                                        {

                                        }
                                        else
                                        {
                                            var scState = SmartContractStateTrei.GetSmartContractState(scUID);
                                            if (scState != null)
                                            {
                                                var trill = scState.ContractData;
                                                var transferTask = Task.Run(() => { SmartContractMain.SmartContractData.CreateSmartContract(trill); });
                                                bool isCompletedSuccessfully = transferTask.Wait(TimeSpan.FromMilliseconds(Globals.NFTTimeout * 1000));
                                                //testing
                                                //bool isCompletedSuccessfully = true;
                                                //transferTask.Wait();
                                                if (!isCompletedSuccessfully)
                                                {
                                                    SCLogUtility.Log("Failed to decompile smart contract for transfer in time.", "BlockTransactionValidatorService.ProcessIncomingReserveTransactions()");
                                                }
                                                else
                                                {
                                                    //download files here.

                                                }

                                            }
                                        }
                                        break;
                                    }
                                case "Evolve()":
                                    if (!string.IsNullOrWhiteSpace(data))
                                    {
                                        SCLogUtility.Log($"NFT Evolve: {tx.Hash}", "BlockTransactionValidatorService.ProcessIncomingReserveTransactions()");
                                        var evolveTask = Task.Run(() => { EvolvingFeature.EvolveNFT(tx); });
                                        bool isCompletedSuccessfully = evolveTask.Wait(TimeSpan.FromMilliseconds(Globals.NFTTimeout * 1000));
                                        if (!isCompletedSuccessfully)
                                        {
                                            SCLogUtility.Log("Failed to decompile smart contract for evolve in time.", "BlockTransactionValidatorService.ProcessIncomingReserveTransactions() - line 224");
                                        }
                                    }
                                    break;
                                case "Devolve()":
                                    if (!string.IsNullOrWhiteSpace(data))
                                    {
                                        SCLogUtility.Log($"NFT Devolve: {tx.Hash}", "BlockTransactionValidatorService.ProcessIncomingReserveTransactions()");
                                        var devolveTask = Task.Run(() => { EvolvingFeature.DevolveNFT(tx); });
                                        bool isCompletedSuccessfully = devolveTask.Wait(TimeSpan.FromMilliseconds(Globals.NFTTimeout * 1000));
                                        if (!isCompletedSuccessfully)
                                        {
                                            SCLogUtility.Log("Failed to decompile smart contract for devolve in time.", "BlockTransactionValidatorService.ProcessIncomingReserveTransactions() - line 235");
                                        }
                                    }
                                    break;
                                case "ChangeEvolveStateSpecific()":
                                    if (!string.IsNullOrWhiteSpace(data))
                                    {
                                        SCLogUtility.Log($"NFT ChangeEvolveStateSpecific: {tx.Hash}", "BlockTransactionValidatorService.ProcessIncomingReserveTransactions()");
                                        var evoSpecificTask = Task.Run(() => { EvolvingFeature.EvolveToSpecificStateNFT(tx); });
                                        bool isCompletedSuccessfully = evoSpecificTask.Wait(TimeSpan.FromMilliseconds(Globals.NFTTimeout * 1000));
                                        if (!isCompletedSuccessfully)
                                        {
                                            SCLogUtility.Log("Failed to decompile smart contract for evo/devo specific in time.", "BlockTransactionValidatorService.ProcessIncomingReserveTransactions() - line 246");
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
                if (tx.TransactionType == TransactionType.NFT_TX ||
                    tx.TransactionType == TransactionType.FTKN_TX ||
                    tx.TransactionType == TransactionType.TKNZ_TX ||
                    tx.TransactionType == TransactionType.SC_TX)
                {
                    var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
                    var scData = scDataArray[0];

                    //do transfer logic here! This is for person giving away or feature actions
                    var scUID = (string?)scData["ContractUID"];
                    var function = (string?)scData["Function"];

                    if (!string.IsNullOrWhiteSpace(function))
                    {
                        if (function == "Transfer()" || function == "TokenContractOwnerChange()")
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
                                    var localTX = TransactionData.GetTxByHash(callBackHash);
                                    if(localTX != null)
                                    {
                                        var scDataArray = JsonConvert.DeserializeObject<JArray>(localTX.Data);
                                        var scData = scDataArray[0];
                                        var scUID = (string?)scData["ContractUID"];

                                        if(scUID != null)
                                        {
                                            var scState = SmartContractStateTrei.GetSmartContractState(scUID);
                                            if (scState != null)
                                            {
                                                var data = scState.ContractData;
                                                if (data != null)
                                                {
                                                    var sc = SmartContractMain.SmartContractData.GetSmartContract(scUID);
                                                    if (sc == null)
                                                    {
                                                        var transferTask = Task.Run(() => { SmartContractMain.SmartContractData.CreateSmartContract(data); });
                                                        bool isCompletedSuccessfully = transferTask.Wait(TimeSpan.FromMilliseconds(Globals.NFTTimeout * 1000));

                                                        if (!isCompletedSuccessfully)
                                                        {
                                                            SCLogUtility.Log("Failed to decompile smart contract for transfer in time.", "BlockTransactionValidatorService.ProcessOutgoingReserveTransaction()");
                                                        }
                                                    }
                                                    else
                                                    {
                                                        SCLogUtility.Log("SC was not null. Contract already exist.", "BlockTransactionValidatorService.ProcessOutgoingReserveTransaction()");
                                                    }
                                                }
                                                else
                                                {
                                                    SCLogUtility.Log("SC Data from TX was null.", "BlockTransactionValidatorService.ProcessOutgoingReserveTransaction()");
                                                }
                                            }
                                            else
                                            {
                                                SCLogUtility.Log("SC State Trei Rec was null.", "BlockTransactionValidatorService.ProcessOutgoingReserveTransaction()");
                                            }
                                        }
                                        else
                                        {
                                            SCLogUtility.Log("SCUID from TX was null.", "BlockTransactionValidatorService.ProcessOutgoingReserveTransaction()");
                                        }
                                    }
                                    else
                                    {
                                        SCLogUtility.Log("Original TX was null.", "BlockTransactionValidatorService.ProcessOutgoingReserveTransaction()");
                                    }
                                }
                            }
                            if (function == "Recover()")
                            {
                                var rAccount = ReserveAccount.GetReserveAccountSingle(tx.FromAddress);
                                if (rAccount != null)
                                {
                                    rAccount.AvailableBalance = 0.0M;
                                    rAccount.LockedBalance = 0.0M;
                                    ReserveAccount.SaveReserveAccount(rAccount);
                                }
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
