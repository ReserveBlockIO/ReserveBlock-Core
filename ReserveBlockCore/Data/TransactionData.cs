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
        public static void AddTxToWallet(Transaction transaction)
        {
            var txs = GetAll();
            var txCheck = txs.FindOne(x => x.Hash == transaction.Hash);
            if(txCheck== null)
            {
                txs.InsertSafe(transaction);
            }
        }

        public static async Task<bool> HasTxBeenCraftedIntoBlock(Transaction tx)
        {
            var result = false;

            var transactions = Globals.MemBlocks.ToArray().SelectMany(x => x.Transactions).ToArray();
            if (transactions.Count() > 0)
            {
                var txExist = transactions.Any(x => x.Hash == tx.Hash);
                if (txExist == true)
                {
                    result = true;//douple spend has occured
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

            if (minuteDiff > 120.0M)
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
                collection.EnsureIndexSafe(x => x.Hash);
                return collection;
            }
            catch(Exception ex)
            {
                DbContext.Rollback();
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

        public static List<Transaction> ProcessTxPool()
        {
            var collection = DbContext.DB_Mempool.GetCollection<Transaction>(DbContext.RSRV_TRANSACTION_POOL);

            var memPoolTxList = collection.FindAll().ToList();
            //Size the pool to 1mb
            var sizedMempoolList = MempoolSizeUtility.SizeMempoolDown(memPoolTxList);

            var approvedMemPoolList = new List<Transaction>();

            if(sizedMempoolList.Count() > 0)
            {
                sizedMempoolList.ForEach(tx => {
                    var txExist = approvedMemPoolList.Exists(x => x.Hash == tx.Hash);
                    if(!txExist)
                    {
                        var signature = tx.Signature;
                        var sigCheck = SignatureService.VerifySignature(tx.FromAddress, tx.Hash, signature);
                        if (sigCheck)
                        {
                            var balance = AccountStateTrei.GetAccountBalance(tx.FromAddress);

                            var totalSend = (tx.Amount + tx.Fee);
                            if (balance >= totalSend)
                            {
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
                                        DbContext.Rollback();
                                    }
                                }
                            }
                        }
                        else
                        {
                            var txToDelete = collection.FindOne(t => t.Hash == tx.Hash);
                            if(txToDelete != null)
                            {
                                try
                                {
                                    collection.DeleteManySafe(x => x.Hash == txToDelete.Hash);
                                }
                                catch (Exception ex)
                                {
                                    DbContext.Rollback();
                                }
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

            var transactions = Globals.MemBlocks.ToArray().SelectMany(x => x.Transactions).ToArray();
            if (transactions.Count() > 0)
            {
                var txExist = transactions.Any(x => x.Hash == tx.Hash);
                if (txExist)
                {
                    result = true;//replay or douple spend has occured
                }

            }

            if(result)
            {
                return result;//replay or douple spend has occured
            }

            var mempool = TransactionData.GetPool();
            var txs = mempool.Find(x => x.FromAddress == tx.FromAddress).ToList();

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

            return result;
        }

        public static LiteDB.ILiteCollection<Transaction> GetAll()
        {
            var collection = DbContext.DB_Wallet.GetCollection<Transaction>(DbContext.RSRV_TRANSACTIONS);
            return collection;
        }

        //Use this to see if any address has transactions against it. 
        public static Transaction GetTxByAddress(string address)
        {
            var transactions = DbContext.DB_Wallet.GetCollection<Transaction>(DbContext.RSRV_TRANSACTIONS);
            transactions.EnsureIndexSafe(x => x.Timestamp);
            var tx = transactions.FindOne(x => x.FromAddress == address || x.ToAddress == address);
            return tx;
        }

        public static IEnumerable<Transaction> GetAccountTransactions(string address, int limit = 50)
        {
            var transactions = DbContext.DB_Wallet.GetCollection<Transaction>(DbContext.RSRV_TRANSACTIONS);
            transactions.EnsureIndexSafe(x => x.FromAddress);
            transactions.EnsureIndexSafe(x => x.ToAddress);
            var query = transactions.Query()
                .OrderByDescending(x => x.Timestamp)
                .Where(x => x.FromAddress == address || x.ToAddress == address)
                .Limit(limit).ToList();
            return query;
        }

        public static Transaction GetTxByHash(string hash)
        {
            var transactions = DbContext.DB_Wallet.GetCollection<Transaction>(DbContext.RSRV_TRANSACTIONS);
            transactions.EnsureIndexSafe(x => x.Timestamp);
            var tx = transactions.FindOne(x => x.Hash == hash);
            return tx;
        }

        //public static IEnumerable<Transaction> GetTxnsByHeight(long height, int limit = 50)
        //{
        //    var transactions = DbContext.DB.GetCollection<Transaction>(DbContext.RSRV_TRANSACTIONS);
        //    transactions.EnsureIndexSafe(x => x.Timestamp);
        //    var query = transactions.Query()
        //        .OrderByDescending(x => x.Timestamp)
        //        .Where(x => x.Height == height)
        //        .Limit(limit).ToList();
        //    return query;

        //}

        public static IEnumerable<Transaction> GetTransactions(int pageNumber, int resultPerPage)
        {
            var transactions = DbContext.DB_Wallet.GetCollection<Transaction>(DbContext.RSRV_TRANSACTIONS);
            transactions.EnsureIndexSafe(x => x.Timestamp);
            var query = transactions.Query()
                .OrderByDescending(x => x.Timestamp)
                .Offset((pageNumber - 1) * resultPerPage)
                .Limit(resultPerPage).ToList();
            return query;
        }

    }

}
