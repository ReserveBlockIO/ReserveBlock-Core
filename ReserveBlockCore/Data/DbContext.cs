using LiteDB;
using ReserveBlockCore.Models;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReserveBlockCore.Data
{
    internal class DbContext
    {
        public static LiteDatabase DB { set; get; }// stores blocks
        public static LiteDatabase DB_Wallet { set; get; } //stores wallet info
        public static LiteDatabase DB_Peers { set; get; } //stores peer info
        public static LiteDatabase DB_Banlist { set; get; } //stores banned peers 
        public static LiteDatabase DB_StateTrei { get; set; } //stores blockchain state trei

        public static LiteDatabase DBTest { set; get; }
        public static LiteDatabase DBTest_Wallet { set; get; }
        public static LiteDatabase DBTest_Peers { set; get; }
        public static LiteDatabase DBTest_Banlist { set; get; }

        //Database names
        public const string RSRV_DB_NAME = @"rsrvblkdata.db";
        public const string RSRV_DB_WALLET_NAME = @"rsrvwaldata.db";
        public const string RSRV_DB_BANLIST_NAME = @"rsrvbanldata.db";
        public const string RSRV_DB_PEERS_NAME = @"rsrvpeersdata.db";
        public const string RSRV_DB_STATE_TREI = @"rsrvstatetrei.db";

        //db names for test
        public const string RSRV_DBTest_NAME = @"rsrvblkdata_test.db";
        public const string RSRV_DBTest_WALLET_NAME = @"rsrvwaldata_test.db";
        public const string RSRV_DBTest_BANLIST_NAME = @"rsrvbanldata_test.db";
        public const string RSRV_DBTest_PEERS_NAME = @"rsrvpeersdata_Test.db";

        //Database tables
        public const string RSRV_BLOCKCHAIN = "rsrv_blockchain";
        public const string RSRV_BLOCKS = "rsrv_blocks";
        public const string RSRV_TRANSACTION_POOL = "rsrv_transaction_pool";
        public const string RSRV_TRANSACTIONS = "rsrv_transactions";
        public const string RSRV_WALLET = "rsrv_wallet";
        public const string RSRV_ACCOUNTS = "rsrv_account";
        public const string RSRV_WALLET_SETTINGS = "rsrv_wallet_settings";
        public const string RSRV_BAN_LIST = "rsrv_ban_list";
        public const string RSRV_PEERS = "rsrv_peers";
        public const string RSRV_VALIDATORS = "rsrv_validators";
        public const string RSRV_STATE_TREI = "rsrv_state_trei";

        public static void Initialize()
        {
            DB = new LiteDatabase(RSRV_DB_NAME);
            DB_StateTrei = new LiteDatabase(RSRV_DB_STATE_TREI);
            DB_Wallet = new LiteDatabase(RSRV_DB_WALLET_NAME);
            DB_Peers = new LiteDatabase(RSRV_DB_BANLIST_NAME);
            DB_Banlist = new LiteDatabase(RSRV_DB_PEERS_NAME);
        }

        public static void InitializeTest()
        {
            DBTest = new LiteDatabase(RSRV_DB_NAME);
            DBTest_Wallet = new LiteDatabase(RSRV_DB_WALLET_NAME);
            DBTest_Peers = new LiteDatabase(RSRV_DB_BANLIST_NAME);
            DBTest_Banlist = new LiteDatabase(RSRV_DB_PEERS_NAME);
        }

        public static void CloseDB()
        {
            DB.Dispose();
            DB_Wallet.Dispose();
            DB_Peers.Dispose();
            DB_Banlist.Dispose();
        }
        public static void CloseTestDB()
        {
            DBTest.Dispose();
            DBTest_Wallet.Dispose();
            DBTest_Peers.Dispose();
            DBTest_Banlist.Dispose();
        }


    }
}
