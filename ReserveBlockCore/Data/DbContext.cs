using LiteDB;
using ReserveBlockCore.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReserveBlockCore.Data
{
    internal class DbContext
    {
        public static LiteDatabase DB { set; get; }

        public const string RSRV_DB_NAME = @"rsrvblkdata.db";
        public const string RSRV_BLOCKCHAIN = "rsrv_blockchain";
        public const string RSRV_BLOCKS = "rsrv_blocks";
        public const string RSRV_TRANSACTION_POOL = "rsrv_transaction_pool";
        public const string RSRV_TRANSACTIONS = "rsrv_transactions";
        public const string RSRV_WALLET = "rsrv_wallet";
        public const string RSRV_WALLET_SETTINGS = "rsrv_wallet_settings";

        public static void Initialize()
        {
            DB = new LiteDatabase(RSRV_DB_NAME);
        }

        public static void CloseDB()
        {
            DB.Dispose();
        }

        public static void BackupWalletData()
        {
            //Create method to back up wallet.
        }

        //This should only be used if wallet is having sync or errors. Please back up wallet data first!
        public static void ClearDB()
        {
            var coll = DB.GetCollection<Block>(RSRV_BLOCKS);
            coll.DeleteAll();

            var coll2 = DB.GetCollection<Transaction>(RSRV_TRANSACTION_POOL);
            coll2.DeleteAll();

            var coll3 = DB.GetCollection<Transaction>(RSRV_TRANSACTIONS);
            coll3.DeleteAll();

            //Force backup wallet just in case.
            BackupWalletData();

            var coll4 = DB.GetCollection<Transaction>(RSRV_WALLET);
            coll4.DeleteAll();

            var coll5 = DB.GetCollection<Blockchain>(RSRV_BLOCKCHAIN);
            coll5.DeleteAll();

        }
    }
}
