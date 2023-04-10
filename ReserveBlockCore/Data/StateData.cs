using ReserveBlockCore.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.Utilities;
using ReserveBlockCore.Services;
using System.Collections.Concurrent;
using System.Xml.Linq;

namespace ReserveBlockCore.Data
{
    public class StateData
    {
        
        public static void CreateGenesisWorldTrei(Block block)
        {
            var trxList = block.Transactions.ToList();
            var accStTrei = new List<AccountStateTrei>();

            trxList.ForEach(x => {

                var acctStateTreiTo = new AccountStateTrei
                {
                    Key = x.ToAddress,
                    Nonce = 0, 
                    Balance = (x.Amount), //subtract from the address
                    StateRoot = block.StateRoot
                };

                accStTrei.Add(acctStateTreiTo);

            });

            var worldTrei = new WorldTrei {
                StateRoot = block.StateRoot,
            };

            var wTrei = DbContext.DB_WorldStateTrei.GetCollection<WorldTrei>(DbContext.RSRV_WSTATE_TREI);
            wTrei.InsertSafe(worldTrei);
            var aTrei = DbContext.DB_AccountStateTrei.GetCollection<AccountStateTrei>(DbContext.RSRV_ASTATE_TREI);
            aTrei.InsertBulkSafe(accStTrei);
        }

        public static async Task UpdateTreis(Block block)
        {
            Globals.TreisUpdating = true;
            var txList = block.Transactions.ToList();
            var txCount = txList.Count();
            int txTreiUpdateSuccessCount = 0;
            var txFailList = new List<Transaction>();

            var accStTrei = GetAccountStateTrei();
            ConcurrentDictionary<string, StateTreiAuditData> StateTreiAuditDict = new ConcurrentDictionary<string, StateTreiAuditData>();

            foreach(var tx in txList)
            {
                try
                {
                    if (block.Height == 0)
                    {
                        var acctStateTreiFrom = new AccountStateTrei
                        {
                            Key = tx.FromAddress,
                            Nonce = tx.Nonce + 1, //increase Nonce for next use
                            Balance = 0, //subtract from the address
                            StateRoot = block.StateRoot
                        };

                        accStTrei.InsertSafe(acctStateTreiFrom);
                    }
                    else
                    {
                        if (tx.FromAddress != "Coinbase_TrxFees" && tx.FromAddress != "Coinbase_BlkRwd")
                        {
                            var from = GetSpecificAccountStateTrei(tx.FromAddress);

                            if (!tx.FromAddress.StartsWith("xRBX"))
                            {
                                from.Nonce += 1;
                                from.StateRoot = block.StateRoot;
                                from.Balance -= (tx.Amount + tx.Fee);

                                accStTrei.UpdateSafe(from);
                            }
                            else
                            {
                                if(tx.TransactionType != TransactionType.RESERVE)
                                {
                                    ReserveTransactions rTx = new ReserveTransactions
                                    {
                                        ConfirmTimestamp = (long)tx.UnlockTime,
                                        FromAddress = tx.FromAddress,
                                        ToAddress = tx.ToAddress,
                                        Transaction = tx,
                                        Hash = tx.Hash
                                    };

                                    ReserveTransactions.SaveReserveTx(rTx);
                                }

                                from.Nonce += 1;
                                from.StateRoot = block.StateRoot;
                                from.Balance -= (tx.Amount + tx.Fee);
                                if(tx.TransactionType == TransactionType.TX)
                                    from.LockedBalance += tx.Amount;

                                accStTrei.UpdateSafe(from);
                            }
                            
                        }
                    }

                    if (tx.ToAddress != "Adnr_Base" && 
                        tx.ToAddress != "DecShop_Base" && 
                        tx.ToAddress != "Topic_Base" && 
                        tx.ToAddress != "Vote_Base" && 
                        tx.ToAddress != "Reserve_Base")
                    {
                        var to = GetSpecificAccountStateTrei(tx.ToAddress);
                        if (tx.TransactionType == TransactionType.TX)
                        {
                            if (to == null)
                            {
                                var acctStateTreiTo = new AccountStateTrei
                                {
                                    Key = tx.ToAddress,
                                    Nonce = 0,
                                    Balance = 0.0M,
                                    StateRoot = block.StateRoot
                                };

                                if (!tx.FromAddress.StartsWith("xRBX"))
                                {
                                    acctStateTreiTo.Balance += tx.Amount;
                                }
                                else
                                {
                                    acctStateTreiTo.LockedBalance += tx.Amount;
                                }

                                accStTrei.InsertSafe(acctStateTreiTo);
                            }
                            else
                            {
                                to.StateRoot = block.StateRoot;
                                if (!tx.FromAddress.StartsWith("xRBX"))
                                {
                                    to.Balance += tx.Amount;
                                }
                                else
                                {
                                    to.LockedBalance += tx.Amount;
                                }
                                
                                accStTrei.UpdateSafe(to);
                            }
                        }
                    }

                    if (tx.TransactionType != TransactionType.TX)
                    {
                        if (tx.TransactionType == TransactionType.NFT_TX || tx.TransactionType == TransactionType.NFT_MINT
                            || tx.TransactionType == TransactionType.NFT_BURN)
                        {
                            var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
                            var scData = scDataArray[0];
                            var function = (string?)scData["Function"];
                            var scUID = (string?)scData["ContractUID"];

                            if (!string.IsNullOrWhiteSpace(function))
                            {
                                switch (function)
                                {
                                    case "Mint()":
                                        AddNewlyMintedContract(tx);
                                        break;
                                    case "Transfer()":
                                        TransferSmartContract(tx);
                                        break;
                                    case "Burn()":
                                        BurnSmartContract(tx);
                                        break;
                                    case "Evolve()":
                                        EvolveSC(tx);
                                        break;
                                    case "Devolve()":
                                        DevolveSC(tx);
                                        break;
                                    case "ChangeEvolveStateSpecific()":
                                        EvolveDevolveSpecific(tx);
                                        break;
                                    default:
                                        break;
                                }
                            }

                        }

                        if(tx.TransactionType == TransactionType.NFT_SALE)
                        {
                            var txData = tx.Data;
                            if (!string.IsNullOrWhiteSpace(txData))
                            {
                                var jobj = JObject.Parse(txData);
                                var function = (string?)jobj["Function"];
                                if (!string.IsNullOrWhiteSpace(function))
                                {
                                    switch (function)
                                    {
                                        case "Sale_Start()":
                                            StartSaleSmartContract(tx);
                                            break;
                                        case "Sale_Complete()":
                                            CompleteSaleSmartContract(tx);
                                            break;
                                        default:
                                            break;
                                    }
                                }
                            }
                        }

                        if (tx.TransactionType == TransactionType.ADNR)
                        {
                            var txData = tx.Data;
                            if (!string.IsNullOrWhiteSpace(txData))
                            {
                                var jobj = JObject.Parse(txData);
                                var function = (string?)jobj["Function"];
                                if (!string.IsNullOrWhiteSpace(function))
                                {
                                    switch (function)
                                    {
                                        case "AdnrCreate()":
                                            AddNewAdnr(tx);
                                            break;
                                        case "AdnrTransfer()":
                                            TransferAdnr(tx);
                                            break;
                                        case "AdnrDelete()":
                                            DeleteAdnr(tx);
                                            break;
                                        default:
                                            break;
                                    }
                                }
                            }
                        }

                        if (tx.TransactionType == TransactionType.VOTE_TOPIC)
                        {
                            var txData = tx.Data;
                            if (!string.IsNullOrWhiteSpace(txData))
                            {
                                var jobj = JObject.Parse(txData);
                                if (jobj != null)
                                {
                                    var function = (string)jobj["Function"];
                                    TopicTrei topic = jobj["Topic"].ToObject<TopicTrei>();
                                    if (topic != null)
                                    {
                                        topic.Id = 0;//save new
                                        topic.BlockHeight = tx.Height;
                                        TopicTrei.SaveTopic(topic);
                                        if(topic.VoteTopicCategory == VoteTopicCategories.AdjVoteIn)
                                            AdjVoteInQueue.SaveToQueue(topic);
                                    }
                                }
                            }
                        }

                        if (tx.TransactionType == TransactionType.VOTE)
                        {
                            var txData = tx.Data;
                            if (!string.IsNullOrWhiteSpace(txData))
                            {
                                var jobj = JObject.Parse(txData);
                                if (jobj != null)
                                {
                                    var function = (string)jobj["Function"];
                                    Vote vote = jobj["Vote"].ToObject<Vote>();
                                    if (vote != null)
                                    {
                                        vote.Id = 0;
                                        vote.TransactionHash = tx.Hash;
                                        vote.BlockHeight = tx.Height;
                                        var result = Vote.SaveVote(vote);
                                        if (result)
                                        {
                                            var topic = TopicTrei.GetSpecificTopic(vote.TopicUID);
                                            if (topic != null)
                                            {
                                                if (vote.VoteType == VoteType.Yes)
                                                    topic.VoteYes += 1;
                                                if (vote.VoteType == VoteType.No)
                                                    topic.VoteNo += 1;

                                                TopicTrei.UpdateTopic(topic);
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        if (tx.TransactionType == TransactionType.DSTR)
                        {
                            var txData = tx.Data;
                            if (!string.IsNullOrWhiteSpace(txData))
                            {
                                var jobj = JObject.Parse(txData);
                                var function = (string?)jobj["Function"];
                                if (!string.IsNullOrWhiteSpace(function))
                                {
                                    switch (function)
                                    {
                                        case "DecShopCreate()":
                                            AddNewDecShop(tx);
                                            break;
                                        case "DecShopUpdate()":
                                            UpdateDecShop(tx);
                                            break;
                                        case "DecShopDelete()":
                                            DeleteDecShop(tx);
                                            break;
                                        default:
                                            break;
                                    }
                                }
                            }
                        }

                        if(tx.TransactionType == TransactionType.RESERVE)
                        {
                            var txData = tx.Data;
                            if (!string.IsNullOrWhiteSpace(txData))
                            {
                                var jobj = JObject.Parse(txData);
                                var function = (string?)jobj["Function"];
                                if (!string.IsNullOrWhiteSpace(function))
                                {
                                    switch (function)
                                    {
                                        case "Register()":
                                            RegisterReserveAccount(tx);
                                            break;
                                        case "CallBack()":
                                            var callBackHash = (string?)jobj["Hash"];
                                            CallBackReserveAccountTx(callBackHash);
                                            break;
                                        case "Recover()":
                                            var restoreHash = (string?)jobj["Hash"];
                                            RecoverReserveAccountTx(restoreHash, block.StateRoot);
                                            break;
                                        default:
                                            break;
                                    }
                                }
                            }
                        }
                    }

                    txTreiUpdateSuccessCount += 1;
                }
                catch(Exception ex)
                {
                    txFailList.Add(tx);
                    var txJson = JsonConvert.SerializeObject(tx);
                    ErrorLogUtility.LogError($"Error Updating State Treis. Error: {ex.ToString()}", "StateData.UpdateTreis() - Part 1");
                    ErrorLogUtility.LogError($"TX Info. TX: {txJson}", "StateData.UpdateTreis() - Part 2");
                }
            }

            if(txTreiUpdateSuccessCount != txCount)
            {
                var txFailListJson = JsonConvert.SerializeObject(txFailList);
                ErrorLogUtility.LogError($"TX Success Count Failed to match tx Count. TX Fail List: {txFailListJson}", "StateData.UpdateTreis() - Part 3");
            }

            WorldTrei.UpdateWorldTrei(block);
            Globals.TreisUpdating = false;
        }

        public static void UpdateTreiFromReserve(List<ReserveTransactions> txList)
        {
            var accStTrei = GetAccountStateTrei();
            var rtxDb = ReserveTransactions.GetReserveTransactionsDb();
            var txDb = Transaction.GetAll();

            foreach(var rtx in  txList)
            {
                try
                {
                    var tx = rtx.Transaction;

                    if(tx.TransactionType == TransactionType.TX)
                    {
                        if (tx.FromAddress != "Coinbase_TrxFees" && tx.FromAddress != "Coinbase_BlkRwd" && tx.ToAddress != "Reserve_Base")
                        {
                            var from = GetSpecificAccountStateTrei(tx.FromAddress);
                            if (from != null)
                            {
                                from.LockedBalance -= tx.Amount;
                                accStTrei.UpdateSafe(from);
                            }

                        }

                        if (tx.ToAddress != "Adnr_Base" &&
                            tx.ToAddress != "DecShop_Base" &&
                            tx.ToAddress != "Topic_Base" &&
                            tx.ToAddress != "Vote_Base" &&
                            tx.ToAddress != "Reserve_Base")
                        {
                            var to = GetSpecificAccountStateTrei(tx.ToAddress);
                            if (tx.TransactionType == TransactionType.TX)
                            {
                                if (to != null)
                                {
                                    if (tx.FromAddress.StartsWith("xRBX"))
                                    {
                                        to.Balance += tx.Amount;
                                        to.LockedBalance -= tx.Amount;

                                        accStTrei.UpdateSafe(to);
                                    }
                                }
                            }
                        }
                    }
                    if(tx.TransactionType == TransactionType.NFT_TX)
                    {
                        var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
                        var scData = scDataArray[0];
                        var function = (string?)scData["Function"];
                        var scUID = (string?)scData["ContractUID"];

                        if(function != null)
                        {
                            if (function == "Transfer()")
                            {
                                var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
                                if (scStateTreiRec != null)
                                {
                                    
                                    scStateTreiRec.OwnerAddress = tx.ToAddress;
                                    scStateTreiRec.NextOwner = null;
                                    scStateTreiRec.IsLocked = false;

                                    SmartContractStateTrei.UpdateSmartContract(scStateTreiRec);
                                }
                            }
                        }
                    }

                    var rtxRec = rtxDb.Query().Where(x => x.Id == rtx.Id).FirstOrDefault();
                    var hash = tx.Hash;

                    if (rtxRec != null)
                    {
                        rtxDb.DeleteSafe(rtxRec.Id);
                    }

                    var txRec = TransactionData.GetTxByHash(hash);
                    if (txRec != null)
                    {
                        txRec.TransactionStatus = TransactionStatus.Success;
                        txDb.UpdateSafe(txRec);
                    }
                }
                catch {  }
            }
        }

        public static LiteDB.ILiteCollection<AccountStateTrei> GetAccountStateTrei()
        {
            var aTrei = DbContext.DB_AccountStateTrei.GetCollection<AccountStateTrei>(DbContext.RSRV_ASTATE_TREI);
            return aTrei;
            
        }

        public static AccountStateTrei? GetSpecificAccountStateTrei(string address)
        {
            var aTrei = GetAccountStateTrei();
            var account = aTrei.FindOne(x => x.Key == address);
            if (account == null)
            {
                return null;
            }
            else
            {
                return account;
            }
        }

        public static SmartContractStateTrei GetSpecificSmartContractStateTrei(string scUID)
        {
            var scTrei = DbContext.DB_SmartContractStateTrei.GetCollection<SmartContractStateTrei>(DbContext.RSRV_SCSTATE_TREI);
            var account = scTrei.FindOne(x => x.SmartContractUID == scUID);
            if (account == null)
            {
                return null;
            }
            else
            {
                return account;
            }
        }
        private static void RegisterReserveAccount(Transaction tx)
        {
            try
            {
                if (tx.Data != null)
                {
                    var jobj = JObject.Parse(tx.Data);
                    if (jobj != null)
                    {
                        var recoveryAddress = (string?)jobj["RecoveryAddress"];
                        if (recoveryAddress != null)
                        {
                            var stateDB = GetAccountStateTrei();
                            var reserveAccountLeaf = GetSpecificAccountStateTrei(tx.FromAddress);
                            if (reserveAccountLeaf != null)
                            {
                                reserveAccountLeaf.RecoveryAccount = recoveryAddress;
                                stateDB.UpdateSafe(reserveAccountLeaf);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private static void CallBackReserveAccountTx(string? callBackHash)
        {
            try
            {
                if(callBackHash != null)
                {
                    var rTX = ReserveTransactions.GetTransactions(callBackHash);
                    if (rTX != null)
                    {
                        var tx = rTX.Transaction;
                        var rtxDb = ReserveTransactions.GetReserveTransactionsDb();

                        if(tx.TransactionType == TransactionType.TX)
                        {
                            var stDb = GetAccountStateTrei();
                            var stateTreiFrom = GetSpecificAccountStateTrei(tx.FromAddress);
                            var stateTreiTo = GetSpecificAccountStateTrei(tx.ToAddress);

                            if (stateTreiFrom != null)
                            {
                                //return amount to From address
                                stateTreiFrom.LockedBalance -= tx.Amount;
                                stateTreiFrom.Balance += tx.Amount;
                                if (stDb != null)
                                    stDb.UpdateSafe(stateTreiFrom);

                                var rLocalAccount = ReserveAccount.GetReserveAccountSingle(stateTreiFrom.Key);
                                if (rLocalAccount != null)
                                {
                                    var rDb = ReserveAccount.GetReserveAccountsDb();
                                    rLocalAccount.LockedBalance -= tx.Amount;
                                    rLocalAccount.AvailableBalance += tx.Amount;
                                    if (rDb != null)
                                        rDb.UpdateSafe(rLocalAccount);
                                }
                            }
                            if (stateTreiTo != null)
                            {
                                //remove amount from locked To address
                                stateTreiTo.LockedBalance -= tx.Amount;
                                if (stDb != null)
                                    stDb.UpdateSafe(stateTreiTo);

                                var localAccount = AccountData.GetSingleAccount(stateTreiTo.Key);
                                if (localAccount != null)
                                {
                                    var accountDB = AccountData.GetAccounts();
                                    localAccount.LockedBalance -= tx.Amount;
                                    if (accountDB != null)
                                        accountDB.UpdateSafe(localAccount);
                                }

                                var rLocalAccount = ReserveAccount.GetReserveAccountSingle(stateTreiTo.Key);
                                if (rLocalAccount != null)
                                {
                                    var rDb = ReserveAccount.GetReserveAccountsDb();
                                    rLocalAccount.LockedBalance -= tx.Amount;
                                    if (rDb != null)
                                        rDb.UpdateSafe(rLocalAccount);
                                }
                            }
                        }

                        if(tx.TransactionType == TransactionType.NFT_TX)
                        {
                            var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
                            var scData = scDataArray[0];
                            var function = (string?)scData["Function"];
                            var scUID = (string?)scData["ContractUID"];

                            if(scUID != null)
                            {
                                var scStateTrei = SmartContractStateTrei.GetSmartContractState(scUID);
                                if(scStateTrei != null)
                                {
                                    var scDb = SmartContractStateTrei.GetSCST();
                                    if(scDb != null)
                                    {
                                        scStateTrei.NextOwner = null;
                                        scStateTrei.IsLocked = false;
                                        scDb.UpdateSafe(scStateTrei);
                                    }
                                }
                            }
                        }
                        

                        var localTx = TransactionData.GetTxByHash(tx.Hash);
                        if(localTx != null)
                        {
                            //Change TX status to CalledBack
                            var txDB = Transaction.GetAll();
                            localTx.TransactionStatus = TransactionStatus.CalledBack;
                            if(txDB != null)
                                txDB.UpdateSafe(localTx);
                        }

                        var localFrom = ReserveAccount.GetReserveAccountSingle(rTX.FromAddress);
                        if( localFrom == null ) 
                        {
                            if (rtxDb != null)
                                rtxDb.DeleteSafe(rTX.Id);
                        }
                        //Delete from Reserve Transaction List
                        
                    }
                }
            }
            catch { }
        }
        private static void RecoverReserveAccountTx(string? restoreHash, string stateRoot)
        {
            try
            {
                if (restoreHash != null)
                {
                    var rTX = ReserveTransactions.GetTransactions(restoreHash);
                    if(rTX != null)
                    {
                        var tx = rTX.Transaction;
                        var rtxDb = ReserveTransactions.GetReserveTransactionsDb();
                        var stateTreiFrom = GetSpecificAccountStateTrei(tx.FromAddress);
                        if (tx.TransactionType == TransactionType.TX)
                        {
                            var stDb = GetAccountStateTrei();
                            
                            var stateTreiTo = GetSpecificAccountStateTrei(tx.ToAddress);

                            if (stateTreiFrom != null)
                            {
                                var recoveryAddress = stateTreiFrom.RecoveryAccount;
                                if (recoveryAddress != null)
                                {
                                    stateTreiFrom.LockedBalance -= tx.Amount;
                                    if (stDb != null)
                                        stDb.UpdateSafe(stateTreiFrom);

                                    var rLocalAccount = ReserveAccount.GetReserveAccountSingle(stateTreiFrom.Key);
                                    if (rLocalAccount != null)
                                    {
                                        var rDb = ReserveAccount.GetReserveAccountsDb();
                                        rLocalAccount.LockedBalance -= tx.Amount;
                                        if (rDb != null)
                                            rDb.UpdateSafe(rLocalAccount);
                                    }

                                    var stateTreiRecovery = GetSpecificAccountStateTrei(recoveryAddress);
                                    if (stateTreiRecovery != null)
                                    {
                                        stateTreiRecovery.Balance += tx.Amount;
                                        if (stDb != null)
                                            stDb.UpdateSafe(stateTreiFrom);
                                    }
                                    else
                                    {
                                        var acctStateTreiTo = new AccountStateTrei
                                        {
                                            Key = recoveryAddress,
                                            Nonce = 0,
                                            Balance = tx.Amount, //subtract from the address
                                            StateRoot = stateRoot
                                        };

                                        if (stDb != null)
                                            stDb.InsertSafe(acctStateTreiTo);

                                    }

                                    var localAccount = AccountData.GetSingleAccount(recoveryAddress);
                                    if (localAccount != null)
                                    {
                                        var accountDB = AccountData.GetAccounts();
                                        localAccount.Balance += tx.Amount;
                                        if (accountDB != null)
                                            accountDB.UpdateSafe(localAccount);
                                    }
                                }
                            }

                            if (stateTreiTo != null)
                            {
                                stateTreiTo.LockedBalance -= tx.Amount;
                                if (stDb != null)
                                    stDb.UpdateSafe(stateTreiTo);
                            }
                        }
                        
                        if(tx.TransactionType == TransactionType.NFT_TX)
                        {
                            var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
                            var scData = scDataArray[0];
                            var function = (string?)scData["Function"];
                            var scUID = (string?)scData["ContractUID"];
                            var recoveryAddress = stateTreiFrom?.RecoveryAccount;

                            if (scUID != null)
                            {
                                var scStateTrei = SmartContractStateTrei.GetSmartContractState(scUID);
                                if (scStateTrei != null)
                                {
                                    var scDb = SmartContractStateTrei.GetSCST();
                                    if (scDb != null)
                                    {
                                        if(recoveryAddress != null)
                                        {
                                            scStateTrei.OwnerAddress = recoveryAddress;
                                            scStateTrei.NextOwner = null;
                                            scStateTrei.IsLocked = false;
                                            scDb.UpdateSafe(scStateTrei);
                                        }
                                        
                                    }
                                }
                            }
                        }

                        var localTx = TransactionData.GetTxByHash(tx.Hash);
                        if (localTx != null)
                        {
                            //Change TX status to CalledBack
                            var txDB = Transaction.GetAll();
                            localTx.TransactionStatus = TransactionStatus.Recovered;
                            if (txDB != null)
                                txDB.UpdateSafe(localTx);
                        }

                        //Delete from Reserve Transaction List
                        if (rtxDb != null)
                            rtxDb.DeleteSafe(rTX.Id);
                    }
                }
            }
            catch { }
        }
        private static void AddNewDecShop(Transaction tx)
        {
            try
            {
                if (tx.Data != null)
                {
                    var jobj = JObject.Parse(tx.Data);
                    if (jobj != null)
                    {
                        DecShop? decshop = jobj["DecShop"]?.ToObject<DecShop>();
                        if (decshop != null)
                        {
                            decshop.OriginalBlockHeight = tx.Height;
                            decshop.OriginalTXHash = tx.Hash;
                            decshop.LatestBlockHeight = tx.Height;
                            decshop.LatestTXHash = tx.Hash;
                            decshop.IsPublished = true;
                            decshop.NeedsPublishToNetwork = false;
                            var result = DecShop.SaveDecShopStateTrei(decshop);
                        }
                    }
                }
            }
            catch { }
        }
        private static void UpdateDecShop(Transaction tx)
        {
            try
            {
                if (tx.Data != null)
                {
                    var jobj = JObject.Parse(tx.Data);
                    if (jobj != null)
                    {
                        DecShop? decshop = jobj["DecShop"]?.ToObject<DecShop>();
                        if (decshop != null)
                        {
                            decshop.LatestBlockHeight = tx.Height;
                            decshop.LatestTXHash = tx.Hash;
                            decshop.UpdateTimestamp = TimeUtil.GetTime();
                            decshop.NeedsPublishToNetwork = false;
                            decshop.IsPublished = true;
                            var result = DecShop.UpdateDecShopStateTrei(decshop);
                        }
                    }
                }
            }
            catch { }
        }

        private static void DeleteDecShop(Transaction tx)
        {
            try
            {
                if (tx.Data != null)
                {
                    var jobj = JObject.Parse(tx.Data);
                    if(jobj != null)
                    {
                        var uId = (string?)jobj["UniqueId"];
                        if(!string.IsNullOrEmpty(uId))
                        {
                            var db = DecShop.DecShopTreiDb();
                            var decShop = DecShop.GetDecShopStateTreiLeaf(uId);
                            if(db != null)
                            {
                                if(decShop != null)
                                {
                                    db.DeleteSafe(decShop.Id);
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private static void AddNewAdnr(Transaction tx)
        {
            try
            {
                var jobj = JObject.Parse(tx.Data);
                var name = (string?)jobj["Name"];
                Adnr adnr = new Adnr();

                adnr.Address = tx.FromAddress;
                adnr.Timestamp = tx.Timestamp;
                adnr.Name = name + ".rbx";
                adnr.TxHash = tx.Hash;

                Adnr.SaveAdnr(adnr);
                
            }
            catch(Exception ex)
            {                
                ErrorLogUtility.LogError("Failed to deserialized TX Data for ADNR", "TransactionValidatorService.VerifyTx()");
            }
        }
        private static void TransferAdnr(Transaction tx)
        {
            bool complete = false;
            while(!complete)
            {
                var adnrs = Adnr.GetAdnr();
                if (adnrs != null)
                {
                    var adnr = adnrs.FindOne(x => x.Address == tx.FromAddress);
                    if (adnr != null)
                    {
                        adnr.Address = tx.ToAddress;
                        adnr.TxHash = tx.Hash;
                        adnrs.UpdateSafe(adnr);
                        complete = true;
                    }
                }
            }
            
        }

        private static void DeleteAdnr(Transaction tx)
        {
            try
            {
                Adnr.DeleteAdnr(tx.FromAddress);
            }
            catch (Exception ex)
            {                
                ErrorLogUtility.LogError("Failed to deserialized TX Data for ADNR", "TransactionValidatorService.VerifyTx()");
            }
        }

        private static void AddNewlyMintedContract(Transaction tx)
        {
            SmartContractStateTrei scST = new SmartContractStateTrei();
            var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
            var scData = scDataArray[0];
            if (scData != null)
            {
                var function = (string?)scData["Function"];
                var data = (string?)scData["Data"];
                var scUID = (string?)scData["ContractUID"];
                var md5List = (string?)scData["MD5List"];


                scST.ContractData = data;
                scST.MinterAddress = tx.FromAddress;
                scST.OwnerAddress = tx.FromAddress;
                scST.SmartContractUID = scUID;
                scST.Nonce = 0;
                scST.MD5List = md5List;

                try
                {
                    var sc = SmartContractMain.GenerateSmartContractInMemory(data);
                    if (sc.Features != null)
                    {
                        var evoFeatures = sc.Features.Where(x => x.FeatureName == FeatureName.Evolving).Select(x => x.FeatureFeatures).FirstOrDefault();
                        var isDynamic = false;
                        if (evoFeatures != null)
                        {
                            var evoFeatureList = (List<EvolvingFeature>)evoFeatures;
                            foreach (var feature in evoFeatureList)
                            {
                                var evoFeature = (EvolvingFeature)feature;
                                if (evoFeature.IsDynamic == true)
                                    isDynamic = true;
                            }
                        }

                        if (!isDynamic)
                            scST.MinterManaged = true;
                    }
                }
                catch { }

                //Save to state trei
                SmartContractStateTrei.SaveSmartContract(scST);
            }

        }
        private static void TransferSmartContract(Transaction tx)
        {
            SmartContractStateTrei scST = new SmartContractStateTrei();
            var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
            var scData = scDataArray[0];

            var function = (string?)scData["Function"];
            var data = (string?)scData["Data"];
            var scUID = (string?)scData["ContractUID"];
            var locator = (string?)scData["Locators"];

            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
            if(scStateTreiRec != null)
            {
                if(tx.FromAddress.StartsWith("xRBX"))
                {
                    scStateTreiRec.NextOwner = tx.ToAddress;
                    scStateTreiRec.IsLocked = true;
                    scStateTreiRec.Nonce += 1;
                    scStateTreiRec.ContractData = data;
                    scStateTreiRec.Locators = !string.IsNullOrWhiteSpace(locator) ? locator : scStateTreiRec.Locators;
                }
                else
                {
                    scStateTreiRec.OwnerAddress = tx.ToAddress;
                    scStateTreiRec.Nonce += 1;
                    scStateTreiRec.ContractData = data;
                    scStateTreiRec.Locators = !string.IsNullOrWhiteSpace(locator) ? locator : scStateTreiRec.Locators;
                }
                SmartContractStateTrei.UpdateSmartContract(scStateTreiRec);
            }

        }
        private static void BurnSmartContract(Transaction tx)
        {
            SmartContractStateTrei scST = new SmartContractStateTrei();
            var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
            var scData = scDataArray[0];
            var function = (string?)scData["Function"];
            var data = (string?)scData["Data"];
            var scUID = (string?)scData["ContractUID"];

            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
            if (scStateTreiRec != null)
            {
                SmartContractStateTrei.DeleteSmartContract(scStateTreiRec);
            }

        }

        private static void EvolveSC(Transaction tx)
        {
            SmartContractStateTrei scST = new SmartContractStateTrei();
            var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
            var scData = scDataArray[0];

            var data = (string?)scData["Data"];
            var scUID = (string?)scData["ContractUID"];

            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
            if (scStateTreiRec != null)
            {
                scStateTreiRec.Nonce += 1;
                scStateTreiRec.ContractData = data;

                SmartContractStateTrei.UpdateSmartContract(scStateTreiRec);
            }
        }

        private static void DevolveSC(Transaction tx)
        {
            SmartContractStateTrei scST = new SmartContractStateTrei();
            var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
            var scData = scDataArray[0];

            var data = (string?)scData["Data"];
            var scUID = (string?)scData["ContractUID"];

            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
            if (scStateTreiRec != null)
            {
                scStateTreiRec.Nonce += 1;
                scStateTreiRec.ContractData = data;

                SmartContractStateTrei.UpdateSmartContract(scStateTreiRec);
            }
        }

        private static void EvolveDevolveSpecific(Transaction tx)
        {
            SmartContractStateTrei scST = new SmartContractStateTrei();
            var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
            var scData = scDataArray[0];

            var data = (string?)scData["Data"];
            var scUID = (string?)scData["ContractUID"];

            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
            if (scStateTreiRec != null)
            {
                scStateTreiRec.Nonce += 1;
                scStateTreiRec.ContractData = data;

                SmartContractStateTrei.UpdateSmartContract(scStateTreiRec);
            }
        }

        private static void StartSaleSmartContract(Transaction tx)
        {
            SmartContractStateTrei scST = new SmartContractStateTrei();
            var txData = tx.Data;

            var jobj = JObject.Parse(txData);
            var function = (string?)jobj["Function"];

            var scUID = jobj["ContractUID"]?.ToObject<string?>();
            var toAddress = jobj["NextOwner"]?.ToObject<string?>();
            var keySign = jobj["KeySign"]?.ToObject<string?>();
            var amountSoldFor = jobj["SoldFor"]?.ToObject<decimal?>();
            //var locator = jobj["Locators"]?.ToObject<string?>();

            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
            if (scStateTreiRec != null)
            {
                scStateTreiRec.NextOwner = toAddress;
                scStateTreiRec.IsLocked = true;
                scStateTreiRec.Nonce += 1;
                scStateTreiRec.PurchaseAmount = amountSoldFor;
                scStateTreiRec.PurchaseKey = keySign;
                //scStateTreiRec.ContractData = data;
                //scStateTreiRec.Locators = !string.IsNullOrWhiteSpace(locator) ? locator : scStateTreiRec.Locators;
                
                SmartContractStateTrei.UpdateSmartContract(scStateTreiRec);
            }

        }

        private static void CompleteSaleSmartContract(Transaction tx)
        {
            SmartContractStateTrei scST = new SmartContractStateTrei();
            var txData = tx.Data;

            var jobj = JObject.Parse(txData);
            var function = (string?)jobj["Function"];

            var scUID = jobj["ContractUID"]?.ToObject<string?>();
            var royalty = jobj["Royalty"]?.ToObject<bool?>();
            var royaltyAmount = jobj["RoyaltyAmount"]?.ToObject<decimal?>();
            var royaltyPayTo = jobj["RoyaltyPayTo"]?.ToObject<string?>();
            var transactions = jobj["Transactions"]?.ToObject<List<Transaction>?>();
            //var locator = jobj["Locators"]?.ToObject<string?>();

            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
            if (scStateTreiRec != null)
            {
                scStateTreiRec.NextOwner = null;
                scStateTreiRec.IsLocked = false;
                scStateTreiRec.PurchaseAmount = null;
                scStateTreiRec.PurchaseKey = null;
                SmartContractStateTrei.UpdateSmartContract(scStateTreiRec);
            }

            var from = GetSpecificAccountStateTrei(tx.FromAddress);
            if(royalty != null)
            {
                if(royalty.Value)
                {
                    if(transactions != null)
                    {
                        var txToSeller = transactions.Where(x => x.Data.Contains("1/2")).FirstOrDefault();
                        var txToRoyaltyPayee = transactions.Where(x => x.Data.Contains("2/2")).FirstOrDefault();
                    }
                }
                else
                {
                    if (transactions != null)
                    {
                        var txToSeller = transactions.FirstOrDefault();
                    }
                }
            }
            else
            {
                if (transactions != null)
                {
                    var txToSeller = transactions.FirstOrDefault();
                }
            }


        }

    }
}
