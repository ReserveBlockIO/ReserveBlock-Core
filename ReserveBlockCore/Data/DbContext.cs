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
        public static LiteDatabase DB_Assets { set; get; }// stores Assets        
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

        //Database names
        public const string RSRV_DB_NAME = @"rsrvblkdata.db";
        public const string RSRV_DB_ASSETS = @"rsrvassetdata.db";        
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
        public const string RSRV_SCSTATE_TREI = "rsrv_scstate_trei";
        public const string RSRV_BEACON_INFO = "rsrv_beacon_info";
        public const string RSRV_BEACON_DATA = "rsrv_beacon_data";
        public const string RSRV_DNR = "rsrv_dnr";
        public const string RSRV_DECSHOP = "rsrv_decshop";
        public const string RSRV_DECSHOPSTATE_TREI = "rsrv_decshopstate_trei";
        public const string RSRV_KEYSTORE = "rsrv_keystore";

        static DbContext()
        {
            var databaseLocation = Globals.IsTestNet != true ? "Databases" : "DatabasesTestNet";
            var mainFolderPath = Globals.IsTestNet != true ? "RBX" : "RBXTest";

            string path = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                path = homeDirectory + Path.DirectorySeparatorChar + mainFolderPath.ToLower() + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
            }
            else
            {
                if(Debugger.IsAttached)
                {
                    path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "DBs" + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                }
                else
                {
                    path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar + mainFolderPath + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                }
            }
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var mapper = new BsonMapper();
            mapper.RegisterType<DateTime>(
                value => value.ToString("o", CultureInfo.InvariantCulture),
                bson => DateTime.ParseExact(bson, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
            mapper.RegisterType<DateTimeOffset>(
                value => value.ToString("o", CultureInfo.InvariantCulture),
                bson => DateTimeOffset.ParseExact(bson, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

            DB = new LiteDatabase(new ConnectionString{Filename = path + RSRV_DB_NAME,Connection = ConnectionType.Direct,ReadOnly = false}, mapper);
            DB_Assets = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_ASSETS, Connection = ConnectionType.Direct, ReadOnly = false });          
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

            //DB_Assets = new LiteDatabase(path + RSRV_DB_ASSETS, mapper);
            //DB_Queue = new LiteDatabase(path + RSRV_DB_QUEUE_NAME);
            //DB_WorldStateTrei = new LiteDatabase(path + RSRV_DB_WSTATE_TREI);
            //DB_AccountStateTrei = new LiteDatabase(path + RSRV_DB_ASTATE_TREI);
            //DB_SmartContractStateTrei = new LiteDatabase(path + RSRV_DB_SCSTATE_TREI, mapper);
            //DB_Wallet = new LiteDatabase(path + RSRV_DB_WALLET_NAME);
            //DB_Peers = new LiteDatabase(path + RSRV_DB_PEERS_NAME);
            //DB_Banlist = new LiteDatabase(path + RSRV_DB_BANLIST_NAME);
            //DB_Config = new LiteDatabase(path + RSRV_DB_CONFIG);

            DB_Assets.Pragma("UTC_DATE", true);
            DB_SmartContractStateTrei.Pragma("UTC_DATE", true);

            
        }        
        public static void BeginTrans()
        {
            if (Globals.HasTransactionDict.TryGetValue(Environment.CurrentManagedThreadId, out var hasTransaction) && hasTransaction)
                return;
            Globals.HasTransactionDict[Environment.CurrentManagedThreadId] = true;
            
            DB.BeginTrans();
            DB_Assets.BeginTrans();
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
        }
        public static void Commit()
        {
            if (!Globals.HasTransactionDict.TryGetValue(Environment.CurrentManagedThreadId, out var hasTransaction) || !hasTransaction)
                return;
            Globals.HasTransactionDict[Environment.CurrentManagedThreadId] = false;

            DB.Commit();
            DB_Assets.Commit();
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
        }

        public static void Rollback()
        {
            if (!Globals.HasTransactionDict.TryGetValue(Environment.CurrentManagedThreadId, out var hasTransaction) || !hasTransaction)
                return;
            Globals.HasTransactionDict[Environment.CurrentManagedThreadId] = false;

            DB.Rollback();
            DB_Assets.Rollback();
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
        }

        public static void DeleteCorruptDb()
        {
            var databaseLocation = Globals.IsTestNet != true ? "Databases" : "DatabasesTestNet";
            var mainFolderPath = Globals.IsTestNet != true ? "RBX" : "RBXTest";

            string path = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                path = homeDirectory + Path.DirectorySeparatorChar + mainFolderPath.ToLower() + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
            }
            else
            {
                if (Debugger.IsAttached)
                {
                    path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "DBs" + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                }
                else
                {
                    path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar + mainFolderPath + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                }
            }
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            DB.Dispose();

            File.Delete(path + RSRV_DB_NAME);

            DB = new LiteDatabase(path + RSRV_DB_NAME);

        }

        public static void MigrateDbNewChainRef()
        {
            var databaseLocation = Globals.IsTestNet != true ? "Databases" : "DatabasesTestNet";
            var mainFolderPath = Globals.IsTestNet != true ? "RBX" : "RBXTest";

            string path = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                path = homeDirectory + Path.DirectorySeparatorChar + mainFolderPath.ToLower() + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
            }
            else
            {
                if (Debugger.IsAttached)
                {
                    path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "DBs" + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                }
                else
                {
                    path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar + mainFolderPath + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                }
            }
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            DB.Checkpoint();            
            DB_WorldStateTrei.Checkpoint();
            DB_AccountStateTrei.Checkpoint();
            DB_Wallet.Checkpoint();
            DB_HD_Wallet.Checkpoint();
            DB_Peers.Checkpoint();
            DB_Banlist.Checkpoint();
            DB_Config.Checkpoint();
            DB_Assets.Checkpoint();
            DB_SmartContractStateTrei.Checkpoint();
            DB_Beacon.Checkpoint();
            DB_DNR.Checkpoint();
            DB_DecShopStateTrei.Checkpoint();
            DB_Keystore.Checkpoint();

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
            File.Delete(path + RSRV_DB_WSTATE_TREI);
            File.Delete(path + RSRV_DB_ASTATE_TREI);
            File.Delete(path + RSRV_DB_WALLET_NAME);
            File.Delete(path + RSRV_DB_HD_WALLET_NAME);
            File.Delete(path + RSRV_DB_PEERS_NAME);
            File.Delete(path + RSRV_DB_BANLIST_NAME);
            File.Delete(path + RSRV_DB_CONFIG);
            File.Delete(path + RSRV_DB_ASSETS);
            File.Delete(path + RSRV_DB_SCSTATE_TREI);
            File.Delete(path + RSRV_DB_BEACON);
            File.Delete(path + RSRV_DB_DNR);
            File.Delete(path + RSRV_DB_DECSHOPSTATE_TREI);
            File.Delete(path + RSRV_DB_KEYSTORE);

            var mapper = new BsonMapper();
            mapper.RegisterType<DateTime>(
                value => value.ToString("o", CultureInfo.InvariantCulture),
                bson => DateTime.ParseExact(bson, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
            mapper.RegisterType<DateTimeOffset>(
                value => value.ToString("o", CultureInfo.InvariantCulture),
                bson => DateTimeOffset.ParseExact(bson, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

            //recreate DBs
            DB = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_NAME, Connection = ConnectionType.Direct, ReadOnly = false }, mapper);
            DB_Assets = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_ASSETS, Connection = ConnectionType.Direct, ReadOnly = false });            
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

            DB_Assets.Pragma("UTC_DATE", true);
            DB_SmartContractStateTrei.Pragma("UTC_DATE", true);
        }

        public static void CloseDB()
        {
            DB.Dispose();            
            DB_Wallet.Dispose();
            DB_Peers.Dispose();
            DB_Banlist.Dispose();
            DB_WorldStateTrei.Dispose();
            DB_AccountStateTrei.Dispose();
            DB_Config.Dispose();
            DB_Assets.Dispose();
            DB_SmartContractStateTrei.Dispose();
            DB_Beacon.Dispose();
            DB_DNR.Dispose();
            DB_DecShopStateTrei.Dispose();
            DB_Keystore.Dispose();
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
        }

    }
}