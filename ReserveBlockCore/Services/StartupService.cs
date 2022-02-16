using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;

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

        internal static void TestConnect()
        {
            P2PClient.TestLocal();
        }

        internal static void SetBlockchainChainRef()
        {
            BlockchainData.ChainRef = "Gi9RNxviAq1TmvuPZsZBzdAa8AWVJtNa7cm1dFaT4dWDbdqSNSTh";
        }

        internal static void StartupPeers()
        {
            P2PClient.ConnectToPeers();
        }
        //may want to put this in a task to allow use of wallet still? 
        internal static async Task<bool> DownloadBlocks() //download genesis block
        {
            if (P2PClient.ActivePeerList.Count != 0)
            {
                var blocks = BlockData.GetBlocks();
                if(blocks.Count() == 0)
                {
                    Console.WriteLine("Downloading Blocks First.");
                    var block = await P2PClient.GetBlock();
                    Console.WriteLine("Found Block: " + block.Height.ToString());
                    var result = await BlockValidatorService.ValidateBlock(block);
                    if (result == false)
                    {
                        Console.WriteLine("Block was rejected from: " + block.Validator);
                        //Add rejection notice for validator
                    }
                    //while (block.Height != height)
                    //{
                    //    block = await P2PClient.GetBlock();
                    //    var resultLoop = BlockValidatorService.ValidateBlock(block);
                    //    if (resultLoop == false)
                    //    {
                    //        Console.WriteLine("Block was rejected from: " + block.Validator);
                    //        //Add rejection notice for validator
                    //    }
                    //}
                    
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
