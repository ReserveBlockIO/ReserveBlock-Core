using ReserveBlockCore.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.Utilities;
using ReserveBlockCore.Services;
using System.Collections.Concurrent;
using System.Xml.Linq;
using LiteDB;
using System.Net;
using System.Security.Principal;

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
                                        Hash = tx.Hash,
                                        Height = tx.Height,
                                        Data = tx.Data,
                                        Amount = tx.Amount,
                                        Fee = tx.Fee,
                                        Nonce= tx.Nonce,
                                        ReserveTransactionStatus = ReserveTransactionStatus.Pending,
                                        Signature = tx.Signature,
                                        Timestamp = tx.Timestamp,
                                        TransactionType = tx.TransactionType,
                                        UnlockTime = tx.UnlockTime,
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
                        tx.ToAddress != "Reserve_Base" &&
                        tx.ToAddress != "Token_Base")
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
                            string scUID = "";
                            string function = "";
                            bool skip = false;
                            JToken? scData = null;
                            try
                            {
                                var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
                                scData = scDataArray[0];

                                function = (string?)scData["Function"];
                                scUID = (string?)scData["ContractUID"];
                                skip = true;
                            }
                            catch { }

                            try
                            {
                                if (!skip)
                                {
                                    var jobj = JObject.Parse(tx.Data);
                                    scUID = jobj["ContractUID"]?.ToObject<string?>();
                                    function = jobj["Function"]?.ToObject<string?>();
                                }
                            }
                            catch { }

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
                                    case "TokenDeploy()":
                                        DeployTokenContract(tx, block);
                                        break;
                                    case "TokenTransfer()":
                                        TokenTransfer(tx, block);
                                        break;
                                    case "TokenMint()":
                                        TokenMint(tx);
                                        break;
                                    case "TokenBurn()":
                                        TokenBurn(tx);
                                        break;
                                    case "TokenPause()":
                                        TokenPause(tx);
                                        break;
                                    case "TokenBanAddress()":
                                        TokenBanAddress(tx);
                                        break;
                                    case "TokenContractOwnerChange()":
                                        TokenContractOwnerChange(tx);
                                        break;
                                    case "TokenVoteTopicCreate()":
                                        TokenVoteTopicCreate(tx);
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
                                        case "M_Sale_Start()":
                                            StartSaleSmartContract(tx);
                                            break;
                                        case "Sale_Complete()":
                                            CompleteSaleSmartContract(tx, block);
                                            break;
                                        case "M_Sale_Complete()":
                                            CompleteSaleSmartContract(tx, block);
                                            break;
                                        case "Sale_Cancel()":
                                            CancelSaleSmartContract(tx);
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
                                            string recoveryAddress = jobj["RecoveryAddress"].ToObject<string>();
                                            string recoverySigScript = jobj["RecoverySigScript"].ToObject<string>();
                                            RecoverReserveAccountTx(recoveryAddress, tx.FromAddress, block.StateRoot);
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
                    if(rtx.TransactionType == TransactionType.TX)
                    {
                        if (rtx.FromAddress != "Coinbase_TrxFees" && rtx.FromAddress != "Coinbase_BlkRwd" && rtx.ToAddress != "Reserve_Base")
                        {
                            var from = GetSpecificAccountStateTrei(rtx.FromAddress);
                            if (from != null)
                            {
                                from.LockedBalance -= rtx.Amount;
                                accStTrei.UpdateSafe(from);
                            }

                        }

                        if (rtx.ToAddress != "Adnr_Base" &&
                            rtx.ToAddress != "DecShop_Base" &&
                            rtx.ToAddress != "Topic_Base" &&
                            rtx.ToAddress != "Vote_Base" &&
                            rtx.ToAddress != "Reserve_Base" &&
                            rtx.ToAddress != "Token_Base")
                        {
                            var to = GetSpecificAccountStateTrei(rtx.ToAddress);
                            if (rtx.TransactionType == TransactionType.TX)
                            {
                                if (to != null)
                                {
                                    if (rtx.FromAddress.StartsWith("xRBX"))
                                    {
                                        to.Balance += rtx.Amount;
                                        to.LockedBalance -= rtx.Amount;

                                        accStTrei.UpdateSafe(to);
                                    }
                                }
                            }
                        }
                    }
                    if(rtx.TransactionType == TransactionType.NFT_TX)
                    {
                        var scDataArray = JsonConvert.DeserializeObject<JArray>(rtx.Data);
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
                                    
                                    scStateTreiRec.OwnerAddress = rtx.ToAddress;
                                    scStateTreiRec.NextOwner = null;
                                    scStateTreiRec.IsLocked = false;

                                    SmartContractStateTrei.UpdateSmartContract(scStateTreiRec);
                                }
                            }
                        }
                    }

                    var rtxRec = rtxDb.Query().Where(x => x.Id == rtx.Id).FirstOrDefault();
                    var hash = rtx.Hash;

                    if (rtxRec != null)
                    {
                        rtx.ReserveTransactionStatus = ReserveTransactionStatus.Confirmed;
                        rtxDb.UpdateSafe(rtx);
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
                        var rtxDb = ReserveTransactions.GetReserveTransactionsDb();

                        if(rTX.TransactionType == TransactionType.TX)
                        {
                            var stDb = GetAccountStateTrei();
                            var stateTreiFrom = GetSpecificAccountStateTrei(rTX.FromAddress);
                            var stateTreiTo = GetSpecificAccountStateTrei(rTX.ToAddress);

                            if (stateTreiFrom != null)
                            {
                                //return amount to From address
                                stateTreiFrom.LockedBalance -= rTX.Amount;
                                stateTreiFrom.Balance += rTX.Amount;
                                if (stDb != null)
                                    stDb.UpdateSafe(stateTreiFrom);

                                var rLocalAccount = ReserveAccount.GetReserveAccountSingle(stateTreiFrom.Key);
                                if (rLocalAccount != null)
                                {
                                    var rDb = ReserveAccount.GetReserveAccountsDb();
                                    rLocalAccount.LockedBalance -= rTX.Amount;
                                    rLocalAccount.AvailableBalance += rTX.Amount;
                                    if (rDb != null)
                                        rDb.UpdateSafe(rLocalAccount);
                                }
                            }
                            if (stateTreiTo != null)
                            {
                                //remove amount from locked To address
                                stateTreiTo.LockedBalance -= rTX.Amount;
                                if (stDb != null)
                                    stDb.UpdateSafe(stateTreiTo);

                                var localAccount = AccountData.GetSingleAccount(stateTreiTo.Key);
                                if (localAccount != null)
                                {
                                    var accountDB = AccountData.GetAccounts();
                                    localAccount.LockedBalance -= rTX.Amount;
                                    if (accountDB != null)
                                        accountDB.UpdateSafe(localAccount);
                                }

                                var rLocalAccount = ReserveAccount.GetReserveAccountSingle(stateTreiTo.Key);
                                if (rLocalAccount != null)
                                {
                                    var rDb = ReserveAccount.GetReserveAccountsDb();
                                    rLocalAccount.LockedBalance -= rTX.Amount;
                                    if (rDb != null)
                                        rDb.UpdateSafe(rLocalAccount);
                                }
                            }
                        }

                        if(rTX.TransactionType == TransactionType.NFT_TX)
                        {
                            var scDataArray = JsonConvert.DeserializeObject<JArray>(rTX.Data);
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
                        

                        var localTx = TransactionData.GetTxByHash(rTX.Hash);
                        if(localTx != null)
                        {
                            //Change TX status to CalledBack
                            var txDB = Transaction.GetAll();
                            localTx.TransactionStatus = TransactionStatus.CalledBack;
                            rTX.ReserveTransactionStatus = ReserveTransactionStatus.CalledBack;
                            if (txDB != null)
                                txDB.UpdateSafe(localTx);
                        }

                        if (rtxDb != null)
                            rtxDb.UpdateSafe(rTX);
                    }
                }
            }
            catch { }
        }
        private static void RecoverReserveAccountTx(string? _recoveryAddress, string _fromAddress, string stateRoot)
        {
            try
            {
                var stDb = GetAccountStateTrei();
                var rTXList = ReserveTransactions.GetTransactionList(_fromAddress);

                if(rTXList?.Count() > 0)
                {
                    foreach(var rTX in rTXList) 
                    {
                        var rtxDb = ReserveTransactions.GetReserveTransactionsDb();
                        var stateTreiFrom = GetSpecificAccountStateTrei(rTX.FromAddress);
                        if (rTX.TransactionType == TransactionType.TX)
                        {
                            var stateTreiTo = GetSpecificAccountStateTrei(rTX.ToAddress);

                            if (stateTreiFrom != null)
                            {
                                var recoveryAddress = stateTreiFrom.RecoveryAccount;
                                if (recoveryAddress != null)
                                {
                                    stateTreiFrom.LockedBalance -= rTX.Amount;
                                    if (stDb != null)
                                        stDb.UpdateSafe(stateTreiFrom);

                                    var rLocalAccount = ReserveAccount.GetReserveAccountSingle(stateTreiFrom.Key);
                                    if (rLocalAccount != null)
                                    {
                                        var rDb = ReserveAccount.GetReserveAccountsDb();
                                        rLocalAccount.LockedBalance -= rTX.Amount;
                                        if (rDb != null)
                                            rDb.UpdateSafe(rLocalAccount);
                                    }

                                    var stateTreiRecovery = GetSpecificAccountStateTrei(recoveryAddress);
                                    if (stateTreiRecovery != null)
                                    {
                                        stateTreiRecovery.Balance += rTX.Amount;
                                        if (stDb != null)
                                            stDb.UpdateSafe(stateTreiRecovery);
                                    }
                                    else
                                    {
                                        var acctStateTreiTo = new AccountStateTrei
                                        {
                                            Key = recoveryAddress,
                                            Nonce = 0,
                                            Balance = rTX.Amount, //subtract from the address
                                            StateRoot = stateRoot
                                        };

                                        if (stDb != null)
                                            stDb.InsertSafe(acctStateTreiTo);

                                    }

                                    var localAccount = AccountData.GetSingleAccount(recoveryAddress);
                                    if (localAccount != null)
                                    {
                                        var accountDB = AccountData.GetAccounts();
                                        localAccount.Balance += rTX.Amount;
                                        if (accountDB != null)
                                            accountDB.UpdateSafe(localAccount);
                                    }
                                }
                            }

                            if (stateTreiTo != null)
                            {
                                stateTreiTo.LockedBalance -= rTX.Amount;
                                if (stDb != null)
                                    stDb.UpdateSafe(stateTreiTo);
                            }
                        }

                        if (rTX.TransactionType == TransactionType.NFT_TX)
                        {
                            var scDataArray = JsonConvert.DeserializeObject<JArray>(rTX.Data);
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
                                        if (recoveryAddress != null)
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

                        var localTx = TransactionData.GetTxByHash(rTX.Hash);
                        if (localTx != null)
                        {
                            //Change TX status to CalledBack
                            var txDB = Transaction.GetAll();
                            localTx.TransactionStatus = TransactionStatus.Recovered;
                            rTX.ReserveTransactionStatus = ReserveTransactionStatus.Recovered;
                            if (txDB != null)
                                txDB.UpdateSafe(localTx);
                        }

                        if (rtxDb != null)
                            rtxDb.UpdateSafe(rTX);
                    }
                }

                //find current NFTs from the from address reserve and send to recovery
                //find balance for from address reserve and send to recovery
                var rsrvAccount = GetSpecificAccountStateTrei(_fromAddress);
                var _stateTreiRecovery = GetSpecificAccountStateTrei(_recoveryAddress);

                if (_stateTreiRecovery != null)
                {
                    _stateTreiRecovery.Balance += rsrvAccount.Balance;
                    if (stDb != null)
                        stDb.UpdateSafe(_stateTreiRecovery);
                }
                else
                {
                    var acctStateTreiTo = new AccountStateTrei
                    {
                        Key = _recoveryAddress,
                        Nonce = 0,
                        Balance = rsrvAccount.Balance, //subtract from the address
                        StateRoot = stateRoot
                    };

                    if (stDb != null)
                        stDb.InsertSafe(acctStateTreiTo);
                }

                rsrvAccount.Balance = 0.0M;
                rsrvAccount.LockedBalance = 0.0M;

                stDb.UpdateSafe(rsrvAccount);

                var _scDb = SmartContractStateTrei.GetSCST();

                if(_scDb != null)
                {
                    var scList = _scDb.Query().Where(x => x.OwnerAddress == _fromAddress).ToList();
                    if(scList?.Count > 0 ) 
                    {
                        foreach(var sc in scList)
                        {
                            sc.OwnerAddress = _recoveryAddress;
                            sc.NextOwner = null;
                            sc.IsLocked = false;
                            _scDb.UpdateSafe(sc);
                        }
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

        private static void DeployTokenContract(Transaction tx, Block block)
        {
            SmartContractStateTrei scST = new SmartContractStateTrei();
            var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
            var scData = scDataArray[0];
            var stDb = GetAccountStateTrei();
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
                scST.IsToken = true;

                try
                {
                    var sc = SmartContractMain.GenerateSmartContractInMemory(data);
                    if (sc.Features != null)
                    {
                        var tokenFeatures = sc.Features.Where(x => x.FeatureName == FeatureName.Token).Select(x => x.FeatureFeatures).FirstOrDefault();
                        if (tokenFeatures != null)
                        {
                            var tokenFeature = (TokenFeature)tokenFeatures;
                            if(tokenFeature != null)
                            {
                                var tokenDetails = TokenDetails.CreateTokenDetails(tokenFeature, sc);
                                scST.TokenDetails = tokenDetails;

                                if(tokenFeature.TokenSupply > 0)
                                {
                                    var toAddress = GetSpecificAccountStateTrei(sc.MinterAddress);
                                    if(toAddress != null)
                                    {
                                        var tokenAccount = TokenAccount.CreateTokenAccount(sc.SmartContractUID, tokenFeature.TokenName,
                                            tokenFeature.TokenTicker, tokenFeature.TokenSupply, tokenFeature.TokenDecimalPlaces);

                                        if(toAddress.TokenAccounts.Count > 0)
                                        {
                                            toAddress.TokenAccounts.Add(tokenAccount);
                                        }
                                        else
                                        {
                                            List<TokenAccount> tokenAccounts = new List<TokenAccount>
                                            {
                                                tokenAccount
                                            };

                                            toAddress.TokenAccounts = tokenAccounts;
                                        }
                                    }
                                    else
                                    {
                                        var tokenAccount = TokenAccount.CreateTokenAccount(sc.SmartContractUID, tokenFeature.TokenName, 
                                            tokenFeature.TokenTicker, tokenFeature.TokenSupply, tokenFeature.TokenDecimalPlaces);

                                        List<TokenAccount> tokenAccounts = new List<TokenAccount>
                                        {
                                            tokenAccount
                                        };

                                        var acctStateTreiTo = new AccountStateTrei
                                        {
                                            Key = tx.ToAddress,
                                            Nonce = 0,
                                            Balance = 0.0M,
                                            StateRoot = block.StateRoot,
                                            LockedBalance = 0.0M,
                                            TokenAccounts = tokenAccounts
                                        };
                                    }
                                }
                            }
                            
                        }
                    }
                }
                catch { }

                //Save to state trei
                SmartContractStateTrei.SaveSmartContract(scST);
            }
        }
        private static void TokenContractOwnerChange(Transaction tx)
        {
            var txData = tx.Data;
            var jobj = JObject.Parse(txData);
            var function = (string?)jobj["Function"];

            var scUID = jobj["ContractUID"]?.ToObject<string?>();
            var toAddress = jobj["ToAddress"]?.ToObject<string?>();
            var fromAddress = jobj["FromAddress"]?.ToObject<string?>();

            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
            if (scStateTreiRec != null)
            {
                if (scStateTreiRec.TokenDetails != null)
                {
                    scStateTreiRec.TokenDetails.ContractOwner = toAddress;
                    scStateTreiRec.OwnerAddress = toAddress;
                    SmartContractStateTrei.UpdateSmartContract(scStateTreiRec);
                }
            }
        }

        private static void TokenVoteTopicCreate(Transaction tx)
        {
            var txData = tx.Data;
            var jobj = JObject.Parse(txData);
            var function = (string?)jobj["Function"];

            var scUID = jobj["ContractUID"]?.ToObject<string?>();
            var fromAddress = jobj["FromAddress"]?.ToObject<string?>();
            var topic = jobj["TokenVoteTopic"]?.ToObject<TokenVoteTopic?>();

            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
            if (scStateTreiRec != null)
            {
                if (scStateTreiRec.TokenDetails != null)
                {
                    var topicList = scStateTreiRec.TokenDetails.TokenTopicList;
                    if (topicList?.Count > 0)
                    {
                        var exist = scStateTreiRec.TokenDetails.TokenTopicList.Exists(x => x.TopicUID == topic.TopicUID);
                        if (!exist)
                        {
                            scStateTreiRec.TokenDetails.TokenTopicList.Add(topic);
                        }
                    }
                    else
                    {
                        scStateTreiRec.TokenDetails.TokenTopicList = new List<TokenVoteTopic> { topic };
                    }
                    SmartContractStateTrei.UpdateSmartContract(scStateTreiRec);
                }
            }
        }

        private static void TokenVoteTopicCast(Transaction tx)
        {
            var txData = tx.Data;
            var jobj = JObject.Parse(txData);
            var function = (string?)jobj["Function"];

            var scUID = jobj["ContractUID"]?.ToObject<string?>();
            var fromAddress = jobj["FromAddress"]?.ToObject<string?>();
            var topicUID = jobj["TopicUID"]?.ToObject<string?>();
            var voteType = jobj["TopicUID"]?.ToObject<VoteType?>();

            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
            if (scStateTreiRec != null)
            {
                if (scStateTreiRec.TokenDetails != null)
                {
                    var topicList = scStateTreiRec.TokenDetails.TokenTopicList;
                    if (topicList?.Count > 0)
                    {
                        var topic = scStateTreiRec.TokenDetails.TokenTopicList.Where(x => x.TopicUID == topicUID).FirstOrDefault();
                        if (topic != null)
                        {
                            if (voteType == VoteType.Yes)
                                topic.VoteYes += 1;
                            if (voteType == VoteType.No)
                                topic.VoteNo += 1;

                            int fromIndex = scStateTreiRec.TokenDetails.TokenTopicList.FindIndex(a => a.TopicUID == topicUID);
                            scStateTreiRec.TokenDetails.TokenTopicList[fromIndex] = topic;
                            SmartContractStateTrei.UpdateSmartContract(scStateTreiRec);
                        }
                    }
                }
            }
        }
        private static void TokenBanAddress(Transaction tx)
        {
            var txData = tx.Data;
            var jobj = JObject.Parse(txData);

            var function = (string?)jobj["Function"];

            var scUID = jobj["ContractUID"]?.ToObject<string?>();
            var banAddress = jobj["BanAddress"]?.ToObject<string?>();
            var fromAddress = jobj["FromAddress"]?.ToObject<string?>();

            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
            if (scStateTreiRec != null)
            {
                if (scStateTreiRec.TokenDetails != null)
                {
                    var banList = scStateTreiRec.TokenDetails.AddressBlackList;
                    if(banList?.Count > 0)
                    {
                        var exist = scStateTreiRec.TokenDetails.AddressBlackList.Exists(x => x == banAddress);
                        if(!exist)
                        {
                            scStateTreiRec.TokenDetails.AddressBlackList.Add(banAddress);
                        }
                    }
                    else
                    {
                        scStateTreiRec.TokenDetails.AddressBlackList = new List<string> { banAddress };
                    }
                    SmartContractStateTrei.UpdateSmartContract(scStateTreiRec);
                }
            }

        }
        private static void TokenPause(Transaction tx)
        {
            var txData = tx.Data;
            var jobj = JObject.Parse(txData);
            
            var function = (string?)jobj["Function"];

            var scUID = jobj["ContractUID"]?.ToObject<string?>();
            var pause = jobj["Pause"]?.ToObject<bool?>();
            var fromAddress = jobj["FromAddress"]?.ToObject<string?>();

            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
            if (scStateTreiRec != null)
            {
                if (scStateTreiRec.TokenDetails != null)
                {
                    scStateTreiRec.TokenDetails.IsPaused = !scStateTreiRec.TokenDetails.IsPaused;
                    SmartContractStateTrei.UpdateSmartContract(scStateTreiRec);
                }
            }

        }
        private static void TokenMint(Transaction tx)
        {
            SmartContractStateTrei scST = new SmartContractStateTrei();
            var txData = tx.Data;
            var stDB = GetAccountStateTrei();

            var jobj = JObject.Parse(txData);

            var function = (string?)jobj["Function"];
            var scUID = jobj["ContractUID"]?.ToObject<string?>();
            var amount = jobj["Amount"]?.ToObject<decimal?>();
            var fromAddress = jobj["FromAddress"]?.ToObject<string?>();

            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
            if (scStateTreiRec != null)
            {
                if (scStateTreiRec.TokenDetails != null)
                {
                    var fromAccount = GetSpecificAccountStateTrei(fromAddress);
                    var tokenAccountFrom = fromAccount.TokenAccounts?.Where(x => x.SmartContractUID == scUID).FirstOrDefault();
                    if (tokenAccountFrom != null)
                    {
                        tokenAccountFrom.Balance += amount.Value;
                        int fromIndex = fromAccount.TokenAccounts.FindIndex(a => a.SmartContractUID == scUID);
                        fromAccount.TokenAccounts[fromIndex] = tokenAccountFrom;
                        stDB.UpdateSafe(fromAccount);
                    }
                    else
                    {
                        var nTokenAccountT0 = TokenAccount.CreateTokenAccount(scUID, scStateTreiRec.TokenDetails.TokenName, scStateTreiRec.TokenDetails.TokenTicker,
                            amount.Value, scStateTreiRec.TokenDetails.DecimalPlaces);

                        if (fromAccount.TokenAccounts == null)
                        {
                            List<TokenAccount> tokenAccounts = new List<TokenAccount>
                            {
                                nTokenAccountT0
                            };

                            fromAccount.TokenAccounts = tokenAccounts;
                            stDB.UpdateSafe(fromAccount);
                        }
                        else
                        {
                            fromAccount.TokenAccounts.Add(nTokenAccountT0);
                            stDB.UpdateSafe(fromAccount);
                        }
                    }
                    scStateTreiRec.TokenDetails.CurrentSupply += amount.Value;
                    SmartContractStateTrei.UpdateSmartContract(scStateTreiRec);
                }
            }

        }
        private static void TokenTransfer(Transaction tx, Block block)
        {
            SmartContractStateTrei scST = new SmartContractStateTrei();
            var txData = tx.Data;
            var stDB = GetAccountStateTrei();

            var jobj = JObject.Parse(txData);

            var function = (string?)jobj["Function"];

            var scUID = jobj["ContractUID"]?.ToObject<string?>();
            var toAddress = jobj["ToAddress"]?.ToObject<string?>();
            var amount = jobj["Amount"]?.ToObject<decimal?>();
            var fromAddress = jobj["FromAddress"]?.ToObject<string?>();

            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
            if (scStateTreiRec != null)
            {
                if(scStateTreiRec.TokenDetails != null)
                {
                    var toAccount = GetSpecificAccountStateTrei(toAddress);
                    var fromAccount = GetSpecificAccountStateTrei(fromAddress);

                    if(toAccount == null)
                    {
                        var accStTrei = GetAccountStateTrei();
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
                        toAccount = acctStateTreiTo;
                    }

                    var tokenAccountFrom = fromAccount.TokenAccounts?.Where(x => x.SmartContractUID == scUID).FirstOrDefault();
                    if(tokenAccountFrom != null)
                    {
                        tokenAccountFrom.Balance -= amount.Value;
                        int fromIndex = fromAccount.TokenAccounts.FindIndex(a => a.SmartContractUID == scUID);
                        fromAccount.TokenAccounts[fromIndex] = tokenAccountFrom;
                        stDB.UpdateSafe(fromAccount);
                    }

                    var tokenAccountTo = toAccount.TokenAccounts?.Where(x => x.SmartContractUID == scUID).FirstOrDefault();
                    if(tokenAccountTo == null)
                    {
                        var nTokenAccountT0 = TokenAccount.CreateTokenAccount(scUID, scStateTreiRec.TokenDetails.TokenName, scStateTreiRec.TokenDetails.TokenTicker, 
                            amount.Value, scStateTreiRec.TokenDetails.DecimalPlaces);

                        if(toAccount.TokenAccounts == null)
                        {
                            List<TokenAccount> tokenAccounts = new List<TokenAccount>
                            {
                                nTokenAccountT0
                            };

                            toAccount.TokenAccounts = tokenAccounts;
                        }
                        else
                        {
                            toAccount.TokenAccounts.Add(nTokenAccountT0);
                            stDB.UpdateSafe(toAccount);
                        }
                    }
                    else
                    {
                        tokenAccountTo.Balance += amount.Value;
                        int toIndex = toAccount.TokenAccounts.FindIndex(a => a.SmartContractUID == scUID);
                        toAccount.TokenAccounts[toIndex] = tokenAccountTo;
                    }
                    stDB.UpdateSafe(toAccount);
                }
                

                //if (tx.FromAddress.StartsWith("xRBX"))
                //{
                //    scStateTreiRec.NextOwner = tx.ToAddress;
                //    scStateTreiRec.IsLocked = true;
                //    scStateTreiRec.Nonce += 1;
                //    scStateTreiRec.ContractData = data;
                //    scStateTreiRec.Locators = !string.IsNullOrWhiteSpace(locator) ? locator : scStateTreiRec.Locators;
                //}
                //else
                //{
                //    scStateTreiRec.OwnerAddress = tx.ToAddress;
                //    scStateTreiRec.Nonce += 1;
                //    scStateTreiRec.ContractData = data;
                //    scStateTreiRec.Locators = !string.IsNullOrWhiteSpace(locator) ? locator : scStateTreiRec.Locators;
                //}
            }

        }

        private static void TokenBurn(Transaction tx)
        {
            SmartContractStateTrei scST = new SmartContractStateTrei();
            var txData = tx.Data;
            var stDB = GetAccountStateTrei();

            var jobj = JObject.Parse(txData);

            var function = (string?)jobj["Function"];

            var scUID = jobj["ContractUID"]?.ToObject<string?>();
            var amount = jobj["Amount"]?.ToObject<decimal?>();
            var fromAddress = jobj["FromAddress"]?.ToObject<string?>();

            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
            if (scStateTreiRec != null)
            {
                if (scStateTreiRec.TokenDetails != null)
                {
                    var fromAccount = GetSpecificAccountStateTrei(fromAddress);

                    var tokenAccountFrom = fromAccount.TokenAccounts?.Where(x => x.SmartContractUID == scUID).FirstOrDefault();
                    if (tokenAccountFrom != null)
                    {
                        tokenAccountFrom.Balance -= amount.Value;
                        int fromIndex = fromAccount.TokenAccounts.FindIndex(a => a.SmartContractUID == scUID);
                        fromAccount.TokenAccounts[fromIndex] = tokenAccountFrom;
                        stDB.UpdateSafe(fromAccount);
                    }

                    scStateTreiRec.TokenDetails.CurrentSupply -= amount.Value;

                    SmartContractStateTrei.UpdateSmartContract(scStateTreiRec);
                }
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
            var amountSoldFor = jobj["SoldFor"]?.ToObject<decimal?>();
            var locator = jobj["Locators"]?.ToObject<string?>();
            //var locator = jobj["Locators"]?.ToObject<string?>();

            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
            if (scStateTreiRec != null)
            {
                scStateTreiRec.NextOwner = toAddress;
                scStateTreiRec.IsLocked = true;
                scStateTreiRec.Nonce += 1;
                scStateTreiRec.PurchaseAmount = amountSoldFor;
                scStateTreiRec.Locators = !string.IsNullOrWhiteSpace(locator) ? locator : scStateTreiRec.Locators;

                SmartContractStateTrei.UpdateSmartContract(scStateTreiRec);
            }

        }

        private static void CompleteSaleSmartContract(Transaction tx, Block block)
        {
            SmartContractStateTrei scST = new SmartContractStateTrei();
            var accStTrei = GetAccountStateTrei();

            var txData = tx.Data;

            var jobj = JObject.Parse(txData);
            var function = (string?)jobj["Function"];

            var scUID = jobj["ContractUID"]?.ToObject<string?>();
            var royalty = jobj["Royalty"]?.ToObject<bool?>();
            var royaltyAmount = jobj["RoyaltyAmount"]?.ToObject<decimal?>();
            var royaltyPayTo = jobj["RoyaltyPayTo"]?.ToObject<string?>();
            var transactions = jobj["Transactions"]?.ToObject<List<Transaction>?>();
            var keySign = jobj["KeySign"]?.ToObject<string?>();

            //var locator = jobj["Locators"]?.ToObject<string?>();

            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
            if (scStateTreiRec != null)
            {
                scStateTreiRec.NextOwner = null;
                scStateTreiRec.IsLocked = false;
                scStateTreiRec.PurchaseAmount = null;
                scStateTreiRec.OwnerAddress = tx.FromAddress;
                if (scStateTreiRec.PurchaseKeys != null)
                {
                    scStateTreiRec.PurchaseKeys.Add(keySign);
                }
                else
                {
                    scStateTreiRec.PurchaseKeys = new List<string> { keySign };
                }

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
                        if(txToSeller != null)
                        {
                            var toSeller = GetSpecificAccountStateTrei(txToSeller.ToAddress);
                            if (toSeller == null)
                            {
                                var acctStateTreiTo = new AccountStateTrei
                                {
                                    Key = txToSeller.ToAddress,
                                    Nonce = 0,
                                    Balance = 0.0M,
                                    StateRoot = block.StateRoot
                                };

                                acctStateTreiTo.Balance += txToSeller.Amount;
                                accStTrei.InsertSafe(acctStateTreiTo);
                            }
                            else
                            {
                                toSeller.StateRoot = block.StateRoot;
                                toSeller.Balance += txToSeller.Amount;
                                
                                accStTrei.UpdateSafe(toSeller);
                            }

                            from.Nonce += 1;
                            from.StateRoot = block.StateRoot;
                            from.Balance -= (txToSeller.Amount + txToSeller.Fee);

                            accStTrei.UpdateSafe(from);
                        }
                        if (txToRoyaltyPayee != null)
                        {
                            var toRoyalty = GetSpecificAccountStateTrei(txToRoyaltyPayee.ToAddress);
                            if (toRoyalty == null)
                            {
                                var acctStateTreiTo = new AccountStateTrei
                                {
                                    Key = txToRoyaltyPayee.ToAddress,
                                    Nonce = 0,
                                    Balance = 0.0M,
                                    StateRoot = block.StateRoot
                                };

                                acctStateTreiTo.Balance += txToRoyaltyPayee.Amount;
                                accStTrei.InsertSafe(acctStateTreiTo);
                            }
                            else
                            {
                                toRoyalty.StateRoot = block.StateRoot;
                                toRoyalty.Balance += txToRoyaltyPayee.Amount;

                                accStTrei.UpdateSafe(toRoyalty);
                            }

                            from.Nonce += 1;
                            from.StateRoot = block.StateRoot;
                            from.Balance -= (txToRoyaltyPayee.Amount + txToRoyaltyPayee.Fee);

                            accStTrei.UpdateSafe(from);
                        }

                    }
                }
                else
                {
                    if (transactions != null)
                    {
                        var txToSeller = transactions.FirstOrDefault();
                        if (txToSeller != null)
                        {
                            var toSeller = GetSpecificAccountStateTrei(txToSeller.ToAddress);
                            if (toSeller == null)
                            {
                                var acctStateTreiTo = new AccountStateTrei
                                {
                                    Key = txToSeller.ToAddress,
                                    Nonce = 0,
                                    Balance = 0.0M,
                                    StateRoot = block.StateRoot
                                };

                                acctStateTreiTo.Balance += txToSeller.Amount;
                                accStTrei.InsertSafe(acctStateTreiTo);
                            }
                            else
                            {
                                toSeller.StateRoot = block.StateRoot;
                                toSeller.Balance += txToSeller.Amount;

                                accStTrei.UpdateSafe(toSeller);
                            }

                            from.Nonce += 1;
                            from.StateRoot = block.StateRoot;
                            from.Balance -= (txToSeller.Amount + txToSeller.Fee);

                            accStTrei.UpdateSafe(from);
                        }

                    }
                }
            }
            else
            {
                if (transactions != null)
                {
                    var txToSeller = transactions.FirstOrDefault();
                    if (txToSeller != null)
                    {
                        var toSeller = GetSpecificAccountStateTrei(txToSeller.ToAddress);
                        if (toSeller == null)
                        {
                            var acctStateTreiTo = new AccountStateTrei
                            {
                                Key = txToSeller.ToAddress,
                                Nonce = 0,
                                Balance = 0.0M,
                                StateRoot = block.StateRoot
                            };

                            acctStateTreiTo.Balance += txToSeller.Amount;
                            accStTrei.InsertSafe(acctStateTreiTo);
                        }
                        else
                        {
                            toSeller.StateRoot = block.StateRoot;
                            toSeller.Balance += txToSeller.Amount;

                            accStTrei.UpdateSafe(toSeller);
                        }

                        from.Nonce += 1;
                        from.StateRoot = block.StateRoot;
                        from.Balance -= (txToSeller.Amount + txToSeller.Fee);

                        accStTrei.UpdateSafe(from);
                    }
                }
            }
        }

        private static void CancelSaleSmartContract(Transaction tx)
        {
            SmartContractStateTrei scST = new SmartContractStateTrei();
            var txData = tx.Data;

            var jobj = JObject.Parse(txData);
            var function = (string?)jobj["Function"];

            var scUID = jobj["ContractUID"]?.ToObject<string?>();

            var scStateTreiRec = SmartContractStateTrei.GetSmartContractState(scUID);
            if (scStateTreiRec != null)
            {
                scStateTreiRec.NextOwner = null;
                scStateTreiRec.IsLocked = false;
                scStateTreiRec.Nonce += 1;
                scStateTreiRec.PurchaseAmount = null;

                SmartContractStateTrei.UpdateSmartContract(scStateTreiRec);
            }

        }

    }
}
