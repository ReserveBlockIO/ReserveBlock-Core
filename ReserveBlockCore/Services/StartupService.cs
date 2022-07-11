using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using LiteDB;
using ReserveBlockCore.Beacon;
using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Utilities;
using Spectre.Console;

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
                    var port = Program.Port;
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

        internal static void SetupNodeDictionary()
        {
            P2PClient.NodeDict = new Dictionary<int, string>();
            P2PClient.NodeDict.Add(1, null);
            P2PClient.NodeDict.Add(2, null);
            P2PClient.NodeDict.Add(3, null);
            P2PClient.NodeDict.Add(4, null);
            P2PClient.NodeDict.Add(5, null);
            P2PClient.NodeDict.Add(6, null);
        }

        internal static void ClearValidatorDups()
        {
            ValidatorService.ClearDuplicates();
        }
        internal static void StartupDatabase()
        {
            //Establish block, wallet, ban list, and peers db
            Console.WriteLine("Initializing Reserve Block Database...");
            DbContext.Initialize();
        }

        internal static void HDWalletCheck()
        {
            var check = HDWallet.HDWalletData.GetHDWallet();
            if(check != null)
            {
                Program.HDWallet = true;
            }
        }
        internal static void SetBlockchainChainRef()
        {
            //mainnet
            //BlockchainData.ChainRef = "m_Gi9RNxviAq1TmvuPZsZBzdAa8AWVJtNa7cm1dFaT4dWDbdqSNSTh";

            //testnet
            BlockchainData.ChainRef = "t6_Gi9RNxviAq1TmvuPZsZBzdAa8AWVJtNa7cm1dFaT4dWDbdqSNSTh";
            LogUtility.Log("RBX ChainRef - " + BlockchainData.ChainRef, "Main");

            if (Program.IsTestNet)
            {
                BlockchainData.ChainRef = "t_testnet";
            }
        }

        internal static void SetBlockchainVersion()
        {
            //BlockchainData.BlockVersion = BlockVersionUtility.GetBlockVersion();
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
            Program.BlockHeight = BlockchainData.GetHeight();
            LogUtility.Log("RBX Height - " + Program.BlockHeight.ToString(), "Main");
        }

        internal static void SetLastBlock()
        {
            if(Program.BlockHeight != -1)
            {
                Program.LastBlock = BlockchainData.GetLastBlock();
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
                BeaconServer server = new BeaconServer(GetPathUtility.GetBeaconPath(), Program.Port);
                Thread obj_thread = new Thread(server.StartServer());
                Console.WriteLine("Beacon Started");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
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
                    WalletVersion = Program.CLIVersion  
                };

                adjudicators.Insert(adj1);
            }
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

                    if(minuteDiff > 180.0M)
                    {
                        pool.DeleteMany(x => x.Hash == tx.Hash);
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
                            accounts.Update(account);
                        }
                    }
                }
            }
            

        }

        internal static void SetValidator()
        {
            var accounts = AccountData.GetAccounts();
            var myAccount = accounts.FindOne(x => x.IsValidating == true && x.Address != Program.GenesisAddress);
            if (myAccount != null)
            {
                Program.ValidatorAddress = myAccount.Address;
            }
        }

        internal static async void SetConfigValidator()
        {
            var address = Program.ConfigValidator;
            var uname = Program.ConfigValidatorName;
            var accounts = AccountData.GetAccounts();
            var myAccount = accounts.FindOne(x => x.Address == address);
            if (myAccount != null && myAccount.IsValidating != true)
            {
                var valResult = await ValidatorService.StartValidating(myAccount, uname);
                Program.ValidatorAddress = myAccount.Address;
            }
        }

        internal static void SetSelfAdjudicator()
        {
            var leadAdj = Program.LeadAdjudicator;
            var account = AccountData.GetSingleAccount(leadAdj.Address);
            if(account != null)
            {
                Program.Adjudicate = true;
            }
        }

        internal static async Task SetLeadAdjudicator()
        {
            var adjudicatorLead = Adjudicators.AdjudicatorData.GetLeadAdjudicator();
            if(adjudicatorLead != null)
            {
                Program.LeadAdjudicator = adjudicatorLead;
            }
            else
            {
                Program.LeadAdjudicator = await P2PClient.GetLeadAdjudicator();
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
            var blocks = blockChain.Find(Query.All(Query.Descending)).ToList();

            Program.MemBlocks = blocks.Take(200).ToList();
        }

        public static async Task ConnectoToAdjudicator()
        {
            if(Program.ValidatorAddress != null && Program.ValidatorAddress != "")
            {
                var account = AccountData.GetLocalValidator();
                var validators = Validators.Validator.GetAll();
                var validator = validators.FindOne(x => x.Address == account.Address);
                if(validator != null)
                {

                    BigInteger b1 = BigInteger.Parse(account.PrivateKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
                    PrivateKey privateKey = new PrivateKey("secp256k1", b1);

                    var signature = SignatureService.CreateSignature(validator.Address, privateKey, account.PublicKey);

                    var adjudicator = Adjudicators.AdjudicatorData.GetLeadAdjudicator();
                    if(adjudicator != null)
                    {
                        var url = "http://" + adjudicator.NodeIP + ":" + Program.Port + "/adjudicator";
                        await P2PClient.ConnectAdjudicator(url, validator.Address, validator.UniqueName, signature);
                    }
                    else
                    {
                        Console.WriteLine("You have no adjudicators. You will not be able to solve blocks.");
                    }
                    
                }

            }
        }



        internal static async Task DownloadBlocksOnStart()
        {
            Program.StopAllTimers = true;
            var download = true;
            while(download) //this will loop forever till download happens
            {
                if(Program.IsResyncing == false)
                {
                    var result = await P2PClient.GetCurrentHeight();
                    if (result.Item1 == true)
                    {
                        LogUtility.Log("Block downloads started.", "DownloadBlocksOnStart()-if");
                        Program.BlocksDownloading = true;
                        Program.BlocksDownloading = await BlockDownloadService.GetAllBlocks(result.Item2);
                    }
                    //This is not being reached on some devices. 
                    else
                    {
                        LogUtility.Log("Block downloads finished.", "DownloadBlocksOnStart()-else");
                        Program.BlocksDownloading = false;
                        download = false; //exit the while.
                        Program.StopAllTimers = false;
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
                                    accounts.Update(account);//updating local record with synced state trei
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
            if(Program.IsResyncing == false)
            {
                Program.BlocksDownloading = false;
                Program.StopAllTimers = false;
                Program.IsChainSynced = true;
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
                Program.DatabaseCorruptionDetected = true;
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
                            accounts.Update(account);//resets balances to 0.
                        }
                    }
                    peers.DeleteAll();
                    validators.DeleteAll();
                    transactions.DeleteAll();//delete all local transactions
                    stateTrei.DeleteAll(); //removes all state trei data
                    worldTrei.DeleteAll();  //removes the state trei
                    blockChain.DeleteAll();//remove all blocks

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

            transactions.DeleteAll();//delete all local transactions
            stateTrei.DeleteAll(); //removes all state trei data
            worldTrei.DeleteAll();  //removes the state trei

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
                    accounts.Update(account);//updating local record with synced state trei
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
                            mempool.DeleteMany(x => x.Hash == transaction.Hash);
                        }

                        var account = AccountData.GetAccounts().FindAll().Where(x => x.Address == transaction.ToAddress).FirstOrDefault();
                        if (account != null)
                        {
                            AccountData.UpdateLocalBalanceAdd(transaction.ToAddress, transaction.Amount);
                            var txdata = TransactionData.GetAll();
                            txdata.Insert(transaction);
                        }

                        //Adds sent TX to wallet
                        var fromAccount = AccountData.GetAccounts().FindOne(x => x.Address == transaction.FromAddress);
                        if (fromAccount != null)
                        {
                            var txData = TransactionData.GetAll();
                            var fromTx = transaction;
                            fromTx.Amount = transaction.Amount * -1M;
                            fromTx.Fee = transaction.Fee * -1M;
                            txData.Insert(fromTx);
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

                    stateTrei.DeleteAll();
                    DbContext.DB_AccountStateTrei.Checkpoint();

                    blocks.DeleteMany(x => x.Height >= blockFixHeight);
                    DbContext.DB.Checkpoint();
                    var blocksFromGenesis = blocks.Find(Query.All(Query.Ascending));

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
                    accounts.Update(account);
                }
                var isDeleted = validators.Delete(validator.Id);
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
            SeedNodeService.SeedNodes();
            bool result = false;
            try
            {
                result = await P2PClient.ConnectToPeers();

                if(result == true)
                {
                    var accounts = AccountData.GetAccounts();
                    var myAccount = accounts.FindOne(x => x.IsValidating == true && x.Address != Program.GenesisAddress);
                    if(myAccount != null)
                    {
                        Program.ValidatorAddress = myAccount.Address;
                        LogUtility.Log("Validator Address set: " + Program.ValidatorAddress, "StartupService:StartupPeers()");
                    }
                    else
                    {
                        //No validator account on start up
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            

            if(result == true)
            {
                //Connected to peers
                await BlockchainData.InitializeChain();
            }
            else
            {
                Console.WriteLine("Failed to automatically connect to peers. Please add manually.");
                //Put StartupInitializeChain();
                //Here and once chain fails to connect it will create genesis 
            }
        }
        internal static async Task<bool> DownloadBlocks() //download genesis block
        {
            var peersConnected = await P2PClient.ArePeersConnected();

            if (peersConnected.Item1)
            {
                if(Program.BlockHeight == -1)
                {
                    //This just gets first few blocks to start chain off.
                    Console.WriteLine("Downloading Blocks First.");
                    var blockCol = await P2PClient.GetBlock();

                    if(blockCol.Count() > 0)
                    {
                        foreach(var block in blockCol)
                        {
                            Console.WriteLine("Found Block: " + block.Height.ToString());
                            var result = await BlockValidatorService.ValidateBlock(block);
                            if (result == false)
                            {
                                Console.WriteLine("Block was rejected from: " + block.Validator);
                                //Add rejection notice for validator
                            }
                        }
                    }
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

            if(Program.IsTestNet != true)
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
            
            if(Program.IsTestNet != true)
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
            Console.WriteLine("| 7. Account Info                      |");
            Console.WriteLine("| 8. Startup Masternode                |");
            Console.WriteLine("| 9. Startup Datanode                  |");
            Console.WriteLine("| 10. Enable API (Turn On and Off)     |");
            Console.WriteLine("| 11. Stop Masternode                  |");
            Console.WriteLine("| 12. Stop Datanode                    |");
            Console.WriteLine("| 13. Exit                             |");
            Console.WriteLine("|======================================|");
        }
    }
}
