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
using System.Runtime.InteropServices;
using Spectre.Console;
using ReserveBlockCore.DST;

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
                case "/basic":
                    Globals.BasicCLI = !Globals.BasicCLI;
                    var state = Globals.BasicCLI ? "-->ON<--" : "-->OFF<--";
                    if (!Globals.BasicCLI)
                        StartupService.MainMenu();
                    Console.WriteLine($"Reserveblock Basic CLI has been turned {state}...");
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
                case "/chat":
                    Console.Clear();
                    Globals.StopConsoleOutput = true;
                    _ = Chat.Run();
                    Globals.StopConsoleOutput = false;
                    break;
                case "/update":
                    Globals.StopConsoleOutput = true;
                    await VersionControlService.DownloadLatestRelease();
                    Globals.StopConsoleOutput = false;
                    break;
                case "/val":
                    await BaseCommandServices.ValidatorInfo();
                    break;
                case "/resetval":
                    BaseCommandServices.ResetValidator();
                    break;
                case "/adjinfo":
                    await BaseCommandServices.AdjudicatorInfo();
                    break;
                case "/peers":
                    await BaseCommandServices.PeerInfo();
                    break;
                case "/cinfo":
                    await BaseCommandServices.ConsensusNodeInfo();
                    break;
                case "/vote":
                    Globals.StopConsoleOutput = true;
                    await Voting.Voting.StartVoteProgram();
                    Globals.StopConsoleOutput = false;
                    break;
                case "/benchip":
                    Globals.StopConsoleOutput = true;
                    await BaseCommandServices.BenchIP();
                    Globals.StopConsoleOutput = false;
                    break;
                case "/addsigner":
                    Globals.StopConsoleOutput = true;
                    await BaseCommandServices.AddSigner();
                    Globals.StopConsoleOutput = false;
                    break;
                case "/removesigner":
                    Globals.StopConsoleOutput = true;
                    await BaseCommandServices.RemoveSigner();
                    Globals.StopConsoleOutput = false;
                    break;
                case "/encrypt":
                    Globals.StopConsoleOutput = true;
                    await BaseCommandServices.EncryptWallet();
                    Globals.StopConsoleOutput = false;
                    break;
                case "/decrypt":
                    Globals.StopConsoleOutput = true;
                    await BaseCommandServices.DecryptWallet();
                    Globals.StopConsoleOutput = false;
                    break;
                case "/backupwallet":
                    //BackupUtil.BackupWalletData("Not Yet Added.");
                    Console.WriteLine("Reserve Block Wallet has been backed up.");
                    break;
                case "/mempool":
                    Console.WriteLine("Printing Mempool Results: ");
                    TransactionData.PrintMemPool();
                    break;
                case "/resblocks":
                    Globals.StopConsoleOutput = true;
                    await BaseCommandServices.ResyncBlocks();
                    Globals.StopConsoleOutput = false;
                    break;
                case "/blockdets":
                    Globals.StopConsoleOutput = true;
                    await BaseCommandServices.BlockDetails();
                    Globals.StopConsoleOutput = false;
                    break;
                case "/optlog":
                    Globals.OptionalLogging = !Globals.OptionalLogging;
                    Console.WriteLine($"Optional Logging Switched to: {Globals.OptionalLogging}");
                    break;
                case "/beacon":
                    Globals.StopConsoleOutput = true;
                    BaseCommandServices.CreateBeacon();
                    Globals.StopConsoleOutput = false;
                    break;
                case "/switchbeacon":
                    Globals.StopConsoleOutput = true;
                    BaseCommandServices.SwitchBeaconState();
                    Globals.StopConsoleOutput = false;
                    break;
                case "/unlock":
                    Globals.StopConsoleOutput = true;
                    BaseCommandServices.UnlockWallet();
                    Globals.StopConsoleOutput = false;
                    break;
                case "/addpeer":
                    Globals.StopConsoleOutput = true;
                    BaseCommandServices.AddPeer();
                    Globals.StopConsoleOutput = false;
                    break;
                case "/banpeer":
                    Globals.StopConsoleOutput = true;
                    BaseCommandServices.BanPeer();
                    Globals.StopConsoleOutput = false;
                    break;
                case "/unbanpeer":
                    Globals.StopConsoleOutput = true;
                    BaseCommandServices.UnbanPeer();
                    Globals.StopConsoleOutput = false;
                    break;
                case "/creatednr":
                    Globals.StopConsoleOutput = true;
                    await BaseCommandServices.CreateDnr();
                    Globals.StopConsoleOutput = false;
                    break;
                case "/deletednr":
                    Globals.StopConsoleOutput = true;
                    await BaseCommandServices.DeleteDnr();
                    Globals.StopConsoleOutput = false;
                    break;
                case "/transferdnr":
                    Globals.StopConsoleOutput = true;
                    await BaseCommandServices.TransferDnr();
                    Globals.StopConsoleOutput = false;
                    break;
                case "/findbeacon":
                    Globals.StopConsoleOutput = true;
                    var beaconCount = Globals.Beacons.Values.Count();
                    Globals.StopConsoleOutput = false;
                    ConsoleWriterService.Output($"Beacons found {beaconCount}");
                    break;
                case "/findtx":
                    Globals.StopConsoleOutput = true;
                    await BaseCommandServices.FindTXByHash();
                    Globals.StopConsoleOutput = false;
                    break;
                case "/synctrei":
                    Globals.StopConsoleOutput = true;
                    await BaseCommandServices.SyncTreis();
                    Globals.StopConsoleOutput = false;
                    break;
                case "/setto":
                    BaseCommandServices.SetTrilliumOutput();
                    break;
                case "/mother":
                    Globals.StopConsoleOutput = true;
                    BaseCommandServices.StartMother();
                    Globals.StopConsoleOutput = false;
                    break;
                case "/rps":
                    Globals.StopConsoleOutput = true;
                    BaseCommandServices.RPS();
                    Globals.StopConsoleOutput = false;
                    StartupService.MainMenu();
                    break;
                case "/addbadtx":
                    Globals.StopConsoleOutput = true;
                    BaseCommandServices.AddBadTx();
                    Globals.StopConsoleOutput = false;
                    break;
                case "/removebadtx":
                    Globals.StopConsoleOutput = true;
                    BaseCommandServices.RemoveBadTx();
                    Globals.StopConsoleOutput = false;
                    break;
                case "/restart":
                    Globals.StopConsoleOutput = true;
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        await WindowsUtilities.ClientRestart();
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        await LinuxUtilities.ClientRestart();
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        Console.WriteLine("No restart command for OSX yet.");
                    }
                    else
                    {
                        Console.WriteLine("OS Not detected.");
                    }
                    Globals.StopConsoleOutput = false;
                    break;
                case "/entercode":
                    Globals.StopConsoleOutput = true;
                    BaseCommandServices.EnterCode();
                    Globals.StopConsoleOutput = false;
                    StartupService.MainMenu();
                    break;
                case "/trillium":
                    Globals.StopConsoleOutput = true;
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
                    Globals.StopConsoleOutput = false;
                    break;
                case "1": // Genesis Block (check)
                    var genBlock = BlockchainData.GetGenesisBlock();
                    BlockchainData.PrintBlock(genBlock);
                    break;
                case "2": // Create Account
                    await BaseCommandServices.CreateAddress();
                    Console.WriteLine("Please type /menu to return to mainscreen.");
                    break;
                case "2r": // Create Reserve Account Account
                    Globals.StopConsoleOutput = true;
                    Console.WriteLine("Please type /menu to return to mainscreen.");
                    await BaseCommandServices.CreateReserveAddress();
                    Globals.StopConsoleOutput = false;
                    break;
                case "2hd": // Create HD Wallet
                    Globals.StopConsoleOutput = true;
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
                    Globals.StopConsoleOutput = false;
                    break;
                case "3": // Restore Account
                    Globals.StopConsoleOutput = true;
                    if (Globals.IsWalletEncrypted == true)
                    {
                        if(Globals.EncryptPassword.Length > 0)
                        {
                            Console.WriteLine("Please enter private key... ");
                            try
                            {
                                var rescanForTx = false;
                                var privKey = await ReadLineUtility.ReadLine();
                                if(!string.IsNullOrEmpty(privKey))
                                {
                                    AnsiConsole.MarkupLine("Would you like to rescan block chain to find transactions? ('[bold green]y[/]' for yes and '[bold red]n[/]' for no).");
                                    var rescan = await ReadLineUtility.ReadLine();
                                    if(!string.IsNullOrEmpty(rescan))
                                    {
                                        rescanForTx = rescan.ToLower() == "y" ? true : false;
                                    }
                                    var restoredAccount = await Account.Restore(privKey, rescanForTx);
                                    AccountData.WalletInfo(restoredAccount);
                                }
                            }
                            catch(Exception ex) { }
                            
                        }
                        else
                        {
                            Console.WriteLine("Please enter your wallet encryption password before importing a private key.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Please enter private key... ");
                        try
                        {
                            var privKey = await ReadLineUtility.ReadLine();
                            if (!string.IsNullOrEmpty(privKey))
                            {
                                var restoredAccount = await Account.Restore(privKey);
                                AccountData.WalletInfo(restoredAccount);
                            }
                        }
                        catch (Exception ex) { }
                        Console.WriteLine("Please type /menu to return to mainscreen.");
                    }
                    Globals.StopConsoleOutput = false;
                    break;
                case "3r": // Restore reserve account
                    Globals.StopConsoleOutput = true;
                    BaseCommandServices.RestoreReserveAccount();
                    Globals.StopConsoleOutput = false;
                    break;
                case "3hd": // restore HD Wallet
                    Globals.StopConsoleOutput = true;
                    var mnemonicRestore = BaseCommandServices.RestoreHDWallet();
                    Console.WriteLine("-----------------------HD Wallet Process Result------------------------");
                    Console.WriteLine($"Result: {mnemonicRestore}");
                    Console.WriteLine("----------------------Type /menu to return to menu---------------------");
                    Globals.StopConsoleOutput = false;
                    break;
                case "4": //Send Coins
                    Globals.StopConsoleOutput = true;
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
                    
                    Globals.StopConsoleOutput = false;
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
                    Globals.StopConsoleOutput = true;
                    if (Globals.StopAllTimers == false && Globals.BlocksDownloadSlim.CurrentCount != 0)
                    {
                        if(Globals.IsWalletEncrypted == false)
                        {
                            ValidatorService.DoValidate();
                        }
                        else
                        {
                            if(Globals.EncryptPassword.Length == 0)
                            {
                                Console.WriteLine("Please type in your encryption password first.");
                            }
                            else
                            {
                                ValidatorService.DoValidate();
                            }
                        }
                        
                    }
                    else
                    {
                        Console.WriteLine("Please wait to start. wallet is still activating features.");
                    }
                    Globals.StopConsoleOutput = false;
                    break;
                case "9": //Print specific block
                    Globals.StopConsoleOutput = true;
                    BaseCommandServices.PrintBlock();
                    Globals.StopConsoleOutput = false;
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
                case "12": //SC import
                    ConsoleWriterService.Output("Feature coming soon");
                    break;
                case "13": //voting
                    Globals.StopConsoleOutput = true;
                    await Voting.Voting.StartVoteProgram();
                    Globals.StopConsoleOutput = false;
                    break;
                case "14": //Exit
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
