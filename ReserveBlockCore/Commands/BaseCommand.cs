using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using ReserveBlockCore.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                case "2":
                    //Insert Method
                    break;
                case "3":
                    //Insert Method
                    break;
                case "4":
                    //Insert Method
                    commandResult = "This feature is coming soon...";
                    break;
                case "5":
                    //Insert Method
                    break;
                case "6":
                    //Insert Method
                    break;
                case "7":
                    commandResult = "This feature is coming soon...";
                    break;
                case "8":
                    //Insert Method
                    break;
                case "9":
                    commandResult = "This feature is coming soon...";
                    break;
                case "10":
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
