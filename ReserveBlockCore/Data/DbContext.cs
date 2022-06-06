using LiteDB;
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

namespace ReserveBlockCore.Data
{
    internal class DbContext
    {
        public static LiteDatabase DB { set; get; }// stores blocks
        public static LiteDatabase DB_Assets { set; get; }// stores blocks
        public static LiteDatabase DB_Queue { set; get; }// stores blocks
        public static LiteDatabase DB_Wallet { set; get; } //stores wallet info
        public static LiteDatabase DB_Peers { set; get; } //stores peer info
        public static LiteDatabase DB_Banlist { set; get; } //stores banned peers 
        public static LiteDatabase DB_WorldStateTrei { get; set; } //stores blockchain world state trei
        public static LiteDatabase DB_AccountStateTrei { get; set; } //stores blockchain world state trei
        public static LiteDatabase DB_SmartContractStateTrei { set; get; }// stores blocks
        public static LiteDatabase DB_Config { get; set; }

        //Database names
        public const string RSRV_DB_NAME = @"rsrvblkdata.db";
        public const string RSRV_DB_ASSETS = @"rsrvassetdata.db";
        public const string RSRV_DB_QUEUE_NAME = @"rsrvblkqueuedata.db";
        public const string RSRV_DB_WALLET_NAME = @"rsrvwaldata.db";
        public const string RSRV_DB_BANLIST_NAME = @"rsrvbanldata.db";
        public const string RSRV_DB_PEERS_NAME = @"rsrvpeersdata.db";
        public const string RSRV_DB_WSTATE_TREI = @"rsrvwstatetrei.db";
        public const string RSRV_DB_ASTATE_TREI = @"rsrvastatetrei.db";
        public const string RSRV_DB_SCSTATE_TREI = @"rsrvscstatetrei.db";
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
        public const string RSRV_ADJUDICATORS = "rsrv_adjudicators";
        public const string RSRV_WSTATE_TREI = "rsrv_wstate_trei";
        public const string RSRV_ASTATE_TREI = "rsrv_astate_trei";
        public const string RSRV_CONFIG = "rsrv_config";
        public const string RSRV_CONFIG_RULES = "rsrv_config_rules";
        public const string RSRV_ASSETS = "rsrv_assets";
        public const string RSRV_SCSTATE_TREI = "rsrv_scstate_trei";

        public static void Initialize()
        {
            var databaseLocation = Program.IsTestNet != true ? "Databases" : "DatabasesTestNet";

            string path = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                path = homeDirectory + Path.DirectorySeparatorChar + "rbx" + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
            }
            else
            {
                if(Debugger.IsAttached)
                {
                    path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                }
                else
                {
                    path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar + "RBX" + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
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
            DB_Queue = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_QUEUE_NAME, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_WorldStateTrei = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_WSTATE_TREI, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_AccountStateTrei = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_ASTATE_TREI, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_SmartContractStateTrei = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_SCSTATE_TREI, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_Wallet = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_WALLET_NAME, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_Peers = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_PEERS_NAME, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_Banlist = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_BANLIST_NAME, Connection = ConnectionType.Direct, ReadOnly = false });
            DB_Config = new LiteDatabase(new ConnectionString { Filename = path + RSRV_DB_CONFIG, Connection = ConnectionType.Direct, ReadOnly = false });

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

        public static void DeleteCorruptDb()
        {
            var databaseLocation = Program.IsTestNet != true ? "Databases" : "DatabasesTestNet";

            string path = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                path = homeDirectory + Path.DirectorySeparatorChar + "rbx" + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
            }
            else
            {
                if (Debugger.IsAttached)
                {
                    path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                }
                else
                {
                    path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar + "RBX" + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
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
            var databaseLocation = Program.IsTestNet != true ? "Databases" : "DatabasesTestNet";

            string path = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                path = homeDirectory + Path.DirectorySeparatorChar + "rbx" + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
            }
            else
            {
                if (Debugger.IsAttached)
                {
                    path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                }
                else
                {
                    path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar + "RBX" + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                }
            }
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            DB.Checkpoint();
            DB_Queue.Checkpoint();
            DB_WorldStateTrei.Checkpoint();
            DB_AccountStateTrei.Checkpoint();
            DB_Wallet.Checkpoint();
            DB_Peers.Checkpoint();
            DB_Banlist.Checkpoint();
            DB_Config.Checkpoint();
            DB_Assets.Checkpoint();
            DB_SmartContractStateTrei.Checkpoint();

            //dispose connection to DB
            CloseDB();
            if (File.Exists(path + RSRV_DB_WALLET_NAME.Replace("rsrvwaldata", "rsrvwaldata_bak")))
            {
                File.Delete(path + RSRV_DB_WALLET_NAME.Replace("rsrvwaldata", "rsrvwaldata_bak"));
                File.Move(path + RSRV_DB_WALLET_NAME, path + RSRV_DB_WALLET_NAME.Replace("rsrvwaldata", "rsrvwaldata_bak"));
            }
            else
            {
                File.Move(path + RSRV_DB_WALLET_NAME, path + RSRV_DB_WALLET_NAME.Replace("rsrvwaldata", "rsrvwaldata_bak"));
            }
            

            File.Delete(path + RSRV_DB_NAME);
            File.Delete(path + RSRV_DB_QUEUE_NAME);
            File.Delete(path + RSRV_DB_WSTATE_TREI);
            File.Delete(path + RSRV_DB_ASTATE_TREI);
            File.Delete(path + RSRV_DB_WALLET_NAME);
            File.Delete(path + RSRV_DB_PEERS_NAME);
            File.Delete(path + RSRV_DB_BANLIST_NAME);
            File.Delete(path + RSRV_DB_CONFIG);
            File.Delete(path + RSRV_DB_ASSETS);
            File.Delete(path + RSRV_DB_SCSTATE_TREI);

            //recreate DBs
            DB = new LiteDatabase(path + RSRV_DB_NAME);
            DB_Queue = new LiteDatabase(path + RSRV_DB_QUEUE_NAME);
            DB_WorldStateTrei = new LiteDatabase(path + RSRV_DB_WSTATE_TREI);
            DB_AccountStateTrei = new LiteDatabase(path + RSRV_DB_ASTATE_TREI);
            DB_SmartContractStateTrei = new LiteDatabase(path + RSRV_DB_SCSTATE_TREI);
            DB_Wallet = new LiteDatabase(path + RSRV_DB_WALLET_NAME);
            DB_Peers = new LiteDatabase(path + RSRV_DB_PEERS_NAME);
            DB_Banlist = new LiteDatabase(path + RSRV_DB_BANLIST_NAME);
            DB_Config = new LiteDatabase(path + RSRV_DB_CONFIG);
            DB_Assets = new LiteDatabase(path + RSRV_DB_ASSETS);
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
            DB_Assets.Dispose();
            DB_SmartContractStateTrei.Dispose();
        }

    }
}
