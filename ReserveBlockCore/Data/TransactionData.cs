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
    }

}
