using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReserveBlockCore.Utilities;
using ReserveBlockCore.Models;
using ReserveBlockCore.Extensions;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.Services;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace ReserveBlockCore.Data
{
    internal class TransactionData
    {
        public static bool GenesisTransactionsCreated = false;
        public static void CreateGenesisTransction()
        {
            if (GenesisTransactionsCreated != true)
            {
                var trxPool = TransactionData.GetPool();
                trxPool.DeleteAllSafe();
                var timeStamp = TimeUtil.GetTime();

                var balanceSheet = GenesisBalanceUtility.GenesisBalances();
                foreach(var item in balanceSheet)
                {
                    var addr = item.Key;
                    var balance = item.Value;
                    var gTrx = new Transaction
                    {
                        Amount = balance,
                        Height = 0,
                        FromAddress = "rbx_genesis_transaction",
                        ToAddress = addr,
                        Fee = 0,
                        Hash = "", //this will be built down below. showing just to make this clear.
                        Timestamp = timeStamp,
                        Signature = "COINBASE_TX",
                        TransactionType = TransactionType.TX,
                        Nonce = 0
                    };

                    gTrx.Build();

                    AddToPool(gTrx);

                }

            }

        }
        public static void AddTxToWallet(Transaction transaction, bool subtract = false)
        {
            var txs = GetAll();
            var txCheck = txs.FindOne(x => x.Hash == transaction.Hash);
            if(txCheck== null)
            {
                Transaction tx = new Transaction { 
                    Height = transaction.Height,
                    Hash = transaction.Hash,
                    Amount = transaction.Amount,
                    FromAddress = transaction.FromAddress,
                    ToAddress = transaction.ToAddress,
                    Fee = transaction.Fee,
                    Data = transaction.Data,
                    Nonce = transaction.Nonce,
                    Signature = transaction.Signature,
                    Timestamp = transaction.Timestamp,
                    TransactionRating = transaction.TransactionRating,
                    TransactionStatus = transaction.TransactionStatus,
                    TransactionType = transaction.TransactionType,
                    UnlockTime = transaction.UnlockTime
                };
                if (subtract)
                {
                    tx.Amount = (tx.Amount * -1M);
                    tx.Fee = (tx.Fee * -1M);
                }
                    
                txs.InsertSafe(tx);
            }
        }

        public static void UpdateTxStatusAndHeightXXXX(Transaction transaction, TransactionStatus txStatus, long blockHeight, bool sameWalletTX = false, bool isReserveSend = false)
        {
            var txs = GetAll();
            var txCheck = txs.FindOne(x => x.Hash == transaction.Hash);
            if(!sameWalletTX)
            {
                if (txCheck == null)
                {
                    //posible sub needed
                    transaction.Id = new LiteDB.ObjectId();
                    transaction.TransactionStatus = txStatus;
                    transaction.Height = blockHeight;
                    txs.InsertSafe(transaction);
                    var account = AccountData.GetSingleAccount(transaction.FromAddress);
                    if (account != null)
                    {
                        var accountDb = AccountData.GetAccounts();
                        var stateTrei = StateData.GetSpecificAccountStateTrei(account.Address);
                        if (stateTrei != null)
                        {
                            account.Balance = stateTrei.Balance;
                            accountDb.UpdateSafe(account);
                        }
                    }
                }
                else
                {
                    txCheck.TransactionStatus = txStatus;
                    txCheck.Height = blockHeight;
                    txs.UpdateSafe(txCheck);
                }
            }
            else
            {
                if(txCheck != null)
                {
                    if(txCheck.Amount < 0)
                    {
                        transaction.Id = new LiteDB.ObjectId();
                        transaction.TransactionStatus = txStatus;
                        transaction.Height = blockHeight;
                        transaction.Amount = transaction.Amount < 0 ? transaction.Amount * -1.0M : transaction.Amount;
                        transaction.Fee = transaction.Fee < 0 ? transaction.Fee * -1.0M : transaction.Fee;
                        txs.InsertSafe(transaction);

                        var account = AccountData.GetSingleAccount(transaction.FromAddress);
                        if (account != null)
                        {
                            var accountDb = AccountData.GetAccounts();
                            var stateTrei = StateData.GetSpecificAccountStateTrei(account.Address);
                            if (stateTrei != null)
                            {
                                account.Balance = stateTrei.Balance;
                                accountDb.UpdateSafe(account);
                            }
                        }
                    }
                }
            }
            
        }

        public static void UpdateTxStatusAndHeight(Transaction transaction, TransactionStatus txStatus, long blockHeight, bool sameWalletTX = false)
        {
            var txs = GetAll();
            var txCheck = txs.FindOne(x => x.Hash == transaction.Hash);
            if (!sameWalletTX)
            {
                if (txCheck == null)
                {
                    //posible sub needed
                    transaction.Id = new LiteDB.ObjectId();
                    transaction.TransactionStatus = txStatus;
                    transaction.Height = blockHeight;
                    txs.InsertSafe(transaction);
                    var account = AccountData.GetSingleAccount(transaction.FromAddress);
                    var rAccount = ReserveAccount.GetReserveAccountSingle(transaction.FromAddress);
                    if (account != null)
                    {
                        var accountDb = AccountData.GetAccounts();
                        var stateTrei = StateData.GetSpecificAccountStateTrei(account.Address);
                        if (stateTrei != null)
                        {
                            account.Balance = stateTrei.Balance;
                            accountDb.UpdateSafe(account);
                        }
                    }
                    if(rAccount != null)
                    {
                        var stateTrei = StateData.GetSpecificAccountStateTrei(rAccount.Address);
                        if (stateTrei != null)
                        {
                            rAccount.AvailableBalance = stateTrei.Balance;
                            rAccount.LockedBalance = stateTrei.LockedBalance;
                            ReserveAccount.SaveReserveAccount(rAccount);
                        }
                    }
                }
                else
                {
                    txCheck.TransactionStatus = txStatus;
                    txCheck.Height = blockHeight;
                    txs.UpdateSafe(txCheck);
                }
            }
            else
            {
                if (txCheck != null)
                {
                    if (txCheck.Amount < 0)
                    {
                        transaction.Id = new LiteDB.ObjectId();
                        transaction.TransactionStatus = txStatus;
                        transaction.Height = blockHeight;
                        transaction.Amount = transaction.Amount < 0 ? transaction.Amount * -1.0M : transaction.Amount;
                        transaction.Fee = transaction.Fee < 0 ? transaction.Fee * -1.0M : transaction.Fee;
                        txs.InsertSafe(transaction);

                        var account = AccountData.GetSingleAccount(transaction.FromAddress);
                        var rAccount = ReserveAccount.GetReserveAccountSingle(transaction.FromAddress);

                        if (account != null)
                        {
                            var accountDb = AccountData.GetAccounts();
                            var stateTrei = StateData.GetSpecificAccountStateTrei(account.Address);
                            if (stateTrei != null)
                            {
                                account.Balance = stateTrei.Balance;
                                accountDb.UpdateSafe(account);
                            }
                        }

                        if (rAccount != null)
                        {
                            var stateTrei = StateData.GetSpecificAccountStateTrei(rAccount.Address);
                            if (stateTrei != null)
                            {
                                rAccount.AvailableBalance = stateTrei.Balance;
                                rAccount.LockedBalance = stateTrei.LockedBalance;
                                ReserveAccount.SaveReserveAccount(rAccount);
                            }
                        }
                    }
                }
            }

        }

        public static async Task UpdateWalletTXTask()
        {
            var txs = GetAll();
            var txList = txs.Find(x => x.TransactionStatus == TransactionStatus.Pending).ToList();
            foreach(var tx in txList)
            {
                try
                {
                    var isTXCrafted = await HasTxBeenCraftedIntoBlock(tx);
                    if (isTXCrafted)
                    {
                        tx.TransactionStatus = TransactionStatus.Success;
                        txs.UpdateSafe(tx);
                    }
                    else
                    {
                        var isStale = await IsTxTimestampStale(tx);
                        if (isStale)
                        {
                            tx.TransactionStatus = TransactionStatus.Failed;
                            txs.UpdateSafe(tx);
                            var account = AccountData.GetSingleAccount(tx.FromAddress);
                            if (account != null)
                            {
                                var accountDb = AccountData.GetAccounts();
                                var stateTrei = StateData.GetSpecificAccountStateTrei(account.Address);
                                if (stateTrei != null)
                                {
                                    account.Balance = stateTrei.Balance;
                                    accountDb.UpdateSafe(account);
                                }
                            }
                        }

                    }
                }
                catch (Exception ex)
                {
                    ErrorLogUtility.LogError($"Unknown Error: {ex.ToString()}", "TransactionData.UpdateWalletTXTask()");
                }
            }
        }

        public static async Task<bool> HasTxBeenCraftedIntoBlock(Transaction tx)
        {
            var result = false;
            
            if (Globals.MemBlocks.Any())
            {
                var txExist = Globals.MemBlocks.ContainsKey(tx.Hash);
                if (txExist == true)
                {
                    result = true;
                }
            }
            return result;
        }

        public static async Task<bool> IsTxTimestampStale(Transaction tx)
        {
            var result = false;

            var currentTime = TimeUtil.GetTime();
            var timeDiff = currentTime - tx.Timestamp;
            var minuteDiff = timeDiff / 60M;

            if (minuteDiff > 60.0M)
            {
                result = true;
            }

            return result;
        }

        public static void AddToPool(Transaction transaction)
        {
            var TransactionPool = GetPool();
            TransactionPool.InsertSafe(transaction);
        }

        public static LiteDB.ILiteCollection<Transaction> GetPool()
        {
            try
            {
                var collection = DbContext.DB_Mempool.GetCollection<Transaction>(DbContext.RSRV_TRANSACTION_POOL);
                return collection;
            }
            catch(Exception ex)
            {
                DbContext.Rollback("TransactionData.GetPool()");
                return null;
            }
            
        }
        public static void PrintMemPool()
        {
            var pool = GetPool();
            if(pool.Count() != 0)
            {
                var txs = pool.FindAll().ToList();
                foreach(var tx in txs)
                {
                    var rating = tx.TransactionRating != null ? tx.TransactionRating.ToString() : "NA";
                    var txString = "From: " + tx.FromAddress + " | To: " + tx.ToAddress + " | Amount: " + tx.Amount.ToString() + " | Fee: " + tx.Fee.ToString()
                        + " | TX ID: " + tx.Hash + " | Timestamp: " + tx.Timestamp.ToString() + " | Rating: " + rating;
                    Console.WriteLine(txString);
                }
            }
            else
            {
                Console.WriteLine("No Transactions in your mempool");
            }
        }
        public static List<Transaction>? GetMempool()
        {
            var pool = GetPool();
            if (pool.Count() != 0)
            {
                var txs = pool.FindAll().ToList();
                if(txs.Count() != 0)
                {
                    return txs;
                }
            }
            else
            {
                return null;
            }

            return null;
        }

        public static async Task<List<Transaction>> ProcessTxPool()
        {
            var collection = DbContext.DB_Mempool.GetCollection<Transaction>(DbContext.RSRV_TRANSACTION_POOL);

            var memPoolTxList = collection.FindAll().ToList();
            //Size the pool to 1mb
            var sizedMempoolList = MempoolSizeUtility.SizeMempoolDown(memPoolTxList);

            var approvedMemPoolList = new List<Transaction>();

            var adnrNameList = new List<string>();

            if(sizedMempoolList.Count() > 0)
            {
                sizedMempoolList.ForEach(async tx =>
                {
                    try
                    {
                        var txExist = approvedMemPoolList.Exists(x => x.Hash == tx.Hash);
                        if (!txExist)
                        {
                            var reject = false;
                            if (tx.TransactionType != TransactionType.TX &&
                                tx.TransactionType != TransactionType.ADNR &&
                                tx.TransactionType != TransactionType.VOTE_TOPIC &&
                                tx.TransactionType != TransactionType.VOTE && 
                                tx.TransactionType != TransactionType.DSTR &&
                                tx.TransactionType != TransactionType.RESERVE &&
                                tx.TransactionType != TransactionType.NFT_SALE)
                            {
                                var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
                                if (scDataArray != null)
                                {
                                    var scData = scDataArray[0];

                                    var function = (string?)scData["Function"];
                                    var scUID = (string?)scData["ContractUID"];
                                    if (!string.IsNullOrWhiteSpace(function))
                                    {
                                        var otherTxs = approvedMemPoolList.Where(x => x.FromAddress == tx.FromAddress && x.Hash != tx.Hash).ToList();
                                        if (otherTxs.Count() > 0)
                                        {
                                            foreach (var otx in otherTxs)
                                            {
                                                if (otx.TransactionType == TransactionType.NFT_TX ||
                                                otx.TransactionType == TransactionType.NFT_BURN ||
                                                otx.TransactionType == TransactionType.NFT_MINT)
                                                {
                                                    if (otx.Data != null)
                                                    {
                                                        var ottxDataArray = JsonConvert.DeserializeObject<JArray>(otx.Data);
                                                        if (ottxDataArray != null)
                                                        {
                                                            var ottxData = ottxDataArray[0];

                                                            var ottxFunction = (string?)ottxData["Function"];
                                                            var ottxscUID = (string?)ottxData["ContractUID"];
                                                            if (!string.IsNullOrWhiteSpace(ottxFunction))
                                                            {
                                                                if (ottxscUID == scUID)
                                                                {
                                                                    reject = true;
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
                                var jobj = JObject.Parse(tx.Data);
                                if (jobj != null)
                                {
                                    var function = (string)jobj["Function"];
                                    if (!string.IsNullOrWhiteSpace(function))
                                    {
                                        var name = (string?)jobj["Name"];
                                        if (!string.IsNullOrWhiteSpace(name))
                                        {
                                            if (adnrNameList.Contains(name.ToLower()))
                                            {
                                                reject = true;
                                            }
                                            else
                                            {
                                                adnrNameList.Add(name.ToLower());
                                            }
                                        }
                                    }
                                }
                            }

                            if(tx.TransactionType == TransactionType.VOTE_TOPIC)
                            {
                                var signature = tx.Signature;
                                //the signature must be checked here to ensure someone isn't spamming bad TXs to invalidated votes/vote topics
                                var sigCheck = SignatureService.VerifySignature(tx.FromAddress, tx.Hash, signature);
                                if (sigCheck)
                                {
                                    var topicAlreadyExist = approvedMemPoolList.Exists(x => x.FromAddress == tx.FromAddress && x.TransactionType == TransactionType.VOTE_TOPIC);
                                    if (topicAlreadyExist)
                                        reject = true;
                                }
                            }

                            if (tx.TransactionType == TransactionType.VOTE)
                            {
                                var signature = tx.Signature;
                                //the signature must be checked here to ensure someone isn't spamming bad TXs to invalidated votes/vote topics
                                var sigCheck = SignatureService.VerifySignature(tx.FromAddress, tx.Hash, signature);
                                if (sigCheck)
                                {
                                    var topicAlreadyExist = approvedMemPoolList.Exists(x => x.FromAddress == tx.FromAddress && x.TransactionType == TransactionType.VOTE);
                                    if (topicAlreadyExist)
                                        reject = true;
                                }
                            }

                            if (tx.TransactionType == TransactionType.DSTR)
                            {
                                var signature = tx.Signature;
                                //the signature must be checked here to ensure someone isn't spamming bad TXs to invalidated votes/vote topics
                                var sigCheck = SignatureService.VerifySignature(tx.FromAddress, tx.Hash, signature);
                                if (sigCheck)
                                {
                                    var topicAlreadyExist = approvedMemPoolList.Exists(x => x.FromAddress == tx.FromAddress && x.TransactionType == TransactionType.DSTR);
                                    if (topicAlreadyExist)
                                        reject = true;
                                }
                            }

                            if (reject == false)
                            {
                                var signature = tx.Signature;
                                var sigCheck = SignatureService.VerifySignature(tx.FromAddress, tx.Hash, signature);
                                if (sigCheck)
                                {
                                    var balance = AccountStateTrei.GetAccountBalance(tx.FromAddress);

                                    var totalSend = (tx.Amount + tx.Fee);
                                    if (balance >= totalSend)
                                    {
                                        var dblspndChk = await DoubleSpendReplayCheck(tx);
                                        var isCraftedIntoBlock = await HasTxBeenCraftedIntoBlock(tx);
                                        var txVerify = await TransactionValidatorService.VerifyTX(tx);

                                        if (txVerify.Item1 && !dblspndChk && !isCraftedIntoBlock)
                                            approvedMemPoolList.Add(tx);
                                    }
                                    else
                                    {
                                        var txToDelete = collection.FindOne(t => t.Hash == tx.Hash);
                                        if (txToDelete != null)
                                        {
                                            try
                                            {
                                                collection.DeleteManySafe(x => x.Hash == txToDelete.Hash);
                                            }
                                            catch (Exception ex)
                                            {
                                                DbContext.Rollback("TransactionData.ProcessTxPool()");
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    var txToDelete = collection.FindOne(t => t.Hash == tx.Hash);
                                    if (txToDelete != null)
                                    {
                                        try
                                        {
                                            collection.DeleteManySafe(x => x.Hash == txToDelete.Hash);
                                        }
                                        catch (Exception ex)
                                        {
                                            DbContext.Rollback("TransactionData.ProcessTxPool()-2");
                                        }
                                    }
                                }
                            }
                            else
                            {
                                var txToDelete = collection.FindOne(t => t.Hash == tx.Hash);
                                if (txToDelete != null)
                                {
                                    try
                                    {
                                        collection.DeleteManySafe(x => x.Hash == txToDelete.Hash);
                                    }
                                    catch (Exception ex)
                                    {
                                        DbContext.Rollback("TransactionData.ProcessTxPool()-3");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        var txToDelete = collection.FindOne(t => t.Hash == tx.Hash);
                        if (txToDelete != null)
                        {
                            try
                            {
                                collection.DeleteManySafe(x => x.Hash == txToDelete.Hash);
                            }
                            catch (Exception ex2)
                            {
                                DbContext.Rollback("TransactionData.ProcessTxPool()-4");
                            }
                        }
                    }
                });

            }

            return approvedMemPoolList;
        }

        public static async Task<bool> DoubleSpendReplayCheck(Transaction tx)
        {
            bool result = false;
            
            if (Globals.MemBlocks.Any())
            {
                var txExist = Globals.MemBlocks.ContainsKey(tx.Hash);
                if (txExist)
                {
                    result = true;//replay or douple spend has occured
                }
            }

            if(result)
            {
                return result;//replay or douple spend has occured
            }

            var mempool = GetPool();
            var txs = mempool.Find(x => x.FromAddress == tx.FromAddress && x.Hash != tx.Hash).ToList();

            if(txs.Count() > 0)
            {
                var amount = txs.Sum(x => x.Amount + x.Fee);
                var stateTreiAcct = StateData.GetSpecificAccountStateTrei(tx.FromAddress);
                if(stateTreiAcct != null)
                {
                    var amountTotal = amount + tx.Amount + tx.Fee;
                    if (amountTotal > stateTreiAcct.Balance)
                    {
                        result = true; //douple spend or overspend has occured
                    }
                }
            }

            if (result)
            {
                return result;//replay or douple spend has occured
            }

            //double NFT transfer or burn check
            if (tx.TransactionType != TransactionType.TX && 
                tx.TransactionType != TransactionType.ADNR && 
                tx.TransactionType != TransactionType.VOTE_TOPIC && 
                tx.TransactionType != TransactionType.VOTE && 
                tx.TransactionType != TransactionType.DSTR &&
                tx.TransactionType != TransactionType.RESERVE &&
                tx.TransactionType != TransactionType.NFT_SALE)
            {
                if(tx.Data != null)
                {
                    var scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
                    if(scDataArray != null)
                    {
                        var scData = scDataArray[0];

                        var function = (string?)scData["Function"];
                        var scUID = (string?)scData["ContractUID"];
                        if (!string.IsNullOrWhiteSpace(function))
                        {
                            switch (function)
                            {
                                case "Transfer()":
                                    //do something
                                    var otherTransferTxs = mempool.Find(x => x.FromAddress == tx.FromAddress && x.Hash != tx.Hash).ToList();
                                    if(otherTransferTxs.Count() > 0)
                                    {
                                        foreach(var ottx in otherTransferTxs)
                                        {
                                            if(ottx.TransactionType == TransactionType.NFT_TX || ottx.TransactionType == TransactionType.NFT_BURN)
                                            {
                                                if(ottx.Data != null)
                                                {
                                                    var ottxDataArray = JsonConvert.DeserializeObject<JArray>(ottx.Data);
                                                    if(ottxDataArray != null)
                                                    {
                                                        var ottxData = ottxDataArray[0];

                                                        var ottxFunction = (string?)ottxData["Function"];
                                                        var ottxscUID = (string?)ottxData["ContractUID"];
                                                        if(!string.IsNullOrWhiteSpace(ottxFunction))
                                                        {
                                                            if(ottxscUID == scUID)
                                                            {
                                                                //FAIL
                                                                return false;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    break;
                                case "Burn()":
                                    var otherBurnTxs = mempool.Find(x => x.FromAddress == tx.FromAddress && x.Hash != tx.Hash).ToList();
                                    if (otherBurnTxs.Count() > 0)
                                    {
                                        foreach (var obtx in otherBurnTxs)
                                        {
                                            if (obtx.TransactionType == TransactionType.NFT_TX || obtx.TransactionType == TransactionType.NFT_BURN)
                                            {
                                                if (obtx.Data != null)
                                                {
                                                    var obtxDataArray = JsonConvert.DeserializeObject<JArray>(obtx.Data);
                                                    if (obtxDataArray != null)
                                                    {
                                                        var obtxData = obtxDataArray[0];

                                                        var obtxFunction = (string?)obtxData["Function"];
                                                        var obtxscUID = (string?)obtxData["ContractUID"];
                                                        if (!string.IsNullOrWhiteSpace(obtxFunction))
                                                        {
                                                            if (obtxscUID == scUID)
                                                            {
                                                                //FAIL
                                                                return false;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                    
                }
            }
            return result;
        }

        public static LiteDB.ILiteCollection<Transaction> GetAll()
        {
            var collection = DbContext.DB_Wallet.GetCollection<Transaction>(DbContext.RSRV_TRANSACTIONS);
            return collection;
        }

        public static IEnumerable<Transaction> GetAllLocalTransactions(bool showFailed = false)
        {
            var transactions = GetAll().Query().Where(x => x.TransactionStatus != TransactionStatus.Failed).ToEnumerable();

            if (showFailed)
                transactions = GetAll().Query().Where(x => true).ToEnumerable();

            return transactions;
        }

        public static Transaction? GetTxByHash(string hash)
        {
            var transaction = GetAll().Query().Where(x => x.Hash == hash).FirstOrDefault();

            return transaction;
        }

        public static IEnumerable<Transaction> GetTxByBlock(long height)
        {
            var transactions = GetAll().Query().Where(x => x.Height == height).ToEnumerable();

            return transactions;
        }

        public static IEnumerable<Transaction> GetSuccessfulLocalTransactions(bool showFailed = false)
        {
            var transactions = GetAll().Query().Where(x => x.TransactionStatus == TransactionStatus.Success).ToEnumerable();

            if (showFailed)
                transactions = GetAll().Query().Where(x => true).ToEnumerable();

            return transactions;
        }
        public static IEnumerable<Transaction> GetReserveLocalTransactions(bool showFailed = false)
        {
            var transactions = GetAll().Query().Where(x => x.TransactionStatus == TransactionStatus.Reserved).ToEnumerable();

            return transactions;
        }

        public static IEnumerable<Transaction> GetLocalMinedTransactions(bool showFailed = false)
        {
            var transactions = GetAll().Query().Where(x =>  x.FromAddress == "Coinbase_BlkRwd").ToEnumerable();

            if (showFailed)
                transactions = GetAll().Query().Where(x => true).ToEnumerable();

            return transactions;
        }

        public static IEnumerable<Transaction> GetLocalPendingTransactions()
        {
            var transactions = GetAll().Query().Where(x => x.TransactionStatus == TransactionStatus.Pending).ToEnumerable();

            return transactions;
        }

        public static IEnumerable<Transaction> GetLocalFailedTransactions()
        {
            var transactions = GetAll().Query().Where(x => x.TransactionStatus == TransactionStatus.Failed).ToEnumerable();

            return transactions;
        }

        public static IEnumerable<Transaction> GetLocalTransactionsSinceBlock(long blockHeight)
        {
            var transactions = GetAll().Query().Where(x => x.Height >= blockHeight).ToEnumerable();

            return transactions;
        }

        public static IEnumerable<Transaction> GetLocalTransactionsBeforeBlock(long blockHeight)
        {
            var transactions = GetAll().Query().Where(x => x.Height < blockHeight).ToEnumerable();

            return transactions;
        }

        public static IEnumerable<Transaction> GetLocalTransactionsSinceDate(long timestamp)
        {
            var transactions = GetAll().Query().Where(x => x.Timestamp >= timestamp).ToEnumerable();

            return transactions;
        }

        public static IEnumerable<Transaction> GetLocalTransactionsBeforeDate(long timestamp)
        {
            var transactions = GetAll().Query().Where(x => x.Timestamp < timestamp).ToEnumerable();

            return transactions;
        }

        public static IEnumerable<Transaction> GetLocalVoteTransactions()
        {
            var transactions = GetAll().Query().Where(x => x.TransactionType == TransactionType.VOTE).ToEnumerable();

            return transactions;
        }

        public static IEnumerable<Transaction> GetLocalVoteTopics()
        {
            var transactions = GetAll().Query().Where(x => x.TransactionType == TransactionType.VOTE_TOPIC).ToEnumerable();

            return transactions;
        }

        public static IEnumerable<Transaction> GetLocalAdnrTransactions()
        {
            var transactions = GetAll().Query().Where(x => x.TransactionType == TransactionType.ADNR).ToEnumerable();

            return transactions;
        }

        public static IEnumerable<Transaction> GetAllLocalTransactionsByAddress(string address)
        {
            var transactions = GetAll().Query().Where(x => x.FromAddress == address || x.ToAddress == address).ToEnumerable();

            return transactions;
        }
        public static IEnumerable<Transaction> GetAccountTransactionsLimit(string address, int limit = 50)
        {
            var transactions = GetAll();
            var query = transactions.Query()
                .OrderByDescending(x => x.Timestamp)
                .Where(x => x.FromAddress == address || x.ToAddress == address)
                .Limit(limit).ToEnumerable();
            return query;
        }

        public static IEnumerable<Transaction> GetTransactionsPaginated(int pageNumber, int resultPerPage, string? address = null)
        {
            var transactions = GetAll();
            if(address == null)
            {
                var query = transactions.Query()
                    .OrderByDescending(x => x.Timestamp)
                    .Offset((pageNumber - 1) * resultPerPage)
                    .Limit(resultPerPage).ToEnumerable();

                return query;
            }
            else
            {
                var query = transactions.Query()
                    .Where(x => x.FromAddress == address || x.ToAddress == address)
                    .OrderByDescending(x => x.Timestamp)
                    .Offset((pageNumber - 1) * resultPerPage)
                    .Limit(resultPerPage).ToEnumerable();

                return query;
            }
            
        }

    }

}
