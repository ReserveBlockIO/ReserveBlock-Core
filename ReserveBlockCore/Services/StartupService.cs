using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReserveBlockCore.Data;

namespace ReserveBlockCore.Services
{
    internal class StartupService
    {
        internal static void StartupDatabase()
        {
            //Establish block, wallet, ban list, and peers db
            Console.WriteLine("Initializing Reserve Block Database...");
            DbContext.Initialize();
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
            Console.WriteLine("| 2. Display Last Block                |");
            Console.WriteLine("| 3. Create Transactions               |");
            Console.WriteLine("| 4. Create NFT                        |");
            Console.WriteLine("| 5. Check Address Balance             |");
            Console.WriteLine("| 6. Transaction History               |");
            Console.WriteLine("| 7. Display NFTs                      |");
            Console.WriteLine("| 8. Startup Masternode                |");
            Console.WriteLine("| 9. Startup Datanode                  |");
            Console.WriteLine("| 10. Exit                             |");
            Console.WriteLine("|======================================|");
        }
    }
}
