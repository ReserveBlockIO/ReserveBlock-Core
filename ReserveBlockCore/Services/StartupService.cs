using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using ReserveBlockCore.Extensions;
using Newtonsoft.Json;
using ReserveBlockCore.Beacon;
using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Utilities;
using Spectre.Console;
using System.Collections.Concurrent;
using System.Security.Cryptography.Xml;
using Microsoft.AspNetCore.SignalR.Client;
using ReserveBlockCore.Nodes;
using System.Net;
using System.Security;
using System.Xml.Linq;
using System.Data;
using System.Diagnostics;

namespace ReserveBlockCore.Services
{
    internal class StartupService
    {

        internal static void AnotherInstanceCheck()
        {
            using (TcpClient tcpClient = new TcpClient())
            {
                try
                {
                    var port = Globals.Port;
                    tcpClient.Connect("127.0.0.1", port);
                    LogUtility.Log($"CLI Already Running on port {port}. Closing new instance.", "StartupService.AnotherInstanceCheck()");
                    Environment.Exit(0);
                }
                catch (Exception)
                {
                    Console.WriteLine("Application Starting...");
                }
            }
        }
        internal static void ClearValidatorDups()
        {
            ValidatorService.ClearDuplicates();
        }

        internal static void ClearOldValidatorDups()
        {
            ValidatorService.ClearOldValidator();
        }
        internal static void StartupDatabase()
        {                        
            Console.WriteLine("Initializing Reserve Block Database...");
            DbContext.Initialize();
            var peerDb = Peers.GetAll();
            Globals.BannedIPs = new ConcurrentDictionary<string, Peers>(
                peerDb.Find(x => x.IsBanned || x.IsPermaBanned).ToArray().ToDictionary(x => x.PeerIP, x => x));
        }

        public static async void EncryptedPasswordEntry()
        {
            bool exit = false;
            while (!exit)
            {
                var password = "";
                if(Globals.EncryptPassword.Length > 0)
                {
                    Console.WriteLine("Password loaded from args...");
                    password = Globals.EncryptPassword.ToUnsecureString();
                }
                else
                {
                    Console.WriteLine("Please enter validator password.");
                    password = Console.ReadLine();
                }
                
                if (!string.IsNullOrEmpty(password))
                {
                    Globals.EncryptPassword = password.ToSecureString();
                    var account = AccountData.GetSingleAccount(Globals.ValidatorAddress);
                    BigInteger b1 = BigInteger.Parse(account.GetKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
                    PrivateKey privateKey = new PrivateKey("secp256k1", b1);

                    var randString = RandomStringUtility.GetRandomString(8);

                    var signature = SignatureService.CreateSignature(randString, privateKey, account.PublicKey);
                    var sigVerify = SignatureService.VerifySignature(account.Address, randString, signature);

                    if(sigVerify)
                    {
                        password = "";
                        exit = true;
                    }
                    else
                    {
                        password = "";
                        Globals.EncryptPassword.Dispose();
                        Globals.EncryptPassword = new SecureString();
                        Console.WriteLine("Password was incorrect. Please attempt again");
                        Console.WriteLine("If you would like to turn off validating to proceed please type 'y' and press enter. To try again type 'n'.");
                        var response = Console.ReadLine();
                        if(!string.IsNullOrEmpty(response))
                        {
                            if(response.ToLower() == "y")
                            {
                                await ValidatorService.DoMasterNodeStop();
                                exit = true;
                            }
                        }
                        
                    }
                }
            }
        }

        public static async void EncryptedPasswordEntryAdj()
        {
            bool exit = false;
            while (!exit)
            {
                var password = "";
                if (Globals.EncryptPassword.Length > 0)
                {
                    Console.WriteLine("Password loaded from args...");
                    password = Globals.EncryptPassword.ToUnsecureString();
                }
                else
                {
                    Console.WriteLine("Please enter validator password.");
                    password = Console.ReadLine();
                }
                if (!string.IsNullOrEmpty(password))
                {
                    Globals.EncryptPassword = password.ToSecureString();
                    var account = Globals.AdjudicateAccount;
                    BigInteger b1 = BigInteger.Parse(account.GetKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
                    PrivateKey privateKey = new PrivateKey("secp256k1", b1);

                    var randString = RandomStringUtility.GetRandomString(8);

                    var signature = SignatureService.CreateSignature(randString, privateKey, account.PublicKey);
                    var sigVerify = SignatureService.VerifySignature(account.Address, randString, signature);

                    if (sigVerify)
                    {
                        Globals.AdjudicatePrivateKey = privateKey;
                        password = "";
                        exit = true;
                    }
                    else
                    {
                        password = "";
                        Globals.EncryptPassword.Dispose();
                        Globals.EncryptPassword = new SecureString();
                        Console.WriteLine("Password was incorrect. Please attempt again");
                    }
                }
            }
        }

        internal static void SetAdjudicatorAddresses()
        {
            Globals.LastBlock = BlockchainData.GetLastBlock() ?? new Block { Height = -1 };

            var signerDB = Signer.GetSigners();
            var Signers = signerDB.FindAll().ToArray();

            if (Signers.Any())
            {
                Signer.Signers = new ConcurrentDictionary<(string Address, long StartHeight), long?>(
                    Signers.ToDictionary(x => (x.Address, x.StartHeight), x => x.EndHeight));
            }
            else
            {
                Signer.Signers = Globals.IsTestNet ? new ConcurrentDictionary<(string, long), long?>
                {
                    [("xBRxhFC2C4qE21ai3cQuBrkyjXnvP1HqZ8", 0)] = null,
                    [("xBRA57xaL612t35aac1WWQxYQ2ipTV5WcF", 0)] = null,
                    [("xBREKz8TcSh7uhs5mNrWttGkrciaq2jy3V", 0)] = null,
                    [("xBRHXgEwJEqZad6USusAXJfz7Pc6KHViix", 0)] = null,
                } :
                new ConcurrentDictionary<(string, long), long?>
                {
                    [("RBxy1XGZ72f6YqktseaLJ1sJsE9u5DF3sp", Globals.V3Height)] = null,
                    [("RBxkrs6snuTuHjAfzedXGzRixfeyvQfy7m", Globals.V3Height)] = null,
                    [("RBxz1j5veSPrBg4RSyYD4CZ9BY6LPQ65gM", Globals.V3Height)] = null,
                    [("RBx1FNEvjB97HRdreDg3zHCNCSSEvSyBTE", Globals.V3Height)] = null,
                    [("RBxuRe1PorrpUCSbcmBk4JDHCxeADAkXyX", Globals.V3Height)] = null,
                    [("RBxfsqZ28nZt9wM9rNeacfxqPFUkKfXWM7", Globals.V3Height)] = null,
                    [("RBxc2kz67W2zvb3yGxzACEQqgFiiBfYSTY", Globals.V3Height)] = null,
                };

                foreach (var signer in Signer.Signers.Select(x => new Signer { Address = x.Key.Address, StartHeight = x.Key.StartHeight, EndHeight = x.Value }))
                    signerDB.InsertSafe(signer);
            }

            var Accounts = AccountData.GetAccounts().FindAll().ToArray();
            Globals.AdjudicateAccount = Accounts.Where(x => Globals.Signers.ContainsKey(x.Address)).FirstOrDefault();
            if (Globals.AdjudicateAccount != null)
            {
                BigInteger b1 = BigInteger.Parse(Globals.AdjudicateAccount.GetKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
                Globals.AdjudicatePrivateKey = new PrivateKey("secp256k1", b1);
            }            
        }

        internal static void HDWalletCheck()
        {
            var check = HDWallet.HDWalletData.GetHDWallet();
            if(check != null)
            {
                Globals.HDWallet = true;
            }
        }

        internal static void EncryptedWalletCheck()
        {
            var keystore = Keystore.GetKeystore();
            if (keystore != null)
            {
                if(keystore.FindAll().Count() > 0)
                    Globals.IsWalletEncrypted = true;
            }
        }
        internal static void SetBlockchainChainRef()
        {
            //58 mainnet b
            //BlockchainData.ChainRef = "m_Gi9RNxviAq1TmvuPZsZBzdAa8AWVJtNa7cm1dFaT4dWDbdqSNSTh";

            BlockchainData.ChainRef = "m1_Gi9RNxviAq1TmvuPZsZBzdAa8AWVJtNa7cm1dFaT4dWDbdqSNSTh";
            LogUtility.Log("RBX ChainRef - " + BlockchainData.ChainRef, "Main");

            if (Globals.IsTestNet)
            {
                //testnet
                BlockchainData.ChainRef = "t_testnet1";
            }
        }

        internal static void CheckBlockRefVerToDb()
        {
            var genesisBlock = BlockchainData.GetGenesisBlock();
            if(genesisBlock != null)
            {
                if(genesisBlock.ChainRefId != BlockchainData.ChainRef)
                {
                    //migrate to new chain ref.
                    DbContext.MigrateDbNewChainRef();
                }
            }
        }

        internal static async Task RunSettingChecks(bool runStateSync = false)
        {
            var settings = Settings.GetSettings();
            if (settings != null)
            {
                if (!settings.CorrectShutdown)
                {
                    if(!Debugger.IsAttached || runStateSync)
                    {
                        await StateTreiSyncService.SyncAccountStateTrei();
                    }
                }

                if (Globals.AdjudicateAccount == null)
                {
                    var now = DateTime.Now;
                    var lastShutDown = settings.LastShutdown;

                    if (lastShutDown != null && settings.CorrectShutdown)
                    {
                        if (lastShutDown.Value.AddSeconds(20) > now)
                        {
                            var diff = Convert.ToInt32((lastShutDown.Value.AddSeconds(20) - now).TotalMilliseconds);
                            Console.WriteLine("Wallet was restarted too fast. Startup will continue in a moment. Do not close wallet.");
                            await Task.Delay(diff);//make the wallet wait if restart is too fast
                        }
                    }
                    else
                    {
                        if (!Debugger.IsAttached)
                        {
                            Console.WriteLine("Wallet was restarted too fast or improperly closed. Startup will continue in a moment. Do not close wallet.");
                            await Task.Delay(15000);
                        }
                    }
                }

                _ = Settings.InitiateStartupUpdate();
            }
        }

        internal static void SetBlockHeight()
        {
            Globals.LastBlock.Height = BlockchainData.GetHeight();
            LogUtility.Log("RBX Height - " + Globals.LastBlock.Height.ToString(), "Main");
        }

        internal static void SetLastBlock()
        {
            if(Globals.LastBlock.Height != -1)
            {
                Globals.LastBlock = BlockchainData.GetLastBlock();
            }
        }
        internal static async void RunStateSync()
        {            
            if (Globals.AdjudicateAccount == null)
                await StateTreiSyncService.SyncAccountStateTrei();
        }
        internal static void RunRules()
        {
            //no rules needed at this time
        }

        internal static void StartBeacon()
        {
            try
            {
                if(Globals.SelfBeacon?.SelfBeaconActive == true)
                {
                    var port = Globals.Port + 20000; //23338 - mainnet
 
                    BeaconServer server = new BeaconServer(GetPathUtility.GetBeaconPath(), port);
                    Thread obj_thread = new Thread(server.StartServer());
                    Console.WriteLine("Beacon Started");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            
        }

        internal static async Task SetSelfBeacon()
        {
            var beacons = Beacons.GetBeacons();
            if(beacons != null)
            {
                var selfBeacon = beacons.Query().Where(x => x.SelfBeacon).FirstOrDefault();
                if(selfBeacon != null)
                    Globals.SelfBeacon = selfBeacon;
            }
        }

        internal static void LoadBeacons()
        {
            var beacons = Beacons.GetBeacons();
            if(beacons != null)
            {
                var beaconList = beacons.Query().Where(x => true).ToEnumerable();
                if(beaconList.Count() == 0)
                {
                    //seed beacons
                    BootstrapBeacons();
                    beaconList = beacons.Query().Where(x => true).ToEnumerable();
                    if(beaconList.Count() > 0)
                    {
                        foreach(var beacon in beaconList)
                        {
                            Globals.Beacons.TryAdd(beacon.IPAddress, beacon);
                        }
                    }
                    else
                    {
                        ErrorLogUtility.LogError("Failed to see Beacons. NFT Transfers will not work.", "StartupService.LoadBeacons()");
                    }
                }
                else
                {
                    foreach (var beacon in beaconList)
                    {
                        Globals.Beacons.TryAdd(beacon.IPAddress, beacon);
                    }
                }
            }                

        }

        internal static void BootstrapBeacons()
        {
            var beacons = Beacons.GetBeacons();
            if(beacons != null )
            {
                if(!Globals.IsTestNet)
                {
                    List<Beacons> beaconList = new List<Beacons> 
                    {
                        new Beacons { IPAddress = "162.248.14.123", Name = "Lily Beacon", Port = Globals.Port + 20000, BeaconUID = "LilyBeacon", DefaultBeacon = true, AutoDeleteAfterDownload = true, FileCachePeriodDays = 2, IsPrivateBeacon = false, SelfBeacon = false, SelfBeaconActive = false, BeaconLocator = "", Region = 1 },
                        new Beacons { IPAddress = "144.126.149.104",Name = "Wisteria Beacon", Port = Globals.Port + 20000, BeaconUID = "WisteriaBeacon", DefaultBeacon = true, AutoDeleteAfterDownload = true, FileCachePeriodDays = 2, IsPrivateBeacon = false, SelfBeacon = false, SelfBeaconActive = false, BeaconLocator = "", Region = 1 },
                        new Beacons { IPAddress = "144.126.150.118", Name = "Tulip Beacon", Port = Globals.Port + 20000, BeaconUID = "TulipBeacon", DefaultBeacon = true, AutoDeleteAfterDownload = true, FileCachePeriodDays = 2, IsPrivateBeacon = false, SelfBeacon = false, SelfBeaconActive = false, BeaconLocator = "", Region = 1 },
                        new Beacons { IPAddress = "89.117.21.39", Name = "Sunflower Beacon", Port = Globals.Port + 20000, BeaconUID = "SunflowerBeacon", DefaultBeacon = true, AutoDeleteAfterDownload = true, FileCachePeriodDays = 2, IsPrivateBeacon = false, SelfBeacon = false, SelfBeaconActive = false, BeaconLocator = "", Region = 1 },
                        new Beacons { IPAddress = "89.117.21.40", Name = "Lavender Beacon", Port = Globals.Port + 20000, BeaconUID = "LavenderBeacon", DefaultBeacon = true, AutoDeleteAfterDownload = true, FileCachePeriodDays = 2, IsPrivateBeacon = false, SelfBeacon = false, SelfBeaconActive = false, BeaconLocator = "", Region = 1},
                        new Beacons { IPAddress = "209.126.11.92", Name = "Rose Beacon", Port = Globals.Port + 20000, BeaconUID = "RoseBeacon", DefaultBeacon = true, AutoDeleteAfterDownload = true, FileCachePeriodDays = 2, IsPrivateBeacon = false, SelfBeacon = false, SelfBeaconActive = false, BeaconLocator = "", Region = 1},
                        new Beacons { IPAddress = "149.102.144.58", Name = "Lupin Beacon", Port = Globals.Port + 20000, BeaconUID = "LupinBeacon", DefaultBeacon = true, AutoDeleteAfterDownload = true, FileCachePeriodDays = 2, IsPrivateBeacon = false, SelfBeacon = false, SelfBeaconActive = false, BeaconLocator = "", Region = 2},
                        new Beacons { IPAddress = "194.233.77.39", Name = "Orchid Beacon", Port = Globals.Port + 20000, BeaconUID = "OrchidBeacon", DefaultBeacon = true, AutoDeleteAfterDownload = true, FileCachePeriodDays = 2, IsPrivateBeacon = false, SelfBeacon = false, SelfBeaconActive = false, BeaconLocator = "", Region = 2},
                        new Beacons { IPAddress = "185.188.249.117", Name = "Lotus Beacon", Port = Globals.Port + 20000, BeaconUID = "LotusBeacon", DefaultBeacon = true, AutoDeleteAfterDownload = true, FileCachePeriodDays = 2, IsPrivateBeacon = false, SelfBeacon = false, SelfBeaconActive = false, BeaconLocator = "", Region = 2},
                        new Beacons { IPAddress = "154.26.155.35", Name = "Snapdragon Beacon", Port = Globals.Port + 20000, BeaconUID = "SnapdragonBeacon", DefaultBeacon = true, AutoDeleteAfterDownload = true, FileCachePeriodDays = 2, IsPrivateBeacon = false, SelfBeacon = false, SelfBeaconActive = false, BeaconLocator = "", Region = 2}
                    };

                    foreach (var beacon in beaconList)
                    {
                        beacon.BeaconLocator = Beacons.CreateBeaconLocator(beacon);
                    }

                    Beacons.SaveBeaconList(beaconList);
                }
                else
                {
                    //add testnet beacons
                    List<Beacons> beaconList = new List<Beacons>
                    {
                        new Beacons { IPAddress = "162.248.14.123", Name = "Lily Beacon TESTNET", Port = Globals.Port + 20000, BeaconUID = "LilyBeacon", DefaultBeacon = true, AutoDeleteAfterDownload = true, FileCachePeriodDays = 2, IsPrivateBeacon = false, SelfBeacon = false, SelfBeaconActive = false, BeaconLocator = "", Region = 1 },
                    };

                    foreach (var beacon in beaconList)
                    {
                        beacon.BeaconLocator = Beacons.CreateBeaconLocator(beacon);
                    }

                    Beacons.SaveBeaconList(beaconList);
                }
            } 
        }

        internal static async Task ClearStaleMempool()
        {
            bool memTxDeleted = false;
            var pool = TransactionData.GetPool();
            if(pool.Count() > 0)
            {
                var poolList = pool.FindAll().ToList();
                foreach(var tx in poolList)
                {
                    var time = tx.Timestamp;
                    var currentTime = TimeUtil.GetTime();
                    var timeDiff = currentTime - time;
                    var minuteDiff = timeDiff / 60M;

                    if(minuteDiff > 60.0M)
                    {
                        pool.DeleteManySafe(x => x.Hash == tx.Hash);
                        memTxDeleted = true;
                    }

                    if(tx.TransactionRating == TransactionRating.F)
                    {
                        pool.DeleteManySafe(x => x.Hash == tx.Hash);
                        memTxDeleted = true;
                    }
                }

                DbContext.DB.Checkpoint();
            }

            if(memTxDeleted == true)
            {
                var accounts = AccountData.GetAccounts();
                if (accounts.Count() > 0)
                {
                    var accountList = accounts.FindAll().ToList();
                    foreach(var account  in accountList)
                    {
                        var stateTrei = StateData.GetSpecificAccountStateTrei(account.Address);
                        if(stateTrei != null)
                        {
                            account.Balance = stateTrei.Balance;
                            accounts.UpdateSafe(account);
                        }
                    }
                }
            }
        }

        internal static void SetValidator()
        {
            var accounts = AccountData.GetAccounts();
            if(Globals.IsTestNet == true)
            {
                var myAccountTest = accounts.FindOne(x => x.IsValidating == true);
                if (myAccountTest != null)
                {
                    Globals.ValidatorAddress = myAccountTest.Address;
                }
            }
            var myAccount = accounts.FindOne(x => x.IsValidating == true && x.Address != Globals.GenesisAddress);
            if (myAccount != null)
            {
                Globals.ValidatorAddress = myAccount.Address;
            }
        }

        internal static async void SetConfigValidator()
        {
            var address = Globals.ConfigValidator;
            var uname = Globals.ConfigValidatorName;
            var accounts = AccountData.GetAccounts();
            var myAccount = accounts.FindOne(x => x.Address == address);
            if (myAccount != null && myAccount.IsValidating != true)
            {
                var valResult = await ValidatorService.StartValidating(myAccount, uname);
                Globals.ValidatorAddress = myAccount.Address;
            }
        }

        public static async Task UpdateBenchIpAndSigners()
        {
            while(!string.IsNullOrEmpty(Globals.ValidatorAddress))
            {
                foreach(var node in Globals.AdjNodes.Values.Where(x => x.IsConnected))
                {
                    try
                    {
                        var benchDb = AdjBench.GetBench();
                        var Result = await node.InvokeAsync<string>("IpAddresses", args: new object?[] { }, () => new CancellationTokenSource(8000).Token);
                        var CurrentBench = JsonConvert.DeserializeObject<AdjBench[]>(Result);
                        foreach (var bench in CurrentBench)
                        {
                            if (Globals.AdjBench.TryGetValue(bench.RBXAddress, out var cachedBench))
                            {
                                if (cachedBench.IPAddress != bench.IPAddress)
                                {
                                    cachedBench.IPAddress = bench.IPAddress;
                                    var dbBench = benchDb.FindOne(x => x.RBXAddress == bench.RBXAddress);
                                    dbBench.IPAddress = bench.IPAddress;
                                    benchDb.UpdateSafe(dbBench);
                                }
                            }
                        }

                        var Result2 = await Globals.AdjNodes.Values.FirstOrDefault().Connection?.InvokeCoreAsync<string>("SignerInfo", args: new object?[] { }, new CancellationTokenSource(8000).Token);
                        var CurrentSigners = JsonConvert.DeserializeObject<Signer[]>(Result2);
                        foreach (var signer in CurrentSigners.Where(x => Globals.AdjBench.ContainsKey(x.Address)))
                            if (Signer.Signers.TryAdd((signer.Address, signer.StartHeight), signer.EndHeight))
                            {
                                Signer.GetSigners().InsertSafe(signer);
                            }

                        break;
                    }
                    catch
                    {
                    }
                }

                await Task.Delay(new TimeSpan(1, 0, 0));
            }
        }

        internal static async Task GetAdjudicatorPool()
        {
            if (Globals.AdjudicateAccount != null)
                return;

            var account = AccountData.GetLocalValidator();
            if(account == null) return;
            var validators = Validators.Validator.GetAll();
            var validator = validators.FindOne(x => x.Address == account.Address);
            if (validator == null)
                return;
            
            var time = TimeUtil.GetTime().ToString();
            var signature = SignatureService.ValidatorSignature(validator.Address + ":" + TimeUtil.GetTime());

            foreach(var signer in Globals.Signers) // use to populate database, pull from database
            {

                if (Globals.AdjBench.TryGetValue(signer.Key, out var bench))
                {                    
                    var url = "http://" + bench.IPAddress + ":" + Globals.Port + "/adjudicator";
                    if (await P2PClient.ConnectAdjudicator(url, validator.Address, time, validator.UniqueName, signature))
                    {
                        _ = UpdateBenchIpAndSigners();
                        break;
                    }                    
                }

            }                       
        }

        internal static async Task SetLeadAdjudicator()
        {
            var adjudicatorLead = Adjudicators.AdjudicatorData.GetLeadAdjudicator();
            if(adjudicatorLead != null)
            {
                Globals.LeadAdjudicator = adjudicatorLead;
            }
            else
            {
                Globals.LeadAdjudicator = await P2PClient.GetLeadAdjudicator();
            }
        }

        internal static void CheckLastBlock()
        {
            try
            {
                //var lastBlock = BlockchainData.GetLastBlock();
                //var worldTrei = WorldTrei.GetWorldTreiRecord();
                //if (lastBlock != null && worldTrei != null)
                //{
                //    if (worldTrei.StateRoot != lastBlock.StateRoot)
                //    {
                //        //redownload old block and check the state trei from transactions to see if any were affected and need to be modified.
                //    }
                //}
                
            }
            catch(Exception ex)
            {
                //blocks most likely null
            }
            
        }
        internal static void StartupMemBlocks()
        {
            var blockChain = BlockchainData.GetBlocks();
            Globals.MemBlocks = new ConcurrentDictionary<string, long>(blockChain.Find(LiteDB.Query.All(LiteDB.Query.Descending)).Take(400)
                .Select(x => x.Transactions.Select(y => new { y.Hash, x.Height})).SelectMany(x => x).ToDictionary(x => x.Hash, x => x.Height));
        }

        public static async Task ConnectToConsensusNodes()
        {
            if (Globals.AdjudicateAccount == null)
                return;

            while(true)
            {
                try
                {
                    var SigningAddresses = Globals.Signers.Keys.ToHashSet();
                    var ConsensusAddresses = Globals.Nodes.Values.Select(x => x.Address).ToHashSet();

                    var NewSigners = SigningAddresses.Except(ConsensusAddresses).ToArray();
                    if (NewSigners.Any())
                    {
                        foreach (var signer in NewSigners)
                        {
                            if (Globals.AdjBench.TryGetValue(signer, out var bench))
                            {
                                Globals.Nodes.TryAdd(bench.IPAddress, new NodeInfo
                                {
                                    Address = bench.RBXAddress,
                                    NodeIP = bench.IPAddress
                                });
                            }
                        }
                        ConsensusAddresses = Globals.Nodes.Values.Select(x => x.Address).ToHashSet();
                    }
                                        
                    var NodesToRemove = ConsensusAddresses.Except(SigningAddresses).ToArray();
                    foreach (var address in NodesToRemove)
                    {
                        var ip = Globals.Nodes.Values.Where(x => x.Address == address).Select(x => x.NodeIP).First();
                        if (Globals.Nodes.TryRemove(ip, out var node) && node.Connection != null)
                            await node.Connection.DisposeAsync();
                    }

                    if (Globals.AdjudicateAccount == null)
                    {
                        await Task.Delay(10000);
                        continue;
                    }

                    var DisconnectedPeers = Globals.Nodes.Values.Where(x => x.Address != Globals.AdjudicateAccount.Address && !x.IsConnected).ToArray();
                    if(DisconnectedPeers.Any())
                    {
                        var account = Globals.AdjudicateAccount;
                        var time = TimeUtil.GetTime().ToString();
                        var signature = SignatureService.AdjudicatorSignature(account.Address + ":" + time);
                        var ConnectTasks = new ConcurrentBag<Task>();
                        DisconnectedPeers.ParallelLoop(peer =>
                        {
                            var url = "http://" + peer.NodeIP + ":" + Globals.Port + "/consensus";
                            ConnectTasks.Add(ConsensusClient.ConnectConsensusNode(url, account.Address, time, account.Address, signature));
                        });                        

                        await Task.WhenAll(ConnectTasks);
                    }
                }
                catch (Exception ex)
                {
                }

                await Task.Delay(1000);
            }
        }

        public static async Task ConnectToAdjudicators()
        {
            while(true)
            {
                var delay = Task.Delay(10000);
                try
                {
                    if (Globals.StopAllTimers || string.IsNullOrWhiteSpace(Globals.ValidatorAddress) || Globals.GUIPasswordNeeded)
                    {
                        await delay;
                        continue;
                    }

                    var SigningAddresses = Globals.Signers.Keys.ToHashSet();
                    var Majority = SigningAddresses.Count / 2 + 1;
                    var AdjAddresses = Globals.AdjNodes.Values.Select(x => x.Address).ToHashSet();

                    var NewSigners = SigningAddresses.Except(AdjAddresses).ToArray();
                    if (NewSigners.Any())
                    {
                        foreach (var signer in NewSigners)
                        {
                            if (Globals.AdjBench.TryGetValue(signer, out var bench))
                            {
                                Globals.AdjNodes.TryAdd(bench.IPAddress, new AdjNodeInfo
                                {
                                    Address = bench.RBXAddress,
                                    IpAddress = bench.IPAddress
                                });
                            }
                        }

                        AdjAddresses = Globals.AdjNodes.Values.Select(x => x.Address).ToHashSet();
                    }

                    var NodesToRemove = AdjAddresses.Except(SigningAddresses).ToArray();
                    foreach (var address in NodesToRemove)
                    {
                        var ip = Globals.AdjNodes.Values.Where(x => x.Address == address).Select(x => x.IpAddress).First();
                        if (Globals.AdjNodes.TryRemove(ip, out var node) && node.Connection != null)
                            await node.Connection.DisposeAsync();
                    }

                    var rnd = new Random();                    
                    var NumAdjudicators = Globals.AdjNodes.Values.Where(x => x.IsConnected).Count();
                    if (NumAdjudicators >= Majority && rnd.Next(0, 10000) < 5)
                    {
                        var ip = Globals.AdjNodes.Values.Where(x => x.IsConnected).Skip(rnd.Next(0, Majority)).FirstOrDefault()?.IpAddress;
                        if (Globals.AdjNodes.TryGetValue(ip, out var node) && node.Connection != null)
                            await node.Connection.DisposeAsync();
                        NumAdjudicators = Globals.AdjNodes.Values.Where(x => x.IsConnected).Count();
                    }

                    while(NumAdjudicators > Majority)
                    {
                        var ip = Globals.AdjNodes.Values.Where(x => x.IsConnected).Skip(rnd.Next(0, Majority)).FirstOrDefault()?.IpAddress;
                        if (Globals.AdjNodes.TryGetValue(ip, out var node) && node.Connection != null)
                            await node.Connection.DisposeAsync();
                        NumAdjudicators = Globals.AdjNodes.Values.Where(x => x.IsConnected).Count();
                    }

                    if (NumAdjudicators >= Majority)
                    {
                        await ValidatorService.PerformErrorCountCheck();
                        await delay;
                        continue;
                    }

                    var account = AccountData.GetLocalValidator();
                    var validators = Validators.Validator.GetAll();
                    var validator = validators.FindOne(x => x.Address == account.Address);
                    if (validator != null)
                    {
                        var time = TimeUtil.GetTime().ToString();                            

                            var signature = SignatureService.ValidatorSignature(validator.Address + ":" + TimeUtil.GetTime());                            
                            var NewAdjudicators = Globals.AdjNodes.Values
                                .OrderBy(x => rnd.Next())                                
                                .ToArray();

                        for(var i = 0; i < NewAdjudicators.Length; i++)
                        {
                            var NewAdjudicator = NewAdjudicators[i];
                            if (Globals.AdjNodes.Values.Where(x => x.IsConnected).Count() >= Majority)
                                break;

                            if (NewAdjudicator.IsConnected)
                                continue;

                            var url = "http://" + NewAdjudicator.IpAddress + ":" + Globals.Port + "/adjudicator";
                            await P2PClient.ConnectAdjudicator(url, validator.Address, time, validator.UniqueName, signature);
                        }

                        if (!Globals.AdjNodes.Any())
                            Console.WriteLine("You have no adjudicators. You will not be able to solve blocks.");                        
                    }

                    await ValidatorService.PerformErrorCountCheck();


                }
                catch(Exception ex)
                {
                    Console.WriteLine("Error: {0}", ex.ToString());
                }

                await delay;
            }            
        }
        public static async Task EstablishBeaconReference()
        {
            var beaconRef = BeaconReference.GetBeaconReference();
            if(beaconRef != null)
            {
                var beaconRefRecord = beaconRef.FindAll();
                if(beaconRefRecord.Count() > 0)
                {
                    var rec = beaconRefRecord.First();
                    Globals.BeaconReference = rec;
                }
                else
                {
                    string reference = "";

                    var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
                    var stringChars = new char[16];
                    var random = new Random();

                    for (int i = 0; i < stringChars.Length; i++)
                    {
                        stringChars[i] = chars[random.Next(chars.Length)];
                    }

                    var finalString = new string(stringChars);
                    reference = finalString;

                    BeaconReference br = new BeaconReference {
                        Reference = reference,
                        CreateDate = DateTime.UtcNow
                    };

                    var path = GetPathUtility.GetBeaconPath();
                    var fileExist = File.Exists(path + "beacon_ref.bak");
                    if (!fileExist)
                    {
                        BeaconReference.SaveBeaconReference(br);
                        File.AppendAllText(path + "beacon_ref.bak", reference);
                    }
                    else
                    {
                        string text = File.ReadAllText(path + "beacon_ref.bak");
                        br.Reference = text;
                        BeaconReference.SaveBeaconReference(br, true);
                    }

                    Globals.BeaconReference = br;
                }
            }
            
        }
        internal static async Task DownloadBlocksOnStart()
        {
            var download = true;
            try
            {
                while (download) //this will loop forever till download happens
                {
                    if (Globals.IsResyncing)
                        break;

                    DateTime startTime = DateTime.UtcNow;
                    var result = await P2PClient.GetCurrentHeight();
                    if (result.Item1)
                    {
                        ConsoleWriterService.Output($"Block downloads started on: {startTime.ToLocalTime()}");
                        LogUtility.Log("Block downloads started.", "DownloadBlocksOnStart()-if");
                        await BlockDownloadService.GetAllBlocks();
                    }

                    var lastBlock = Globals.LastBlock;
                    var currentTimestamp = TimeUtil.GetTime(-90);

                    if(lastBlock.Timestamp >= currentTimestamp || Globals.AdjudicateAccount != null || Globals.IsTestNet)
                    {
                        DateTime endTime = DateTime.UtcNow;
                        ConsoleWriterService.Output($"Block downloads finished on: {endTime.ToLocalTime()}");
                        LogUtility.Log("Block downloads finished.", "DownloadBlocksOnStart()-else");
                        download = false; //exit the while.                
                        var accounts = AccountData.GetAccounts();
                        var accountList = accounts.FindAll().ToList();
                        if (accountList.Count() > 0)
                        {
                            var stateTrei = StateData.GetAccountStateTrei();
                            foreach (var account in accountList)
                            {
                                var stateRec = stateTrei.FindOne(x => x.Key == account.Address);
                                if (stateRec != null)
                                {
                                    account.Balance = stateRec.Balance;
                                    accounts.UpdateSafe(account);//updating local record with synced state trei
                                }
                            }
                        }

                    }
                }
                if (!Globals.IsResyncing)
                {                    
                    Globals.StopAllTimers = false;
                    Globals.IsChainSynced = true;
                }
                download = false; //exit the while.
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError($"Unknown Error: {ex.ToString()}", "StartupService.DownloadBlocksOnStart()");
            }
            finally
            {
                Globals.StopAllTimers = false;
            }
        }

        internal static void CheckForDuplicateBlocks()
        {
            var blockChain = BlockchainData.GetBlocks();
            var count = blockChain.Count();
            var HeightIsOkay = count > 0 ? (count - blockChain.Max("Height").AsInt64) == 1 : true;
            if(!HeightIsOkay)
            {
                LogUtility.Log("Duplicate Blocks Found!", "StartupService: dupBlocksList.Count != 0 / meaning dup found!");
                //Reset blocks and all balances and redownload chain. No exception here.
                Console.WriteLine("Duplicate Blocks Found!");
                Globals.DatabaseCorruptionDetected = true;
            }
        }

        internal static void ResetEntireChain()
        {
            var blockChain = BlockchainData.GetBlocks();

            var genesisBlock = BlockchainData.GetGenesisBlock();
            if(genesisBlock != null)
            {
                //put the old chain reference id here to reset chain for ALL nodes
                if (genesisBlock.ChainRefId == "t_Gi9RNxviAq1TmvuPZsZBzdAa8AWVJtNa7cm1dFaT4dWDbdqSNSTh")
                {
                    TransactionData.CreateGenesisTransction();

                    TransactionData.GenesisTransactionsCreated = true;

                    var accounts = AccountData.GetAccounts();
                    var transactions = TransactionData.GetAll();
                    var stateTrei = StateData.GetAccountStateTrei();
                    var worldTrei = WorldTrei.GetWorldTrei();
                    var validators = Validators.Validator.GetAll();
                    var peers = Peers.GetAll();

                    var accountList = accounts.FindAll();
                    if (accountList.Count() > 0)
                    {
                        foreach (var account in accountList)
                        {
                            account.Balance = 0.0M;
                            account.IsValidating = false;
                            accounts.UpdateSafe(account);//resets balances to 0.
                        }
                    }
                    peers.DeleteAllSafe();
                    validators.DeleteAllSafe();
                    transactions.DeleteAllSafe();//delete all local transactions
                    stateTrei.DeleteAllSafe(); //removes all state trei data
                    worldTrei.DeleteAllSafe();  //removes the state trei
                    blockChain.DeleteAllSafe();//remove all blocks

                    try
                    {
                        DbContext.DB.Checkpoint();
                        DbContext.DB_AccountStateTrei.Checkpoint();
                        DbContext.DB_WorldStateTrei.Checkpoint();
                        DbContext.DB_Wallet.Checkpoint();
                        DbContext.DB_Peers.Checkpoint();
                    }
                    catch (Exception ex)
                    {
                        //error saving from db cache
                    }
                }
            }

            
        }

        internal static async void ResetStateTreis()
        {
            var blockChain = BlockchainData.GetBlocks().FindAll();
            var failCount = 0;
            List<Block> failBlocks = new List<Block>();

            var transactions = TransactionData.GetAll();
            var stateTrei = StateData.GetAccountStateTrei();
            var worldTrei = WorldTrei.GetWorldTrei();

            transactions.DeleteAllSafe();//delete all local transactions
            stateTrei.DeleteAllSafe(); //removes all state trei data
            worldTrei.DeleteAllSafe();  //removes the state trei

            DbContext.DB.Checkpoint();
            DbContext.DB_AccountStateTrei.Checkpoint();
            DbContext.DB_WorldStateTrei.Checkpoint();

            var accounts = AccountData.GetAccounts();
            var accountList = accounts.FindAll().ToList();
            if (accountList.Count() > 0)
            {
                foreach (var account in accountList)
                {
                    account.Balance = 0M;
                    accounts.UpdateSafe(account);//updating local record with synced state trei
                }
            }

            foreach (var block in blockChain)
            {
                var result = await BlockchainRescanUtility.ValidateBlock(block, true);
                if(result != false)
                {
                    await StateData.UpdateTreis(block);

                    foreach (Transaction transaction in block.Transactions)
                    {
                        var mempool = TransactionData.GetPool();

                        var mempoolTx = mempool.FindAll().Where(x => x.Hash == transaction.Hash).FirstOrDefault();
                        if (mempoolTx != null)
                        {
                            mempool.DeleteManySafe(x => x.Hash == transaction.Hash);
                        }

                        var account = AccountData.GetAccounts().FindAll().Where(x => x.Address == transaction.ToAddress).FirstOrDefault();
                        if (account != null)
                        {
                            AccountData.UpdateLocalBalanceAdd(transaction.ToAddress, transaction.Amount);
                            var txdata = TransactionData.GetAll();
                            txdata.InsertSafe(transaction);
                        }

                        //Adds sent TX to wallet
                        var fromAccount = AccountData.GetAccounts().FindOne(x => x.Address == transaction.FromAddress);
                        if (fromAccount != null)
                        {
                            var txData = TransactionData.GetAll();
                            var fromTx = transaction;
                            fromTx.Amount = transaction.Amount * -1M;
                            fromTx.Fee = transaction.Fee * -1M;
                            txData.InsertSafe(fromTx);
                            AccountData.UpdateLocalBalance(fromAccount.Address, (transaction.Amount + transaction.Fee));
                        }
                    }
                }
                else
                {
                    //issue with chain and must redownload
                    failBlocks.Add(block);
                    failCount++;
                }
            }

            if(failCount == 0)
            {
                
            }
            else
            {
                //chain is invalid. Delete and redownload
            }
        }

        internal static async void ResetChainToPoint()
        {
            var blockFixHeight = 19941;
            var blocks = BlockchainData.GetBlocks();
            var block = BlockchainData.GetBlockByHeight(blockFixHeight);
            int failCount = 0;
            if(block != null)
            {
                if(block.Hash == "baca9daedafe1b480927e6eefbd366380c0fa2191c444bd246d6f34b43393928")
                {
                    var stateTrei = StateData.GetAccountStateTrei();

                    stateTrei.DeleteAllSafe();
                    DbContext.DB_AccountStateTrei.Checkpoint();

                    blocks.DeleteManySafe(x => x.Height >= blockFixHeight);
                    DbContext.DB.Checkpoint();
                    var blocksFromGenesis = blocks.Find(LiteDB.Query.All(LiteDB.Query.Ascending));

                    foreach (var blk in blocksFromGenesis)
                    {
                        var result = await BlockchainRescanUtility.ValidateBlock(blk);
                        if(result == false)
                        {
                            failCount++;
                        }
                    }

                }
                else
                {
                    //do nothing
                }
            }

            if(failCount > 0)
            {
                Console.WriteLine("Resync Failed. Download whole chain.");
            }
            else
            {
                Console.WriteLine("Resync Completed.");
            }
        }

        internal static void ClearSelfValidator()
        {
            var validators = Validators.Validator.GetAll();
            var validator = validators.FindOne(x => x.NodeIP == "SELF");
            if (validator != null)
            {
                var accounts = AccountData.GetAccounts();
                var account = accounts.FindOne(x => x.Address == validator.Address);

                if(account != null)
                {
                    account.IsValidating = false;
                    accounts.UpdateSafe(account);
                }
                var isDeleted = validators.DeleteSafe(validator.Id);
                if(isDeleted)
                {
                    DbContext.DB_Peers.Checkpoint();//commits from log file
                    //success
                }
            }
        }

        internal static void DisplayValidatorAddress()
        {
            var accounts = AccountData.GetAccounts();
            var myAccount = accounts.FindOne(x => x.IsValidating == true && x.Address != Globals.GenesisAddress);
            if (myAccount != null)
            {
                Globals.ValidatorAddress = myAccount.Address;
                LogUtility.Log("Validator Address set: " + Globals.ValidatorAddress, "StartupService:StartupPeers()");
            }
        }

        internal static async Task StartupMother()
        {
            while (true)
            {
                if (!Globals.ConnectToMother && !string.IsNullOrEmpty(Globals.MotherAddress))
                    return;
                var delay = Task.Delay(13000);
                try
                {
                    var url = "http://" + Globals.MotherAddress + ":" + Globals.Port + "/mother";
                    if (!P2PMotherClient.IsMotherConnected)
                        await P2PMotherClient.ConnectMother(url);

                    if(P2PMotherClient.IsMotherConnected)
                        await P2PMotherClient.SendMotherData();
                }
                catch (Exception ex)
                {
                    ErrorLogUtility.LogError($"Failed to send mother payload {ex.ToString()}", "StartupService.StartupMother()");
                }

                await delay;
            }
        }
        internal static async Task StartupPeers()
        {
            int failCount = 0;
            while (true)
            {
                if (Globals.AdjudicateAccount != null)
                    return;
                var delay = Task.Delay(10000);
                try
                {
                    var ConnectedCount = Globals.Nodes.Values.Where(x => x.IsConnected).Count();
                    if(ConnectedCount < Globals.MaxPeers)
                        await P2PClient.ConnectToPeers();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }

                await delay;
            }
        }
        internal static async Task<bool> DownloadBlocks() //download genesis block
        {
            var peersConnected = await P2PClient.ArePeersConnected();

            if (peersConnected)
            {
                if(Globals.LastBlock.Height == -1)
                {
                    //This just gets first few blocks to start chain off.
                    Console.WriteLine("Downloading Blocks First.");
                    await BlockDownloadService.GetAllBlocks();         
                }
            }
            return true;
        }

        internal static void StartupMenu()
        {
            Console.WriteLine("Wallet Started. Awaiting Command...");
        }

        internal static void MainMenu(bool noAccountMessage = false)
        {
            if (Globals.BasicCLI)
            {
                MainMenuBasic();
            }
            else
            {
                
                try
                {
                    Console.Clear();
                    Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop);
                }
                catch { }
                

                if (Globals.IsTestNet != true)
                {
                    AnsiConsole.Write(
                    new FigletText("RBX Wallet")
                    .LeftAligned()
                    .Color(Color.Blue));
                }
                else
                {
                    AnsiConsole.Write(
                    new FigletText("RBX Wallet - TestNet")
                    .LeftAligned()
                    .Color(Color.Green));
                }

                if (Globals.IsTestNet != true)
                {
                    Console.WriteLine("ReserveBlock Main Menu");
                }
                else
                {
                    Console.WriteLine("ReserveBlock Main Menu **TestNet**");
                }
                Console.WriteLine("|======================================|");
                Console.WriteLine("| 1. Genesis Block (Check)             |");
                Console.WriteLine("| 2. Create Account                    |");
                Console.WriteLine("| 2hd. Create HD Wallet                |");
                Console.WriteLine("| 3. Restore Account                   |");
                Console.WriteLine("| 3hd. Restore HD Wallet               |");
                Console.WriteLine("| 4. Send Coins                        |");
                Console.WriteLine("| 5. Get Latest Block & Metrics        |");
                Console.WriteLine("| 6. Transaction History               |");
                Console.WriteLine("| 7. Wallet Address(es) Info           |");
                Console.WriteLine("| 8. Startup Masternode                |");
                Console.WriteLine("| 9. Search Block                      |");
                Console.WriteLine("| 10. Enable API (Turn On and Off)     |");
                Console.WriteLine("| 11. Stop Masternode                  |");
                Console.WriteLine("| 12. Import Smart Contract (disabled) |");
                Console.WriteLine("| 13. Voting                           |");
                Console.WriteLine("| 14. Exit                             |");
                Console.WriteLine("|======================================|");
                Console.WriteLine("|type /help for menu options           |");
                Console.WriteLine("|type /menu to come back to main area  |");
                Console.WriteLine("|======================================|");

                if (Globals.DuplicateAdjAddr)
                { Console.WriteLine("|Duplicate Address Found Validating!   |"); }
                if (Globals.DuplicateAdjIP)
                { Console.WriteLine("|Duplicate IPAddress Found Validating! |"); }
                if (Globals.NFTFilesReadyEPN)
                {
                    AnsiConsole.MarkupLine("[red]| NFT Files awaiting download!         |[/]");
                    AnsiConsole.MarkupLine("[red]| Please input encrypt password        |[/]");
                }
                if (!Globals.UpToDate)
                {
                    AnsiConsole.MarkupLine("[red]|          **CLI Is Outdated**         |[/]");
                    AnsiConsole.MarkupLine("[red]|Please type /update to download latest|[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[green]|         **CLI Is Up To Date**        |[/]");
                }

                if (noAccountMessage)
                {
                    Console.WriteLine("********************************************************************");
                    AnsiConsole.MarkupLine("[yellow]You do not have any accounts yet. Please choose option 2 to create a new account.[/]");
                }
                if (!Globals.TimeInSync)
                {
                    AnsiConsole.MarkupLine("********************************************************************");
                    AnsiConsole.MarkupLine("[red]|             **Time is out of sync**            |[/]");
                    AnsiConsole.MarkupLine("[red]|Please ensure your system clock is in sync      |[/]");
                    AnsiConsole.MarkupLine("[red]|You may experience issues with clock out of sync|[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[green]| **Time Server shows time is synced** |[/]");
                }
                if (Globals.TimeSyncError)
                {
                    AnsiConsole.MarkupLine("********************************************************************");
                    AnsiConsole.MarkupLine("[red]|             **Failed to Sync Time**            |[/]");
                    AnsiConsole.MarkupLine("[red]|Please ensure your system clock able to sync    |[/]");
                    AnsiConsole.MarkupLine("[red]|You may experience issues with clock out of sync|[/]");
                }
                if (!string.IsNullOrEmpty(Globals.ValidatorAddress))
                {
                    AnsiConsole.MarkupLine("[blue]|          **Validator Active**        |[/]");
                    AnsiConsole.MarkupLine($"[blue]|  {Globals.ValidatorAddress}  |[/]");
                }
                if(!Globals.MemoryOverload)
                {

                    AnsiConsole.MarkupLine($"[darkorange]|            **Memory Usage**          |[/]");
                    AnsiConsole.MarkupLine($"[darkorange]|               {Globals.CurrentMemory} MB              |[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]| *Memory Overload Restart Recommended*|[/]");
                    AnsiConsole.MarkupLine($"[red]|               {Globals.CurrentMemory} MB              |[/]");
                }
            }

            
        }

        internal static void MainMenuBasic(bool noAccountMessage = false)
        {
            try
            {
                Console.Clear();
                Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop);
            }
            catch { }

            if (Globals.DuplicateAdjAddr)
            { Console.WriteLine("|Duplicate Address Found Validating!   |"); }
            if (Globals.DuplicateAdjIP)
            { Console.WriteLine("|Duplicate IPAddress Found Validating! |"); }
            if (Globals.NFTFilesReadyEPN)
            {
                AnsiConsole.MarkupLine("[red]| NFT Files awaiting download!         |[/]");
                AnsiConsole.MarkupLine("[red]| Please input encrypt password        |[/]");
            }
            if (!Globals.UpToDate)
            {
                AnsiConsole.MarkupLine("[red]|          **CLI Is Outdated**         |[/]");
                AnsiConsole.MarkupLine("[red]|Please type /update to download latest|[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[green]|         **CLI Is Up To Date**        |[/]");
            }

            if (noAccountMessage)
            {
                Console.WriteLine("********************************************************************");
                AnsiConsole.MarkupLine("[yellow]You do not have any accounts yet. Please choose option 2 to create a new account.[/]");
            }
            if (!Globals.TimeInSync)
            {
                AnsiConsole.MarkupLine("********************************************************************");
                AnsiConsole.MarkupLine("[red]|             **Time is out of sync**            |[/]");
                AnsiConsole.MarkupLine("[red]|Please ensure your system clock is in sync      |[/]");
                AnsiConsole.MarkupLine("[red]|You may experience issues with clock out of sync|[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[green]| **Time Server shows time is synced** |[/]");
            }
            if (Globals.TimeSyncError)
            {
                AnsiConsole.MarkupLine("********************************************************************");
                AnsiConsole.MarkupLine("[red]|             **Failed to Sync Time**            |[/]");
                AnsiConsole.MarkupLine("[red]|Please ensure your system clock able to sync    |[/]");
                AnsiConsole.MarkupLine("[red]|You may experience issues with clock out of sync|[/]");
            }
            if (!string.IsNullOrEmpty(Globals.ValidatorAddress))
            {
                AnsiConsole.MarkupLine("[blue]|          **Validator Active**        |[/]");
                AnsiConsole.MarkupLine($"[blue]|  {Globals.ValidatorAddress}  |[/]");
            }
        }
    }
}
