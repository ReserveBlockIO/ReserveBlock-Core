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
                    commandResult = "Help Command List Goes Here... Coming soon";
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
                    BackupUtil.BackupWalletData("Not Yet Added.");
                    Console.WriteLine("Reserve Block Wallet has been backed up.");
                    break;
                case "/mempool":
                    Console.WriteLine("Printing Mempool Results: ");
                    TransactionData.PrintMemPool();
                    break;
                case "/recp":
                    BaseCommandServices.ReconnectPeers();
                    break;
                case "/optlog":
                    Program.OptionalLogging = true;
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
                    var restoredAccount = new Account().Restore(privKey);
                    AccountData.WalletInfo(restoredAccount);
                    break;
                case "4": //Send Coins
                    WalletService.StartSend();
                    break;
                case "5": //Get Latest Block
                    var currentBlock = BlockchainData.GetLastBlock();
                    BlockchainData.PrintBlock(currentBlock);
                    break;
                case "6": //Transaction History
                    //Insert Method
                    break;
                case "7": //Account Info
                    AccountData.PrintWalletAccounts();
                    break;
                case "8": //Startup Masternode
                    if(Program.StopAllTimers == false && Program.BlocksDownloading == false)
                    {
                        ValidatorService.DoValidate();
                    }
                    else
                    {
                        Console.WriteLine("Please wait to start. wallet is still activating features.");
                    }
                    
                    break;
                case "9": //Startup Datanode
                    commandResult = "This feature is coming soon...";
                    break;
                case "10": //Enable API
                    Startup.APIEnabled = Startup.APIEnabled == false ? true : false;
                    if (Startup.APIEnabled)
                    {
                        Console.WriteLine("Reserveblock API has been turned -->ON<--...");
                        LogUtility.Log("Reserveblock API has been turned -->ON<--", "BaseCommands");
                    }
                    else
                    {
                        Console.WriteLine("Reserveblock API has been turned -->OFF<--...");
                        LogUtility.Log("Reserveblock API has been turned -->OFF<--", "BaseCommands");
                    }
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


    }
}
