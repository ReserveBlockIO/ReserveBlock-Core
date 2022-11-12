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
                    Console.WriteLine($"Application already running on port {port}. Please verify only one instance is open.");
                    LogUtility.Log($"CLI Already Running on port {port}. Closing new instance.", "StartupService.AnotherInstanceCheck()");
                    Thread.Sleep(2000);
                    Environment.Exit(0);
                }
                catch (Exception)
                {
                    Console.WriteLine("Application Starting...");
                }
            }
        }

        //Only needed for bootstrapping
        internal static async Task ConnectToSinglePeer()
        {
            var url = @"http://127.0.0.1:" + Globals.Port + "/blockchain";
            try
            {
                var hubConnection = new HubConnectionBuilder()
                        .WithUrl(url, options =>
                        {

                        })
                        .WithAutomaticReconnect()
                        .Build();

                var IPAddress = url.Replace("http://", "").Replace("/blockchain", "");
                hubConnection.On<string, string>("GetMessage", async (message, data) =>
                {
                    if (message == "blk" || message == "IP")
                    {
                        if (data?.Length > 1179648)
                            return;

                        if (Globals.Nodes.TryGetValue(IPAddress, out var node))
                        {
                            var now = TimeUtil.GetMillisecondTime();
                            var prevPrevTime = Interlocked.Exchange(ref node.SecondPreviousReceiveTime, node.PreviousReceiveTime);
                            if (now - prevPrevTime < 5000)
                            {
                                Peers.BanPeer(IPAddress, IPAddress + ": Sent blocks too fast to peer.", "GetMessage");
                                return;
                            }
                            Interlocked.Exchange(ref node.PreviousReceiveTime, now);
                        }
                        // if someone calls in more often than 2 times in 15 seconds ban them

                        if (message != "IP")
                        {
                            await NodeDataProcessor.ProcessData(message, data, IPAddress);
                        }
                        else
                        {
                            var IP = data.ToString();
                            if (Globals.ReportedIPs.TryGetValue(IP, out int Occurrences))
                                Globals.ReportedIPs[IP]++;
                            else
                                Globals.ReportedIPs[IP] = 1;
                        }
                    }
                });

                await hubConnection.StartAsync().WaitAsync(new TimeSpan(0, 0, 8));

                Globals.Nodes[IPAddress] = new NodeInfo
                {
                    Connection = hubConnection,
                    NodeIP = IPAddress,
                    NodeHeight = 0,
                    NodeLastChecked = null,
                    NodeLatency = 0,
                    IsSendingBlock = 0,
                    SendingBlockTime = 0,
                    TotalDataSent = 0
                };

                var node = Globals.Nodes[IPAddress];
                (node.NodeHeight, node.NodeLastChecked, node.NodeLatency) = await P2PClient.GetNodeHeight(node);

                    
            }
            catch { }
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
            Globals.BannedIPs = new ConcurrentDictionary<string, bool>(
                peerDb.Find(x => x.IsBanned).ToArray().ToDictionary(x => x.PeerIP, x => true));
        }

        internal static void SetAdjudicatorAddresses()
        {
            Globals.LastBlock = BlockchainData.GetLastBlock() ?? new Block { Height = -1 };

            Globals.AdjudicatorAddresses = new ConcurrentDictionary<string, bool>
            {
                ["xBRzJUZiXjE3hkrpzGYMSpYCHU1yPpu8cj"] = true,
                ["xBRNST9oL8oW6JctcyumcafsnWCVXbzZnr"] = true,
                ["xBRKXKyYQU5k24Rmoj5uRkqNCqJxxci5tC"] = true,
                ["xBRqxLS81HrR3bGRpDa4xTfAEvx7skYDGq"] = true,
                ["xBRS3SxqLQtEtmqZ1BUJiobjUzwufwaAnK"] = true,
            };

            var Accounts = AccountData.GetAccounts().FindAll().ToArray();
            Globals.AdjudicateAccount = Accounts.Where(x => Globals.AdjudicatorAddresses.ContainsKey(x.Address)).FirstOrDefault();
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
            //mainnet
            //BlockchainData.ChainRef = "m_Gi9RNxviAq1TmvuPZsZBzdAa8AWVJtNa7cm1dFaT4dWDbdqSNSTh";

            //testnet
            BlockchainData.ChainRef = "m1_Gi9RNxviAq1TmvuPZsZBzdAa8AWVJtNa7cm1dFaT4dWDbdqSNSTh";
            LogUtility.Log("RBX ChainRef - " + BlockchainData.ChainRef, "Main");

            if (Globals.IsTestNet)
            {
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
        internal static void RunRules()
        {
            //RuleService.ResetValidators();
            //RuleService.ResetFailCounts();
            //RuleService.RemoveOldValidators();
        }

        internal static void StartBeacon()
        {
            try
            {
                var beaconInfo = BeaconInfo.GetBeaconInfo();
                if(beaconInfo != null)
                {
                    var port = Globals.Port + 10000; //23338 - mainnet
                    if (Globals.IsTestNet == true)
                    {
                        port = port + 10000; //33338 - testnet
                    }

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

        //This is just for the initial launch of chain to help bootstrap known validators. This method will eventually be not needed.
        internal static void SetBootstrapAdjudicator()
        {
            var adjudicators = Adjudicators.AdjudicatorData.GetAll();
            var adjudicator = adjudicators.FindOne(x => x.Address == "RBXpH37qVvNwzLjtcZiwEnb3aPNG815TUY");
            if(adjudicator == null)
            {
                Adjudicators adj1 = new Adjudicators {
                    Address = "RBXpH37qVvNwzLjtcZiwEnb3aPNG815TUY",
                    IsActive = true,
                    IsLeadAdjuidcator = true,
                    LastChecked = DateTime.UtcNow,
                    NodeIP = "173.254.253.106",
                    Signature = "MEYCIQDCNDRZ7ovAH7/Ec3x0TP0i1S8OODWE4aKnxisnUnxP4QIhAI8WULPVZC8LZ+4GmQMmthN50WRZ3sswIXjIGoHMv7EE.2qwMbg8SyKNWj1zKLj8qosEMNDHXEpecL46sx8mkkE4E1V212UX6DcPTY6YSdgZLjbvjM5QBX9JDKPtu5wZh6qvj",
                    UniqueName = "Trillium Adjudicator 1",
                    WalletVersion = Globals.CLIVersion  
                };

                adjudicators.InsertSafe(adj1);
            }

            if(Globals.IsTestNet == true)
            {
                var test_adjudicator = adjudicators.FindOne(x => x.Address == "xBRS3SxqLQtEtmqZ1BUJiobjUzwufwaAnK");
                if (test_adjudicator == null)
                {
                    Adjudicators adjTest = new Adjudicators
                    {
                        Address = "xBRS3SxqLQtEtmqZ1BUJiobjUzwufwaAnK",
                        IsActive = true,
                        IsLeadAdjuidcator = true,
                        LastChecked = DateTime.UtcNow,
                        NodeIP = "162.248.14.123",
                        Signature = "MEYCIQDCNDRZ7ovAH7/Ec3x0TP0i1S8OODWE4aKnxisnUnxP4QIhAI8WULPVZC8LZ+4GmQMmthN50WRZ3sswIXjIGoHMv7EE.2qwMbg8SyKNWj1zKLj8qosEMNDHXEpecL46sx8mkkE4E1V212UX6DcPTY6YSdgZLjbvjM5QBX9JDKPtu5wZh6qvj",
                        UniqueName = "Trillium Adjudicator TestNet",
                        WalletVersion = Globals.CLIVersion
                    };

                    adjudicators.InsertSafe(adjTest);
                }
            }

            foreach(var adj in adjudicators.FindAll().ToArray())
            {
                Globals.AdjNodes[adj.NodeIP] = new AdjNodeInfo { Address = adj.Address, IpAddress = adj.NodeIP };
            }
        } 

        internal static void BootstrapBeacons()
        {
            var locators = new List<string>();
            BeaconInfo.BeaconInfoJson beaconLoc1 = new BeaconInfo.BeaconInfoJson
            {
                IPAddress = "162.248.14.123",
                Port = Globals.IsTestNet != true ? Globals.Port + 10000 : Globals.Port + 20000,
                Name = "RBX Beacon 1",
                BeaconUID = "Foundation Beacon 1"
            };

            var beaconLocJson1 = JsonConvert.SerializeObject(beaconLoc1);            
            Globals.Locators[beaconLoc1.BeaconUID] = beaconLocJson1.ToBase64();

            BeaconInfo.BeaconInfoJson beaconLoc2 = new BeaconInfo.BeaconInfoJson
            {
                IPAddress = "162.251.121.150",
                Port = Globals.IsTestNet != true ? Globals.Port + 10000 : Globals.Port + 20000,
                Name = "RBX Beacon 2",
                BeaconUID = "Foundation Beacon 2"

            };
            var beaconLocJson2 = JsonConvert.SerializeObject(beaconLoc2);            
            //Globals.Locators[beaconLoc2.BeaconUID] = beaconLocJson2.ToBase64();

            BeaconInfo.BeaconInfoJson beaconLoc3 = new BeaconInfo.BeaconInfoJson
            {
                IPAddress = "185.199.226.121",
                Port = Globals.IsTestNet != true ? Globals.Port + 10000 : Globals.Port + 20000,
                Name = "RBX Beacon 3",
                BeaconUID = "Foundation Beacon 3"

            };

            var beaconLocJson3 = JsonConvert.SerializeObject(beaconLoc3);
            //Globals.Locators[beaconLoc3.BeaconUID] = beaconLocJson3.ToBase64();
        }
        internal static void ClearStaleMempool()
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

                    if(minuteDiff > 120.0M)
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
        internal static async Task GetAdjudicatorPool()
        {
            //add seed nodes
            SeedNodeService.SeedNodes();
            await NodeConnector.StartNodeAdjPoolConnecting();

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
            Globals.MemBlocks = new ConcurrentQueue<Block>(blockChain.Find(LiteDB.Query.All(LiteDB.Query.Descending), 0, 300));
        }

        public static async Task ConnectoToConsensusNodes()
        {
            if (Globals.AdjudicateAccount == null)
                return;

            var account = Globals.AdjudicateAccount;
            var accPrivateKey = GetPrivateKeyUtility.GetPrivateKey(account.PrivateKey, account.Address);

            BigInteger b1 = BigInteger.Parse(accPrivateKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
            PrivateKey privateKey = new PrivateKey("secp256k1", b1);

            var signature = SignatureService.CreateSignature(account.Address, privateKey, account.PublicKey);
            
            var CurrentAddresses = Globals.ConsensusNodes.Values.Where(x => x.IsConnected).Select(x => x.Address).ToHashSet();

            await Globals.ConsensusNodes.Values.Select(adjudicator =>
            {
                if (adjudicator.IsConnected)
                    return Task.FromResult(true);
                var url = "http://" + adjudicator.IpAddress + ":" + Globals.Port + "/consensus";
                return ConsensusClient.ConnectConsensusNode(url, account.Address, account.Address, signature);
            })
            .WhenAtLeast(x => x, ConsensusClient.Majority() - 1);
          
            if (!Globals.ConsensusNodes.Values.Any(x => x.IsConnected))
                Console.WriteLine("You have no consensus nodes.");
        }

        public static async Task ConnectoToAdjudicators()
        {
            if(!string.IsNullOrWhiteSpace(Globals.ValidatorAddress))
            {
                var account = AccountData.GetLocalValidator();
                var validators = Validators.Validator.GetAll();
                var validator = validators.FindOne(x => x.Address == account.Address);
                if(validator != null)
                {

                    var accPrivateKey = GetPrivateKeyUtility.GetPrivateKey(account.PrivateKey, account.Address);

                    BigInteger b1 = BigInteger.Parse(accPrivateKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
                    PrivateKey privateKey = new PrivateKey("secp256k1", b1);

                    var signature = SignatureService.CreateSignature(validator.Address, privateKey, account.PublicKey);

                    //xBRS3SxqLQtEtmqZ1BUJiobjUzwufwaAnK
                    if (Globals.LastBlock.Height < Globals.BlockLock)
                    {
                        var LeadAdjudicators = Globals.AdjNodes.Values.Where(x => !x.IsConnected && x.Address == Globals.LeadAddress).ToArray();
                        foreach (var adjudicator in LeadAdjudicators)
                        {
                            var url = "http://" + adjudicator.IpAddress + ":" + Globals.Port + "/adjudicator";
                            await P2PClient.ConnectAdjudicator(url, validator.Address, validator.UniqueName, signature);
                        }
                    }
                    else
                    {
                        var rnd = new Random();
                        var CurrentAddresses = Globals.AdjNodes.Values.Where(x => x.IsConnected).Select(x => x.Address).ToHashSet();
                        var adjudicators = Globals.AdjNodes.Values
                            .Where(x => !CurrentAddresses.Contains(x.Address))
                            .OrderBy(x => rnd.Next())
                            .Take(2 - CurrentAddresses.Count)
                            .ToArray();

                        foreach (var adjudicator in adjudicators)
                        {
                            var url = "http://" + adjudicator.IpAddress + ":" + Globals.Port + "/adjudicator";
                            await P2PClient.ConnectAdjudicator(url, validator.Address, validator.UniqueName, signature);
                        }

                        if (!adjudicators.Any())
                            Console.WriteLine("You have no adjudicators. You will not be able to solve blocks.");
                    }
                }

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

        public static async Task ConnectoToBeacon()
        {
            if(Globals.AdjudicateAccount == null)
            {                
                if (Globals.Locators.Any())
                {
                    var beacon = Globals.Locators.Values.FirstOrDefault();
                    var beaconDataJsonDes = JsonConvert.DeserializeObject<BeaconInfo.BeaconInfoJson>(beacon.ToStringFromBase64());
                    if (beaconDataJsonDes != null)
                    {
                        var port = Globals.IsTestNet != true ? Globals.Port + 10000 : Globals.Port + 20000;
                        var url = "http://" + beaconDataJsonDes.IPAddress + ":" + Globals.Port + "/beacon";
                        await P2PClient.ConnectBeacon(url);
                    }
                }
                else
                {
                    Console.WriteLine("You have no remote beacons.");
                }
            }
        }

        internal static async Task DownloadBlocksOnStart()
        {
            Globals.StopAllTimers = true;
            var download = true;
            while(download) //this will loop forever till download happens
            {
                if(Globals.IsResyncing == false)
                {
                    DateTime startTime = DateTime.UtcNow;
                    var result = await P2PClient.GetCurrentHeight();
                    if (result.Item1 == true)
                    {
                        ConsoleWriterService.Output($"Block downloads started on: {startTime.ToLocalTime()}");
                        LogUtility.Log("Block downloads started.", "DownloadBlocksOnStart()-if");
                        await BlockDownloadService.GetAllBlocks();
                    }
                    //This is not being reached on some devices. 
                    else
                    {
                        var lastBlock = Globals.LastBlock;
                        var currentTimestamp = TimeUtil.GetTime(-60);

                        if(true)
                        {
                            DateTime endTime = DateTime.UtcNow;
                            ConsoleWriterService.Output($"Block downloads finished on: {endTime.ToLocalTime()}");
                            LogUtility.Log("Block downloads finished.", "DownloadBlocksOnStart()-else");
                            download = false; //exit the while.
                            Globals.StopAllTimers = false;
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
                }
                else
                {
                    download = false;
                }
                
            }
            if(Globals.IsResyncing == false)
            {
                Globals.BlocksDownloading = 0;
                Globals.StopAllTimers = false;
                Globals.IsChainSynced = true;
            }
            download = false; //exit the while. 
        }

        internal static void CheckForDuplicateBlocks()
        {
            //ClearSelfValidator();

            var blockChain = BlockchainData.GetBlocks();
            var blocks = blockChain.FindAll().ToList();
            var dupBlocksList = blocks.GroupBy(x => x.Height).Where(y => y.Count() > 1).Select(z => z.Key).ToList();

            if(dupBlocksList.Count != 0)
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

                    //re-add bootstrap validators
                    SetBootstrapAdjudicator();
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
                    StateData.UpdateTreis(block);

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
        internal static async Task StartupPeers()
        {
            //add seed nodes
            //This is being done for adj pool now. It will already exist
            //SeedNodeService.SeedNodes();
            bool result = false;
            bool peersConnected = false;
            int failCount = 0;
            while (!peersConnected)
            {
                try
                {
                    if(failCount > 60)
                    {
                        Console.WriteLine($"Failed to connect to any peers. trying again in 60 seconds.");
                        Thread.Sleep(new TimeSpan(0, 0, 60));
                    }
                    else if(failCount >120)
                    {
                        Console.WriteLine($"Failed to connect to any peers. trying again in 120 seconds.");
                        Thread.Sleep(new TimeSpan(0, 0, 120));
                    }

                    AnsiConsole.MarkupLine("[bold yellow]Attempting to connect to peers...[/]");
                    result = await P2PClient.ConnectToPeers();

                    if (result == true)
                    {
                        peersConnected = true;
                        Console.WriteLine(" ");
                        AnsiConsole.MarkupLine("[bold green]Connected to Peers...[/]");
                        var accounts = AccountData.GetAccounts();
                        var myAccount = accounts.FindOne(x => x.IsValidating == true && x.Address != Globals.GenesisAddress);
                        if (myAccount != null)
                        {
                            Globals.ValidatorAddress = myAccount.Address;
                            LogUtility.Log("Validator Address set: " + Globals.ValidatorAddress, "StartupService:StartupPeers()");
                        }
                        else
                        {
                            //No validator account on start up
                        }
                        failCount = 0;
                    }
                    else
                    {
                        failCount += 1;
                        Console.WriteLine($"Failed to connect to any peers. trying again.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
            
            

            if(result == true)
            {
                //Connected to peers
                //Only needed for genesis 
                //await BlockchainData.InitializeChain();
            }
            else
            {
                Console.WriteLine("Failed to automatically connect to peers. Please add manually.");
                //Put StartupInitializeChain();
                //Here and once chain fails to connect it will create genesis 
                //await BlockchainData.InitializeChain();
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
            Console.WriteLine("Starting up Reserve Block Wallet...");
            
            
            //Give thread a moment to recover.
            Thread.Sleep(1000);

            Console.WriteLine("Wallet Started. Awaiting Command...");
        }

        internal static void MainMenu()
        {
            Console.Clear();
            Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop);

            if(Globals.IsTestNet != true)
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
            
            if(Globals.IsTestNet != true)
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
            Console.WriteLine("| 5. Get Latest Block                  |");
            Console.WriteLine("| 6. Transaction History               |");
            Console.WriteLine("| 7. Wallet Address(es) Info           |");
            Console.WriteLine("| 8. Startup Masternode                |");
            Console.WriteLine("| 9. Search Block                      |");
            Console.WriteLine("| 10. Enable API (Turn On and Off)     |");
            Console.WriteLine("| 11. Stop Masternode                  |");
            Console.WriteLine("| 12. Import Smart Contract (disabled) |");
            Console.WriteLine("| 13. Exit                             |");
            Console.WriteLine("|======================================|");
            Console.WriteLine("|type /help for menu options           |");
            Console.WriteLine("|type /menu to come back to main area  |");
            Console.WriteLine("|======================================|");
        }
    }
}
