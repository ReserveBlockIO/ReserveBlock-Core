using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using ReserveBlockCore.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReserveBlockCore.Models;
using LiteDB;

namespace ReserveBlockCore.Commands
{
    internal class BaseCommand
    {
        internal static string ProcessCommand(string command)
        {
            var commandResult = string.Empty;

            switch (command)
            {
                case "/help":
                    commandResult = "Help Command List Goes Here...";
                    break;
                case "/printvars":
                    StaticVariableUtility.PrintStaticVariables();
                    break;
                case "/exit":
                    commandResult = "_EXIT";
                    break;
                case "/menu":
                    StartupService.MainMenu();
                    break;
                case "/clear":
                    Console.Clear();
                    break;
                case "/backupwallet":
                    BackupUtil.BackupWalletData();
                    Console.WriteLine("Reserve Block Wallet has been backed up.");
                    break;
                case "/restorewallet":
                    Console.WriteLine("Please enter backup file directory... ");
                    string? path = @Console.ReadLine();
                    string[] keys = File.ReadAllLines(path);
                    foreach (string key in keys)
                    {
                        Console.WriteLine("\n" + key);
                        var nrestoredAccount = AccountData.RestoreAccount(key); ;
                        AccountData.WalletInfo(nrestoredAccount);
                    }
                    break;
                case "/mempool":
                    Console.WriteLine("Printing Mempool Results: ");
                    TransactionData.PrintMemPool();
                    break;
                case "1": // Genesis Block (check)
                    var genBlock = BlockchainData.GetGenesisBlock();
                    BlockchainData.PrintBlock(genBlock);
                    break;
                case "2": // Create Account
                    var account = new Account().Build();
                    AccountData.WalletInfo(account);
                    break;
                case "3": // Restore Account
                    Console.WriteLine("Please enter private key... ");
                    var privKey = Console.ReadLine();
                    var restoredAccount = AccountData.RestoreAccount(privKey); ;
                    AccountData.WalletInfo(restoredAccount);
                    break;
                case "4": //Send Coins
                    WalletService.StartSend();
                    break;
                case "5": //Check Address Balance
                    //Insert Method
                    break;
                case "6": //Transaction History
                    List<Transaction> lst = GetListOfTransactions();
                    foreach (Transaction t in lst)
                    {
                        Console.WriteLine("To Address: {0}", t.ToAddress);
                        Console.WriteLine("From Address: {0}", t.FromAddress);
                        Console.WriteLine("Amounr: {0}", t.Amount);
                        Console.WriteLine("Fee: {0}", t.Fee);
                    }
                    Console.ReadLine();
                    Console.WriteLine("done");
                    break;
                case "7": //Account Info
                    AccountData.PrintWalletAccounts();
                    break;
                case "8": //Startup Masternode
                    ValidatorService.DoValidate();
                    break;
                case "9": //Startup Datanode
                    commandResult = "This feature is coming soon...";
                    break;
                case "10": //Enable API
                    Startup.APIEnabled = Startup.APIEnabled == false ? true : false;
                    if (Startup.APIEnabled)
                        Console.WriteLine("Reserveblock API has been turned on...");
                    else
                        Console.WriteLine("Reserveblock API has been turned off...");
                    break;
                case "11": //Stop Masternode
                    ValidatorService.DoMasterNodeStop();
                    break;
                case "12": //Stop Datanode
                    break;
                case "13": //Exit
                    commandResult = "_EXIT";
                    break;
                default:
                    commandResult = "Not a recognized command. Please Try Again...";
                    break;
            }

            return commandResult;

        }

        public static List<Transaction> GetListOfTransactions()
        {
            List<Transaction> lst = new List<Transaction>();
            var info = BlockData.GetBlocks();
            foreach (var item in info.FindAll())
            {
                lst.AddRange(item.Transactions);
            }

            return lst;
        }

    }
}
