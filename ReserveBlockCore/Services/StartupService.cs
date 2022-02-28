using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiteDB;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using Spectre.Console;

namespace ReserveBlockCore.Services
{
    internal class StartupService
    {
        internal static void StartupDatabase()
        {
            //Establish block, wallet, ban list, and peers db
            Console.WriteLine("Initializing Reserve Block Database...");
            if(Startup.IsTestNet == true)
            {
                DbContext.InitializeTest();
            }
            else
            {
                DbContext.Initialize();
            }
            
        }

        internal static void SetBlockchainChainRef()
        {
            //mainnet
            //BlockchainData.ChainRef = "m_Gi9RNxviAq1TmvuPZsZBzdAa8AWVJtNa7cm1dFaT4dWDbdqSNSTh";

            //testnet
            BlockchainData.ChainRef = "t_Gi9RNxviAq1TmvuPZsZBzdAa8AWVJtNa7cm1dFaT4dWDbdqSNSTh";
        }

        internal static void StartupMemBlocks()
        {
            var blockChain = BlockchainData.GetBlocks();
            var blocks = blockChain.Find(Query.All(Query.Descending)).ToList();

            Program.MemBlocks = blocks.Take(15).ToList();
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
            
        }

        internal static void CheckForDuplicateBlocks()
        {
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
                    DbContext.DB.Rebuild();
                    DbContext.DB_AccountStateTrei.Rebuild();
                    DbContext.DB_WorldStateTrei.Rebuild();
                    DbContext.DB_Wallet.Rebuild();

                }
                catch (Exception ex)
                {
                    //error saving from db cache
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

            Console.WriteLine("ReserverBlock Main Menu");
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
