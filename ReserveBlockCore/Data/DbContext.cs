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
        public static LiteDatabase DB_Queue { set; get; }// stores blocks
        public static LiteDatabase DB_Wallet { set; get; } //stores wallet info
        public static LiteDatabase DB_Peers { set; get; } //stores peer info
        public static LiteDatabase DB_Banlist { set; get; } //stores banned peers 
        public static LiteDatabase DB_WorldStateTrei { get; set; } //stores blockchain world state trei
        public static LiteDatabase DB_AccountStateTrei { get; set; } //stores blockchain world state trei
        public static LiteDatabase DB_Config { get; set; }

        //Database names
        public const string RSRV_DB_NAME = @"rsrvblkdata.db";
        public const string RSRV_DB_QUEUE_NAME = @"rsrvblkqueuedata.db";
        public const string RSRV_DB_WALLET_NAME = @"rsrvwaldata.db";
        public const string RSRV_DB_BANLIST_NAME = @"rsrvbanldata.db";
        public const string RSRV_DB_PEERS_NAME = @"rsrvpeersdata.db";
        public const string RSRV_DB_WSTATE_TREI = @"rsrvwstatetrei.db";
        public const string RSRV_DB_ASTATE_TREI = @"rsrvastatetrei.db";
        public const string RSRV_DB_CONFIG = @"rsrvconfig.db";

        //Database tables
        public const string RSRV_BLOCKCHAIN = "rsrv_blockchain";
        public const string RSRV_BLOCKS = "rsrv_blocks";
        public const string RSRV_BLOCK_QUEUE = "rsrv_block_queue";
        public const string RSRV_TRANSACTION_POOL = "rsrv_transaction_pool";
        public const string RSRV_TRANSACTIONS = "rsrv_transactions";
        public const string RSRV_WALLET = "rsrv_wallet";
        public const string RSRV_ACCOUNTS = "rsrv_account";
        public const string RSRV_WALLET_SETTINGS = "rsrv_wallet_settings";
        public const string RSRV_BAN_LIST = "rsrv_ban_list";
        public const string RSRV_PEERS = "rsrv_peers";
        public const string RSRV_VALIDATORS = "rsrv_validators";
        public const string RSRV_WSTATE_TREI = "rsrv_wstate_trei";
        public const string RSRV_ASTATE_TREI = "rsrv_astate_trei";
        public const string RSRV_CONFIG = "rsrv_config";
        public const string RSRV_CONFIG_RULES = "rsrv_config_rules";

        public static void Initialize()
        {
            var databaseLocation = Program.IsTestNet != true ? "Databases" : "DatabasesTestNet";
            string path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            DB = new LiteDatabase(path + RSRV_DB_NAME);
            DB_Queue = new LiteDatabase(path + RSRV_DB_QUEUE_NAME);
            DB_WorldStateTrei = new LiteDatabase(path + RSRV_DB_WSTATE_TREI);
            DB_AccountStateTrei = new LiteDatabase(path + RSRV_DB_ASTATE_TREI);
            DB_Wallet = new LiteDatabase(path + RSRV_DB_WALLET_NAME);
            DB_Peers = new LiteDatabase(path + RSRV_DB_PEERS_NAME);
            DB_Banlist = new LiteDatabase(path + RSRV_DB_BANLIST_NAME);
            DB_Config = new LiteDatabase(path + RSRV_DB_CONFIG);
        }

        public static void CloseDB()
        {
            DB.Dispose();
            DB_Queue.Dispose();
            DB_Wallet.Dispose();
            DB_Peers.Dispose();
            DB_Banlist.Dispose();
            DB_WorldStateTrei.Dispose();
            DB_AccountStateTrei.Dispose();
            DB_Config.Dispose();
        }

    }
}
