using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReserveBlockCore.Utilities;
using ReserveBlockCore.Models;
using LiteDB;

namespace ReserveBlockCore.Data
{
    internal class TransactionData
    {
        public static void CreateGenesisTransction()
        {
            var timeStamp = TimeUtil.GetTime();
            var gTrx = new Transaction
            {
                Amount = 100,
                BlockHeight = 0,
                FromAddress = "rbx_genesis_transaction",
                ToAddress = "Foundation Coin Address Goes Here",
                Fee = 0,
                Hash = "", //this will be built down below. showing just to make this clear.
                Timestamp = timeStamp,
            };

            gTrx.Build();

            AddToPool(gTrx);
        }

        public static void AddToPool(Transaction transaction)
        {
            var TransactionPool = GetPool();
            TransactionPool.Insert(transaction);
        }

        public static ILiteCollection<Transaction> GetPool()
        {
            var collection = DbContext.DB.GetCollection<Transaction>(DbContext.RSRV_TRANSACTION_POOL);
            return collection;
        }

        public static ILiteCollection<Transaction> GetAll()
        {
            var collection = DbContext.DB.GetCollection<Transaction>(DbContext.RSRV_TRANSACTIONS);
            return collection;
        }

        //Use this to see if any address has transactions against it. 
        public static Transaction GetTxByAddress(string address)
        {
            var transactions = DbContext.DB.GetCollection<Transaction>(DbContext.RSRV_TRANSACTIONS);
            transactions.EnsureIndex(x => x.Timestamp);
            var tx = transactions.FindOne(x => x.FromAddress == address || x.ToAddress == address);
            return tx;
        }

        public static IEnumerable<Transaction> GetAccountTransactions(string address, int limit = 50)
        {
            var transactions = DbContext.DB.GetCollection<Transaction>(DbContext.RSRV_TRANSACTIONS);
            transactions.EnsureIndex(x => x.FromAddress);
            transactions.EnsureIndex(x => x.ToAddress);
            var query = transactions.Query()
                .OrderByDescending(x => x.Timestamp)
                .Where(x => x.FromAddress == address || x.ToAddress == address)
                .Limit(limit).ToList();
            return query;
        }

        public static Transaction GetTxByHash(string hash)
        {
            var transactions = DbContext.DB.GetCollection<Transaction>(DbContext.RSRV_TRANSACTIONS);
            transactions.EnsureIndex(x => x.Timestamp);
            var tx = transactions.FindOne(x => x.Hash == hash);
            return tx;
        }

        public static IEnumerable<Transaction> GetTxnsByHeight(long height, int limit = 50)
        {
            var transactions = DbContext.DB.GetCollection<Transaction>(DbContext.RSRV_TRANSACTIONS);
            transactions.EnsureIndex(x => x.Timestamp);
            var query = transactions.Query()
                .OrderByDescending(x => x.Timestamp)
                .Where(x => x.BlockHeight == height)
                .Limit(limit).ToList();
            return query;

        }

        public static IEnumerable<Transaction> GetTransactions(int pageNumber, int resultPerPage)
        {
            var transactions = DbContext.DB.GetCollection<Transaction>(DbContext.RSRV_TRANSACTIONS);
            transactions.EnsureIndex(x => x.Timestamp);
            var query = transactions.Query()
                .OrderByDescending(x => x.Timestamp)
                .Offset((pageNumber - 1) * resultPerPage)
                .Limit(resultPerPage).ToList();
            return query;
        }

        public static decimal GetBalance(string address)
        {
            decimal balance = 0;
            decimal spending = 0;
            decimal income = 0;

            var collection = GetAll();
            var transactions = collection.Find(x => x.FromAddress == address || x.ToAddress == address);

            foreach (Transaction tx in transactions)
            {
                var sender = tx.FromAddress;
                var recipient = tx.ToAddress;

                if (address.ToLower().Equals(sender.ToLower()))
                {
                    spending += tx.Amount + tx.Fee;
                }

                if (address.ToLower().Equals(recipient.ToLower()))
                {
                    income += tx.Amount;
                }

                balance = income - spending;
            }

            return balance;
        }
    }

}
