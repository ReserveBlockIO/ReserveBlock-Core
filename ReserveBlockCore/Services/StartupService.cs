using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using LiteDB;
using ReserveBlockCore.Data;
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
                    tcpClient.Connect("127.0.0.1", Program.Port);
                    Console.WriteLine("Application already running on port 3338. Please verify only one instance is open.");
                    Thread.Sleep(2000);
                    Environment.Exit(0);

                }
                catch (Exception)
                {
                    Console.WriteLine("Port closed");
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

        internal static void SetBlockchainChainRef()
        {
            //mainnet
            //BlockchainData.ChainRef = "m_Gi9RNxviAq1TmvuPZsZBzdAa8AWVJtNa7cm1dFaT4dWDbdqSNSTh";

            //testnet
            BlockchainData.ChainRef = "t2_Gi9RNxviAq1TmvuPZsZBzdAa8AWVJtNa7cm1dFaT4dWDbdqSNSTh";

            if (Program.IsTestNet)
            {
                BlockchainData.ChainRef = "t_testnet";
            }
        }

        internal static void SetBlockchainVersion()
        {
            //BlockchainData.BlockVersion = BlockVersionUtility.GetBlockVersion();
        }

        internal static void RunRules()
        {
            RuleService.ResetValidators();
            RuleService.ResetFailCounts();
            RuleService.RemoveOldValidators();
        }
        //This is just for the initial launch of chain to help bootstrap known validators. This method will eventually be not needed.
        internal static void SetBootstrapValidators()
        {
            var validators = Validators.Validator.GetAll();

            var val1Check = validators.FindOne(x => x.Address == "RTX8Tg9PJMW6JTTdu7A5aKEDajawo9cr6g");

            if (val1Check == null)
            {
                var validator1 = new Validators
                {
                    Address = "RTX8Tg9PJMW6JTTdu7A5aKEDajawo9cr6g",
                    EligibleBlockStart = 0,
                    Amount = 3010M,
                    FailCount = 0,
                    IsActive = true,
                    NodeIP = "185.199.226.121",
                    Position = 1,
                    Signature = "MEQCIDVBdYv+Wfpil+j6d06JbCuWihrTUHP9xCqdAICVaVdXAiBpkyinNKZANOfz4rkao8KmzO461TevS5YGr8BNAdBBZg==.JTVCpmPPZMCTVyWhZzitGN4hnNT9YyhX5P6nMi15b8YezkrMsiygEnfMxCQdpwUjqwTsKdJBmjPt16NLaeFjnLR",
                    UniqueName = "GenesisValidator1",
                    NodeReferenceId = BlockchainData.ChainRef,
                    WalletVersion = Program.CLIVersion,
                    LastChecked = DateTime.UtcNow
                };

                validators.Insert(validator1);
            }

            var val2Check = validators.FindOne(x => x.Address == "RTC7uEaVWVakHwYQMhMDAyNkxYgjzV9WZq");

            if (val2Check == null)
            {
                var validator2 = new Validators
                {
                    Address = "RTC7uEaVWVakHwYQMhMDAyNkxYgjzV9WZq",
                    EligibleBlockStart = 0,
                    Amount = 1999M,
                    FailCount = 0,
                    IsActive = true,
                    NodeIP = "192.3.3.171",
                    Position = 2,
                    Signature = "MEUCIEVutYCQT5ruAKnh8BeLpNkx5lvKFji00H2R37IiO1YIAiEAgHuHBpcMb+2NJs8SMxCP05JGUQ2glB0bkgmQ9YEtBX0=.5mvvTz8QoF7FXwBufMjjhsyhhefAHcKHvLZQjb7FJqyaMq5JKofg8n8wJSf13kunqXDMWSU66aZCuSvbGpDRkbLZ",
                    UniqueName = "GenesisValidator2",
                    NodeReferenceId = BlockchainData.ChainRef,
                    WalletVersion = Program.CLIVersion,
                    LastChecked = DateTime.UtcNow
                };

                validators.Insert(validator2);
            }
        }

        internal static void SetBootstrapValidatorsTestNet()
        {
            var validators = Validators.Validator.GetAll();

            var val1Check = validators.FindOne(x => x.Address == "xSYaH36ZyFBZGqCJnQocuyBo3aRaav7RGg");

            if (val1Check == null)
            {
                var validator1 = new Validators
                {
                    Address = "xSYaH36ZyFBZGqCJnQocuyBo3aRaav7RGg",
                    EligibleBlockStart = 0,
                    Amount = 1001M,
                    FailCount = 0,
                    IsActive = true,
                    NodeIP = "185.199.226.121",
                    Position = 1,
                    Signature = "MEYCIQDvmKsH3WkDIg6gubCoxSaBFI89G4qNhO2yWBtrZjxPPAIhANwliGMjvGN8EPMyVptNf8wWJxvdM6ltR9alGqnKvkPp.JTVCpmPPZMCTVyWhZzitGN4hnNT9YyhX5P6nMi15b8YezkrMsiygEnfMxCQdpwUjqwTsKdJBmjPt16NLaeFjnLR",
                    UniqueName = "GenesisValidator1",
                    NodeReferenceId = BlockchainData.ChainRef,
                    WalletVersion = Program.CLIVersion,
                    LastChecked = DateTime.UtcNow
                };

                validators.Insert(validator1);
            }

            var val2Check = validators.FindOne(x => x.Address == "xSDZibXgBPGDGKH5EzzkWdLLRPXNm7NMrC");

            if (val2Check == null)
            {
                var validator2 = new Validators
                {
                    Address = "xSDZibXgBPGDGKH5EzzkWdLLRPXNm7NMrC",
                    EligibleBlockStart = 0,
                    Amount = 1001M,
                    FailCount = 0,
                    IsActive = true,
                    NodeIP = "192.3.3.171",
                    Position = 2,
                    Signature = "MEUCIQDxNnLLawh4ua+yq5iPWEKoVi1NreAdj3BUwz2+kGaS8AIgAk0Q9KdMbAluHbtyKHZjtgGzgkc8dO6mJWSgcZEZgg0=.5mvvTz8QoF7FXwBufMjjhsyhhefAHcKHvLZQjb7FJqyaMq5JKofg8n8wJSf13kunqXDMWSU66aZCuSvbGpDRkbLZ",
                    UniqueName = "GenesisValidator2",
                    NodeReferenceId = BlockchainData.ChainRef,
                    WalletVersion = Program.CLIVersion,
                    LastChecked = DateTime.UtcNow
                };

                validators.Insert(validator2);
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

                var validators = Validators.Validator.GetAll();
                var validator = validators.FindOne(x => x.Address == myAccount.Address);
                if(validator != null)
                {
                    validator.IsActive = true;
                    validator.FailCount = 0;
                    validator.LastChecked = DateTime.UtcNow;

                    validators.Update(validator);
                }
            }

            
        }

        internal static void CheckLastBlock()
        {
            try
            {
                var lastBlock = BlockchainData.GetLastBlock();
                var worldTrei = WorldTrei.GetWorldTreiRecord();
                if (lastBlock != null && worldTrei != null)
                {
                    if (worldTrei.StateRoot != lastBlock.StateRoot)
                    {
                        //redownload old block and check the state trei from transactions to see if any were affected and need to be modified.
                    }
                }

                var blocks = BlockchainData.GetBlocks();
                var goodBlock = blocks.FindOne(x => x.Height == 19783 && x.Hash == "26fceca5f99d8775e690b193fed87fb4a55162b5b1c2b6f9bb1ffb13570d9d74");
                if(goodBlock == null)
                {
                    blocks.DeleteMany(x => x.Height >= 19783);
                    DbContext.DB.Checkpoint();

                    //ResetStateTreis();
                }
                
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



        internal static async Task DownloadBlocksOnStart()
        {
            Program.StopAllTimers = true;
            var download = true;
            while(download) //this will loop forever till download happens
            {
                var result = await P2PClient.GetCurrentHeight();
                if (result.Item1 == true)
                {
                    Program.BlocksDownloading = true;
                    Program.BlocksDownloading = await BlockDownloadService.GetAllBlocks(result.Item2);                    
                }
                else
                {
                    Program.BlocksDownloading = false;
                    download = false; //exit the while.
                    Program.StopAllTimers = false;
                    var accounts = AccountData.GetAccounts();
                    var accountList = accounts.FindAll().ToList();
                    if(accountList.Count() > 0)
                    {
                        var stateTrei = StateData.GetAccountStateTrei();
                        foreach(var account in accountList)
                        {
                            var stateRec = stateTrei.FindOne(x => x.Key == account.Address);
                            if(stateRec != null)
                            {
                                account.Balance = stateRec.Balance;
                                accounts.Update(account);//updating local record with synced state trei
                            }
                        }
                    }
                }
            }
            Program.BlocksDownloading = false;
            download = false; //exit the while. 
            Program.StopAllTimers = false;

        }

        internal static void CheckForDuplicateBlocks()
        {
            ///////////////////////////////////////////////////////////////////////
            //These methods will eventually no longer be needed once out of testnet.
            ClearSelfValidator();
            //ResetEntireChain();
            //ResetChainToPoint();
            //
            ///////////////////////////////////////////////////////////////////////

            var blockChain = BlockchainData.GetBlocks();
            var blocks = blockChain.Find(Query.All(Query.Descending)).ToList();
            var dupBlocksList = blocks.GroupBy(x => x.Height).Where(y => y.Count() > 1).Select(z => z.Key).ToList();

            if(dupBlocksList.Count != 0)
            {
                //Reset blocks and all balances and redownload chain. No exception here.
                var accounts = AccountData.GetAccounts();
                var transactions = TransactionData.GetAll();
                var stateTrei = StateData.GetAccountStateTrei();
                var worldTrei = WorldTrei.GetWorldTrei();

                var accountList = accounts.FindAll();
                if(accountList.Count() > 0)
                {
                    foreach(var account in accountList)
                    {
                        account.Balance = 0.0M;
                        accounts.Update(account);//resets balances to 0.
                    }
                }

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

                }
                catch (Exception ex)
                {
                    //error saving from db cache
                }
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
                    SetBootstrapValidators();
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

        internal static async Task BroadcastValidatorOnline()
        {
            var accounts = AccountData.GetAccounts();
            var accountList = accounts.FindAll();
            if(accountList.Count() > 0)
            {
                var account = accountList.Where(x => x.IsValidating == true).FirstOrDefault();
                if(account != null)
                {
                    await P2PClient.BroadcastValidatorOnline(account.Address);
                }
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
                    await P2PClient.GetMasternodes();
                    
                    var accounts = AccountData.GetAccounts();
                    var myAccount = accounts.FindOne(x => x.IsValidating == true && x.Address != Program.GenesisAddress);
                    if(myAccount != null)
                    {
                        Program.ValidatorAddress = myAccount.Address;
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
                var blocks = BlockData.GetBlocks();
                if(blocks.Count() == 0)
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
        internal static void StartupInitializeChain()
        {
            BlockchainData.InitializeChain();
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

            AnsiConsole.Write(
                new FigletText("ReserveBlock Wallet")
                .LeftAligned()
                .Color(Color.Blue));

            Console.WriteLine("ReserveBlock Main Menu");
            Console.WriteLine("|======================================|");
            Console.WriteLine("| 1. Genesis Block (Check)             |");
            Console.WriteLine("| 2. Create Account                    |");
            Console.WriteLine("| 3. Restore Account                   |");
            Console.WriteLine("| 4. Send Coins                        |");
            Console.WriteLine("| 5. Check Address Balance             |");
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
