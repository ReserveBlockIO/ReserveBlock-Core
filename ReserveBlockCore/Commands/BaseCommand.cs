using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using ReserveBlockCore.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReserveBlockCore.Models;

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
                case "1":
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
                    var restoredAccount = new Account().Restore(privKey);
                    AccountData.WalletInfo(restoredAccount);
                    break;
                case "4":
                    WalletService.StartSend();
                    break;
                case "5":
                    //Insert Method
                    break;
                case "6":
                    //Insert Method
                    break;
                case "7":
                    AccountData.PrintWalletAccounts();
                    break;
                case "8":
                    //Insert Method
                    break;
                case "9":
                    commandResult = "This feature is coming soon...";
                    break;
                case "10":
                    Startup.APIEnabled = Startup.APIEnabled == false ? true : false;
                    if (Startup.APIEnabled)
                        Console.WriteLine("Reserveblock API has been turned on...");
                    else
                        Console.WriteLine("Reserveblock API has been turned off...");
                    break;
                case "11":
                    //Insert Method
                    break;

                default:
                    commandResult = "Not a recognized command. Please Try Again...";
                    break;
            }

            return commandResult;

        }


    }
}
