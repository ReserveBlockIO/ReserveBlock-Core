using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using ReserveBlockCore.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReserveBlockCore.Models;
using ReserveBlockCore.Trillium;
using ReserveBlockCore.P2P;

namespace ReserveBlockCore.Commands
{
    internal class BaseCommand
    {
        internal static async Task<string> ProcessCommand(string command, string? commandParameter = null)
        {
            var commandResult = string.Empty;

            switch (command.ToLower())
            {
                case "/help":
                    BaseCommandServices.PrintHelpMenu();
                    break;
                case "/info":
                    BaseCommandServices.PrintInfo();
                    break;
                case "/debug":
                    StaticVariableUtility.PrintStaticVariables();
                    break;
                case "/printkeys":
                    BaseCommandServices.PrintKeys();
                    break;
                case "/stopco":
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    Console.WriteLine($"Stop Console Output set to: {Globals.StopConsoleOutput}");
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
                case "/val":
                    await BaseCommandServices.ValidatorInfo();
                    break;
                case "/resetval":
                    BaseCommandServices.ResetValidator();
                    break;
                case "/encrypt":
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    await BaseCommandServices.EncryptWallet();
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    break;
                case "/decrypt":
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    await BaseCommandServices.DecryptWallet();
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    break;
                case "/backupwallet":
                    //BackupUtil.BackupWalletData("Not Yet Added.");
                    Console.WriteLine("Reserve Block Wallet has been backed up.");
                    break;
                case "/mempool":
                    Console.WriteLine("Printing Mempool Results: ");
                    TransactionData.PrintMemPool();
                    break;
                case "/recp":
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    BaseCommandServices.ReconnectPeers();
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    break;
                case "/resblocks":
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    await BaseCommandServices.ResyncBlocks();
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    break;
                case "/optlog":
                    Globals.OptionalLogging = !Globals.OptionalLogging;
                    Console.WriteLine($"Optional Logging Switched to: {Globals.OptionalLogging}");
                    break;
                case "/beacon":
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    BaseCommandServices.CreateBeacon();
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    break;
                case "/switchbeacon":
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    BaseCommandServices.SwitchBeaconState();
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    break;
                case "/unlock":
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    BaseCommandServices.UnlockWallet();
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    break;
                case "/addpeer":
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    BaseCommandServices.AddPeer();
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    break;
                case "/banpeer":
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    BaseCommandServices.BanPeer();
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    break;
                case "/unbanpeer":
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    BaseCommandServices.UnbanPeer();
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    break;
                case "/creatednr":
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    await BaseCommandServices.CreateDnr();
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    break;
                case "/deletednr":
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    await BaseCommandServices.DeleteDnr();
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    break;
                case "/transferdnr":
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    await BaseCommandServices.TransferDnr();
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    break;
                case "/findbeacon":
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    var beacons = P2PClient.GetBeacons();
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    ConsoleWriterService.Output($"Beacons found {beacons.Result.Count()}");
                    break;
                case "/trillium":
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    //start trillium
                    if (commandParameter == null)
                    {
                        Console.WriteLine("Please include a command paramter. Ex. '/trillium #reset'");
                    }
                    else
                    {
                        var repl = new TrilliumRepl();
                        var result = repl.Run(commandParameter);
                    }
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    break;

                case "1": // Genesis Block (check)
                    var genBlock = BlockchainData.GetGenesisBlock();
                    BlockchainData.PrintBlock(genBlock);
                    break;
                case "2": // Create Account
                    await BaseCommandServices.CreateAddress();
                    break;
                case "2hd": // Create HD Wallet
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    var mnemonic = BaseCommandServices.CreateHDWallet();
                    if(mnemonic.Contains("Unexpected"))
                    {
                        Console.WriteLine(mnemonic);
                    }
                    else
                    {
                        Console.WriteLine("-----------------------HD Wallet Process Completed------------------------");
                        Console.WriteLine("Be sure to copy this seed phrase down. If lost you cannot recovery funds.");
                        Console.WriteLine($"Mnemonic: {mnemonic}");
                        Console.WriteLine("-----------------------Type /menu to return to menu-----------------------");
                    }
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    break;
                case "3": // Restore Account
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    if (Globals.IsWalletEncrypted == true)
                    {
                        if(Globals.EncryptPassword.Length > 0)
                        {
                            Console.WriteLine("Please enter private key... ");
                            var privKey = Console.ReadLine();
                            var restoredAccount = new Account().Restore(privKey);
                            AccountData.WalletInfo(restoredAccount);
                        }
                        else
                        {
                            Console.WriteLine("Please enter your wallet encryption password before importing a private key.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Please enter private key... ");
                        var privKey = Console.ReadLine();
                        var restoredAccount = new Account().Restore(privKey);
                        AccountData.WalletInfo(restoredAccount);
                    }
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    break;
                case "3hd": // Create HD Wallet
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    var mnemonicRestore = BaseCommandServices.RestoreHDWallet();
                    Console.WriteLine("-----------------------HD Wallet Process Result------------------------");
                    Console.WriteLine($"Result: {mnemonicRestore}");
                    Console.WriteLine("----------------------Type /menu to return to menu---------------------");
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    break;
                case "4": //Send Coins
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    if (Globals.IsWalletEncrypted == true)
                    {
                        if (Globals.EncryptPassword.Length > 0)
                        {
                            await WalletService.StartSend();
                        }
                        else
                        {
                            Console.WriteLine("Please input wallet encryption password");
                        }
                    }
                    else
                    {
                        await WalletService.StartSend();
                    }
                    
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    break;
                case "5": //Get Latest Block
                    var currentBlock = BlockchainData.GetLastBlock();
                    BlockchainData.PrintBlock(currentBlock);
                    break;
                case "6": //Transaction History
                    BaseCommandServices.GetLatestTx();
                    break;
                case "7": //Account Info
                    AccountData.PrintWalletAccounts();
                    break;
                case "8": //Startup Masternode
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    if (Globals.StopAllTimers == false && Globals.BlocksDownloading == 0)
                    {
                        if(Globals.IsWalletEncrypted == false)
                        {
                            ValidatorService.DoValidate();
                        }
                        else
                        {
                            Console.WriteLine("This is an encrypted wallet and cannot have validating turned on at this moment.");
                        }
                        
                    }
                    else
                    {
                        Console.WriteLine("Please wait to start. wallet is still activating features.");
                    }
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    break;
                case "9": //Print specific block
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
                    BaseCommandServices.PrintBlock();
                    Globals.StopConsoleOutput = !Globals.StopConsoleOutput;
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
                    await ValidatorService.DoMasterNodeStop();
                    break;
                case "12": //Stop Datanode
                    ConsoleWriterService.Output("Feature coming soon");
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
