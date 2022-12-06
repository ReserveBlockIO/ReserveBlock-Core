using ReserveBlockCore.Extensions;
using ReserveBlockCore.Models;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Globalization;
using LiteDB;
using ReserveBlockCore.Utilities;
using System.Collections.Concurrent;

namespace ReserveBlockCore.Data
{
    internal static class DbContext
    {
        public static LiteDatabase DB { set; get; }// stores blocks
        public static LiteDatabase DB_Mempool { set; get; }// stores blocks
        public static LiteDatabase DB_Assets { set; get; }// stores Assets (Smart Contracts)       
        public static LiteDatabase DB_AssetQueue { set; get; }// stores Asset Queue      
        public static LiteDatabase DB_Wallet { set; get; } //stores wallet info
        public static LiteDatabase DB_HD_Wallet { set; get; } //stores HD wallet info
        public static LiteDatabase DB_Peers { set; get; } //stores peer info
        public static LiteDatabase DB_Banlist { set; get; } //stores banned peers 
        public static LiteDatabase DB_WorldStateTrei { get; set; } //stores blockchain world state trei
        public static LiteDatabase DB_AccountStateTrei { get; set; } //stores blockchain account state trei
        public static LiteDatabase DB_SmartContractStateTrei { set; get; }// stores SC Data
        public static LiteDatabase DB_DecShopStateTrei { set; get; }// stores decentralized shop data
        public static LiteDatabase DB_Beacon { get; set; }
        public static LiteDatabase DB_Config { get; set; }
        public static LiteDatabase DB_DNR { get; set; }
        public static LiteDatabase DB_Keystore { get; set; }
        public static LiteDatabase DB_TopicTrei { set; get; }
        public static LiteDatabase DB_Vote { set; get; }


        //Database names
        public const string RSRV_DB_NAME = @"rsrvblkdata.db";
        public const string RSRV_DB_MEMPOOL = @"rsrvmempooldata.db";
        public const string RSRV_DB_ASSETS = @"rsrvassetdata.db";
        public const string RSRV_DB_ASSET_QUEUE = @"rsrvassetqueue.db";
        public const string RSRV_DB_WALLET_NAME = @"rsrvwaldata.db";
        public const string RSRV_DB_HD_WALLET_NAME = @"rsrvhdwaldata.db";
        public const string RSRV_DB_BANLIST_NAME = @"rsrvbanldata.db";
        public const string RSRV_DB_PEERS_NAME = @"rsrvpeersdata.db";
        public const string RSRV_DB_WSTATE_TREI = @"rsrvwstatetrei.db";
        public const string RSRV_DB_ASTATE_TREI = @"rsrvastatetrei.db";
        public const string RSRV_DB_SCSTATE_TREI = @"rsrvscstatetrei.db";
        public const string RSRV_DB_DECSHOPSTATE_TREI = @"rsrvdecshopstatetrei.db";
        public const string RSRV_DB_BEACON = @"rsrvbeacon.db";
        public const string RSRV_DB_CONFIG = @"rsrvconfig.db";
        public const string RSRV_DB_DNR = @"rsrvdnr.db";
        public const string RSRV_DB_KEYSTORE = @"rsrvkeystore.db";
        public const string RSRV_DB_TOPIC_TREI = @"rsrvtopictrei.db";
        public const string RSRV_DB_VOTE = @"rsrvvote.db";

        //Database tables
        public const string RSRV_BLOCKCHAIN = "rsrv_blockchain";
        public const string RSRV_BLOCKS = "rsrv_blocks";
        public const string RSRV_BLOCK_QUEUE = "rsrv_block_queue";
        public const string RSRV_TRANSACTION_POOL = "rsrv_transaction_pool";
        public const string RSRV_TRANSACTIONS = "rsrv_transactions";
        public const string RSRV_WALLET = "rsrv_wallet";
        public const string RSRV_HD_WALLET = "rsrv_hd_wallet";
        public const string RSRV_ACCOUNTS = "rsrv_account";
        public const string RSRV_ACCOUNT_KEYSTORE = "rsrv_account_keystore";
        public const string RSRV_WALLET_SETTINGS = "rsrv_wallet_settings";
        public const string RSRV_BAN_LIST = "rsrv_ban_list";
        public const string RSRV_PEERS = "rsrv_peers";
        public const string RSRV_VALIDATORS = "rsrv_validators";
        public const string RSRV_ADJUDICATORS = "rsrv_adjudicators";
        public const string RSRV_WSTATE_TREI = "rsrv_wstate_trei";
        public const string RSRV_ASTATE_TREI = "rsrv_astate_trei";
        public const string RSRV_CONFIG = "rsrv_config";
        public const string RSRV_CONFIG_RULES = "rsrv_config_rules";
        public const string RSRV_ASSETS = "rsrv_assets";
        public const string RSRV_ASSET_QUEUE = "rsrv_asset_queue";
        public const string RSRV_SCSTATE_TREI = "rsrv_scstate_trei";
        public const string RSRV_BEACON_INFO = "rsrv_beacon_info";
        public const string RSRV_BEACON_DATA = "rsrv_beacon_data";
        public const string RSRV_BEACON_REF = "rsrv_beacon_ref";
        public const string RSRV_DNR = "rsrv_dnr";
        public const string RSRV_DECSHOP = "rsrv_decshop";
        public const string RSRV_DECSHOPSTATE_TREI = "rsrv_decshopstate_trei";
        public const string RSRV_KEYSTORE = "rsrv_keystore";
        public const string RSRV_SIGNER = "rsrv_signer";
        public const string RSRV_LOCAL_TIMES = "rsrv_local_time";
        public const string RSRV_TOPIC_TREI = "rsrv_topic_trei";
        public const string RSRV_VOTE = "rsrv_vote";

        internal static void Initialize()
        {
            string path = GetPathUtility.GetDatabasePath();

            var mapper = new BsonMapper();
            mapper.RegisterType<DateTime>(
                value => value.ToString("o", CultureInfo.InvariantCulture),
                bson => DateTime.ParseExact(bson, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
            mapper.RegisterType<DateTimeOffset>(
                value => value.ToString("o", CultureInfo.InvariantCulture),
                bson => DateTimeOffset.ParseExact(bson, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

            DB = new LiteDatabase(new ConnectionString{Filename = path + RSRV_DB_NAME,Connection = ConnectionType.Direct,ReadOnly = false}, mapper);
            DB_Mempool = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_MEMPOOL, Connection = ConnectionType.Direct, ReadOnly = false }, mapper);
            DB_Assets = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_ASSETS, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_AssetQueue = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_ASSET_QUEUE, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_WorldStateTrei = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_WSTATE_TREI, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_AccountStateTrei = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_ASTATE_TREI, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_SmartContractStateTrei = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_SCSTATE_TREI, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_Wallet = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_WALLET_NAME, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_HD_Wallet = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_HD_WALLET_NAME, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_Peers = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_PEERS_NAME, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_Banlist = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_BANLIST_NAME, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_Config = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_CONFIG, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_Beacon = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_BEACON, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_DNR = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_DNR, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_DecShopStateTrei = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_DECSHOPSTATE_TREI, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_Keystore = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_KEYSTORE, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_TopicTrei = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_TOPIC_TREI, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_Vote = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_VOTE, Connection = ConnectionType.Direct, ReadOnly = false });

            var blocks = DB.GetCollection<Block>(RSRV_BLOCKS);
            blocks.EnsureIndexSafe(x => x.Height);
            var localTimeDb = BlockLocalTime.GetBlockLocalTimes();
            localTimeDb.EnsureIndexSafe(x => x.Height, true);

            var transactionPool = DbContext.DB.GetCollection<Transaction>(DbContext.RSRV_TRANSACTION_POOL);
            transactionPool.EnsureIndexSafe(x => x.Hash, false);
            transactionPool.EnsureIndexSafe(x => x.FromAddress, false);
            transactionPool.EnsureIndexSafe(x => x.ToAddress, false);

            var transactions = DbContext.DB_Wallet.GetCollection<Transaction>(DbContext.RSRV_TRANSACTIONS);
            transactions.EnsureIndexSafe(x => x.Hash, false);
            transactions.EnsureIndexSafe(x => x.FromAddress, false);
            transactions.EnsureIndexSafe(x => x.ToAddress, false);

            var aTrei = DbContext.DB_AccountStateTrei.GetCollection<AccountStateTrei>(DbContext.RSRV_ASTATE_TREI);            
            aTrei.EnsureIndexSafe(x => x.Key, false);

            //var peers = DbContext.DB_Peers.GetCollection<Peers>(DbContext.RSRV_PEERS);
            //peers.EnsureIndex(x => x.PeerIP, true);

            DB_Assets.Pragma("UTC_DATE", true);
            DB_AssetQueue.Pragma("UTC_DATE", true);
            DB_SmartContractStateTrei.Pragma("UTC_DATE", true);
            DB_TopicTrei.Pragma("UTC_DATE", true);
            DB_Vote.Pragma("UTC_DATE", true);
        }        
        public static void BeginTrans()
        {
            if (Globals.HasTransactionDict.TryGetValue(Environment.CurrentManagedThreadId, out var hasTransaction) && hasTransaction)
                return;
            Globals.HasTransactionDict[Environment.CurrentManagedThreadId] = true;
            
            DB.BeginTrans();
            DB_Mempool.BeginTrans();
            DB_Assets.BeginTrans();
            DB_AssetQueue.BeginTrans();
            DB_Wallet.BeginTrans();
            DB_HD_Wallet.BeginTrans();
            DB_Peers.BeginTrans();
            DB_Banlist.BeginTrans();
            DB_WorldStateTrei.BeginTrans();
            DB_AccountStateTrei.BeginTrans();
            DB_SmartContractStateTrei.BeginTrans();
            DB_DecShopStateTrei.BeginTrans();
            DB_Beacon.BeginTrans();
            DB_Config.BeginTrans();
            DB_DNR.BeginTrans();
            DB_Keystore.BeginTrans();
            DB_TopicTrei.BeginTrans();
            DB_Vote.BeginTrans();
        }
        public static void Commit()
        {
            if (!Globals.HasTransactionDict.TryGetValue(Environment.CurrentManagedThreadId, out var hasTransaction) || !hasTransaction)
                return;
            Globals.HasTransactionDict[Environment.CurrentManagedThreadId] = false;

            DB.Commit();
            DB_Mempool.Commit();
            DB_Assets.Commit();
            DB_AssetQueue.Commit();
            DB_Wallet.Commit();
            DB_HD_Wallet.Commit();
            DB_Peers.Commit();
            DB_Banlist.Commit();
            DB_WorldStateTrei.Commit();
            DB_AccountStateTrei.Commit();
            DB_SmartContractStateTrei.Commit();
            DB_DecShopStateTrei.Commit();
            DB_Beacon.Commit();
            DB_Config.Commit();
            DB_DNR.Commit();
            DB_Keystore.Commit();
            DB_TopicTrei.Commit();
            DB_Vote.Commit();
        }

        public static void Rollback()
        {
            if (!Globals.HasTransactionDict.TryGetValue(Environment.CurrentManagedThreadId, out var hasTransaction) || !hasTransaction)
                return;
            Globals.HasTransactionDict[Environment.CurrentManagedThreadId] = false;

            DB.Rollback();
            DB_Mempool.Rollback();
            DB_Assets.Rollback();
            DB_AssetQueue.Rollback();
            DB_Wallet.Rollback();
            DB_HD_Wallet.Rollback();
            DB_Peers.Rollback();
            DB_Banlist.Rollback();
            DB_WorldStateTrei.Rollback();
            DB_AccountStateTrei.Rollback();
            DB_SmartContractStateTrei.Rollback();
            DB_DecShopStateTrei.Rollback();
            DB_Beacon.Rollback();
            DB_Config.Rollback();
            DB_DNR.Rollback();
            DB_Keystore.Rollback();
            DB_TopicTrei.Rollback();
            DB_Vote.Rollback();
        }

        public static void DeleteCorruptDb()
        {
            string path = GetPathUtility.GetDatabasePath();

            DB.Dispose();

            File.Delete(path + RSRV_DB_NAME);

            DB = new LiteDatabase(path + RSRV_DB_NAME);

        }

        public static void MigrateDbNewChainRef()
        {
            string path = GetPathUtility.GetDatabasePath();

            DB.Commit();
            DB_Mempool.Commit();
            DB_Assets.Commit();
            DB_AssetQueue.Commit();
            DB_Wallet.Commit();
            DB_HD_Wallet.Commit();
            DB_Peers.Commit();
            DB_Banlist.Commit();
            DB_WorldStateTrei.Commit();
            DB_AccountStateTrei.Commit();
            DB_SmartContractStateTrei.Commit();
            DB_DecShopStateTrei.Commit();
            DB_Beacon.Commit();
            DB_Config.Commit();
            DB_DNR.Commit();
            DB_Keystore.Commit();
            DB_TopicTrei.Commit();
            DB_Vote.Commit();

            //dispose connection to DB
            CloseDB();

            try
            {
                if (File.Exists(path + RSRV_DB_WALLET_NAME.Replace("rsrvwaldata", "rsrvwaldata_bak")))
                {
                    File.Delete(path + RSRV_DB_WALLET_NAME.Replace("rsrvwaldata", "rsrvwaldata_bak"));
                    File.Move(path + RSRV_DB_WALLET_NAME, path + RSRV_DB_WALLET_NAME.Replace("rsrvwaldata", "rsrvwaldata_bak"));
                }
                else
                {
                    File.Move(path + RSRV_DB_WALLET_NAME, path + RSRV_DB_WALLET_NAME.Replace("rsrvwaldata", "rsrvwaldata_bak"));
                }
            }
            catch(Exception ex)
            {
                ErrorLogUtility.LogError("Error making backup!", "DbContext.MigrateDbNewChainRef()");
            }



            File.Delete(path + RSRV_DB_NAME);
            File.Delete(path + RSRV_DB_MEMPOOL);
            File.Delete(path + RSRV_DB_WSTATE_TREI);
            File.Delete(path + RSRV_DB_ASTATE_TREI);
            File.Delete(path + RSRV_DB_WALLET_NAME);
            File.Delete(path + RSRV_DB_HD_WALLET_NAME);
            File.Delete(path + RSRV_DB_PEERS_NAME);
            File.Delete(path + RSRV_DB_BANLIST_NAME);
            File.Delete(path + RSRV_DB_CONFIG);
            File.Delete(path + RSRV_DB_ASSETS);
            File.Delete(path + RSRV_DB_ASSET_QUEUE);
            File.Delete(path + RSRV_DB_SCSTATE_TREI);
            File.Delete(path + RSRV_DB_BEACON);
            File.Delete(path + RSRV_DB_DNR);
            File.Delete(path + RSRV_DB_DECSHOPSTATE_TREI);
            File.Delete(path + RSRV_DB_KEYSTORE);
            File.Delete(path + RSRV_DB_TOPIC_TREI);
            File.Delete(path + RSRV_DB_VOTE);

            var mapper = new BsonMapper();
            mapper.RegisterType<DateTime>(
                value => value.ToString("o", CultureInfo.InvariantCulture),
                bson => DateTime.ParseExact(bson, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
            mapper.RegisterType<DateTimeOffset>(
                value => value.ToString("o", CultureInfo.InvariantCulture),
                bson => DateTimeOffset.ParseExact(bson, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

            //recreate DBs
            DB = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_NAME, Connection = ConnectionType.Direct, ReadOnly = false }, mapper);
            DB_Mempool = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_MEMPOOL, Connection = ConnectionType.Direct, ReadOnly = false }, mapper);
            DB_Assets = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_ASSETS, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_AssetQueue = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_ASSET_QUEUE, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_WorldStateTrei = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_WSTATE_TREI, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_AccountStateTrei = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_ASTATE_TREI, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_SmartContractStateTrei = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_SCSTATE_TREI, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_Wallet = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_WALLET_NAME, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_HD_Wallet = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_HD_WALLET_NAME, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_Peers = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_PEERS_NAME, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_Banlist = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_BANLIST_NAME, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_Config = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_CONFIG, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_Beacon = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_BEACON, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_DNR = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_DNR, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_DecShopStateTrei = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_DECSHOPSTATE_TREI, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_Keystore = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_KEYSTORE, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_TopicTrei = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_TOPIC_TREI, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_Vote = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_VOTE, Connection = ConnectionType.Direct, ReadOnly = false });

            DB_Assets.Pragma("UTC_DATE", true);
            DB_AssetQueue.Pragma("UTC_DATE", true);
            DB_SmartContractStateTrei.Pragma("UTC_DATE", true);
            DB_TopicTrei.Pragma("UTC_DATE", true);
            DB_Vote.Pragma("UTC_DATE", true);
        }

        public static void CloseDB()
        {
            DB.Dispose();
            DB_Mempool.Dispose();
            DB_Wallet.Dispose();
            DB_HD_Wallet.Dispose();
            DB_Peers.Dispose();
            DB_Banlist.Dispose();
            DB_WorldStateTrei.Dispose();
            DB_AccountStateTrei.Dispose();
            DB_Config.Dispose();
            DB_Assets.Dispose();
            DB_AssetQueue.Dispose();
            DB_SmartContractStateTrei.Dispose();
            DB_Beacon.Dispose();
            DB_DNR.Dispose();
            DB_DecShopStateTrei.Dispose();
            DB_Keystore.Dispose();
            DB_TopicTrei.Dispose();
            DB_Vote.Dispose();
        }

        public static async Task CheckPoint()
        {
            try
            {
                DB.Checkpoint();
            }
            catch { }
            try
            {
                DB_Mempool.Checkpoint();
            }
            catch { }
            try
            {
                DB_AccountStateTrei.Checkpoint();
            }
            catch { }
            try
            {
                DB_Banlist.Checkpoint();
            }
            catch { }
            try
            {
                DB_Peers.Checkpoint();
            }
            catch { }
            try
            {
                DB_Wallet.Checkpoint();
            }
            catch { }
            try
            {
                DB_WorldStateTrei.Checkpoint();
            }
            catch { }
            try
            {
                DB_Config.Checkpoint();
            }
            catch { }
            try
            {
                DB_Assets.Checkpoint();
            }
            catch { }
            try
            {
                DB_AssetQueue.Checkpoint();
            }
            catch { }
            try
            {
                DB_SmartContractStateTrei.Checkpoint();
            }
            catch { }
            try
            {
                DB_Beacon.Checkpoint();
            }
            catch { }
            try
            {
                DB_DNR.Checkpoint();
            }
            catch { }
            try
            {
                DB_DecShopStateTrei.Checkpoint();
            }
            catch { }
            try
            {
                DB_Keystore.Checkpoint();
            }
            catch { }
            try
            {
                DB_TopicTrei.Checkpoint();
            }
            catch { }
            try
            {
                DB_Vote.Checkpoint();
            }
            catch { }
        }

    }
}