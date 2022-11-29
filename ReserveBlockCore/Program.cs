global using ReserveBlockCore.Extensions;

using ReserveBlockCore.Commands;
using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;

namespace ReserveBlockCore
{
    class Program
    {
        #region Main
        static async Task Main(string[] args)
        {
            DateTime originDate = new DateTime(2022, 1, 1);
            DateTime currentDate = DateTime.Now;

            //Forced Testnet
            Globals.IsTestNet = true;
            var argList = args.ToList();
            if (args.Length != 0)
            {
                argList.ForEach(x => {
                    var argC = x.ToLower();
                    if (argC == "testnet")
                    {
                        //Launch testnet
                        Globals.IsTestNet = true;
                    }
                });
            }

            Globals.Platform = PlatformUtility.GetPlatform();

            Config.Config.EstablishConfigFile();
            var config = Config.Config.ReadConfigFile();
            Config.Config.ProcessConfig(config);

            var dateDiff = (int)Math.Round((currentDate - originDate).TotalDays);
            Globals.BuildVer = dateDiff;

            Globals.CLIVersion = Globals.MajorVer.ToString() + "." + Globals.MinorVer.ToString() + "." + Globals.BuildVer.ToString() + "-beta";
            var logCLIVer = Globals.CLIVersion.ToString();

            LogUtility.Log(logCLIVer, "Main", true);
            LogUtility.Log($"RBX Wallet - {logCLIVer}", "Main");

            NFTLogUtility.Log(logCLIVer, "Main", true);
            
            NFTLogUtility.Log($"RBX NFT ver. - {logCLIVer}", "Main");

            StartupService.AnotherInstanceCheck();

            StartupService.StartupDatabase();// initializes databases

            await DbContext.CheckPoint();

            StartupService.SetBlockchainChainRef(); // sets blockchain reference id
            StartupService.CheckBlockRefVerToDb();
            StartupService.HDWalletCheck();// checks for HD wallet
            StartupService.EncryptedWalletCheck(); //checks if wallet is encrypted

            Globals.BlockLock = Globals.IsTestNet == true ? 92 : 4000000;

            //To update this go to project -> right click properties -> go To debug -> general -> open debug launch profiles
            if (args.Length != 0)
            {
                argList.ForEach(async x => {
                    var argC = x.ToLower();
                    if (argC == "enableapi")
                    {
                        Startup.APIEnabled = true; //api disabled by default
                    }
                    if (argC == "hidecli")
                    {
                        ProcessStartInfo start = new ProcessStartInfo();
                        start.FileName = Directory.GetCurrentDirectory() + @"\RBXCore\ReserveBlockCore.exe";
                        start.WindowStyle = ProcessWindowStyle.Hidden; //Hides GUI
                        start.CreateNoWindow = true; //Hides console
                        start.Arguments = "enableapi";

                        Globals.proc.StartInfo = start;
                        Globals.proc.Start();

                        Environment.Exit(0);
                    }
                    if (argC == "gui")
                    {
                        //launch gui
                    }
                    if (argC == "testurl")
                    {
                        //Launch testnet
                        Globals.TestURL = true;
                    }
                    if (argC.Contains("privkey"))
                    {
                        try
                        {
                            var keySplit = argC.Split(new char[] { '=' });
                            var privateKey = keySplit[1];
                            var account = await AccountData.RestoreAccount(privateKey);
                            if (account != null)
                            {
                                Console.WriteLine("Account Loaded: " + account.Address);
                            }

                        }
                        catch (Exception ex)
                        {
                            //bad key
                        }
                    }
                });
            }

            StartupService.SetAdjudicatorAddresses();

            //Temporary for TestNet------------------------------------
            //SeedNodeService.SeedNodes();
            //var nodeIp = await SeedNodeService.PingSeedNode();
            //await SeedNodeService.GetSeedNodePeers(nodeIp);
            //Temporary for TestNet------------------------------------

            StartupService.SetBlockHeight();
            StartupService.SetLastBlock();

            var httpClientBuilder = Host.CreateDefaultBuilder(args)
                     .ConfigureServices(services =>
                     {
                         services.AddHttpClient();
                         services.AddTransient<HttpService>();
                     })
                     .Build();

            await httpClientBuilder.StartAsync();
            Globals.HttpClientFactory = httpClientBuilder.Services.GetRequiredService<HttpService>().HttpClientFactory();

            //This is for consensus start.
            StartupService.SetBootstrapAdjudicator(); //sets initial validators from bootstrap list.
            await StartupService.GetAdjudicatorPool();
            _ = Globals.LastBlock.Height > Globals.BlockLock ? ClientCallService.DoWorkV3() : Task.CompletedTask;
            StartupService.DisplayValidatorAddress();
            _ = StartupService.StartupPeers();
            _ = StartupService.DownloadBlocksOnStart();

            StartupService.ClearStaleMempool();
            StartupService.SetValidator();

            //StartupService.RunStateSync();
            StartupService.RunRules(); //rules for cleaning up wallet data.
            StartupService.ClearValidatorDups();

            StartupService.BootstrapBeacons();
            await StartupService.EstablishBeaconReference();

            //Removes validator record from DB_Peers as its now within the wallet.
            StartupService.ClearOldValidatorDups();

            //blockTimer = new Timer(blockBuilder_Elapsed); // 1 sec = 1000, 60 sec = 60000
            //blockTimer.Change(60000, 10000); //waits 1 minute, then runs every 10 seconds for new blocks

            Globals.heightTimer = new Timer(blockHeightCheck_Elapsed); // 1 sec = 1000, 60 sec = 60000
            Globals.heightTimer.Change(0, 18000); //waits 1 minute, then runs every 18 seconds for new blocks

            //Globals.DBCommitTimer = new Timer(dbCommitCheckTimer_Elapsed); // 1 sec = 1000, 60 sec = 60000
            //Globals.DBCommitTimer.Change(90000, 3 * 10 * 6000); //waits 1.5 minute, then runs every 3 minutes

            Globals.ConnectionHistoryTimer = new Timer(connectionHistoryTimer_Elapsed); // 1 sec = 1000, 60 sec = 60000
            Globals.ConnectionHistoryTimer.Change(90000, 3 * 10 * 6000); //waits 1.5 minute, then runs every 3 minutes

            string url = Globals.TestURL == false ? "http://*:" + Globals.APIPort : "https://*:7777"; //local API to connect to wallet. This can be changed, but be cautious. 
            string url2 = "http://*:" + Globals.Port; //this is port for signalr connect and all p2p functions
                                                      //string url2 = "https://*:3338" //This is non http version. Must uncomment out app.UseHttpsRedirection() in startupp2p

            var commandLoopTask = Task.Run(() => CommandLoop(url));
            var commandLoopTask2 = Task.Run(() => CommandLoop2(url2));
            var commandLoopTask3 = Task.Run(() => CommandLoop3());

            //for web API using Kestrel
            var builder = Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseKestrel()
                    .UseStartup<Startup>()
                    .UseUrls(url)
                    .ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
                });

            //for p2p using signalr
            var builder2 = Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseKestrel()
                    .UseStartup<StartupP2P>()
                    .UseUrls(url2)
                    .ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
                    webBuilder.ConfigureKestrel(options => {


                    });
                });

            _ = builder.RunConsoleAsync();
            _ = builder2.RunConsoleAsync();

            LogUtility.Log("Wallet Starting...", "Program:Before CheckLastBlock()");

            StartupService.CheckLastBlock();

            StartupService.CheckForDuplicateBlocks();

            if (Globals.DatabaseCorruptionDetected == true)
            {
                while (true)
                {
                    Console.WriteLine("Please correct database corruption before continuing.");
                    var read = Console.ReadLine();
                }
            }

            StartupService.StartupMemBlocks();

            await TransactionData.UpdateWalletTXTask();

            _ = StartupService.ConnectToAdjudicators();
            _ = StartupService.ConnectoToBeacon();

            if (!string.IsNullOrWhiteSpace(Globals.ConfigValidator))
            {
                StartupService.SetConfigValidator();
            }
            Thread.Sleep(2000);

            var tasks = new Task[] {
                commandLoopTask, //CLI console
                commandLoopTask2, //awaiting parameters
                commandLoopTask3//Beacon client/server
            };

            await Task.WhenAll(tasks);

            LogUtility.Log("Line Reached. Should not be reached Program.cs Line 251", "Program:Before Task.WaitAll(commandLoopTask, commandLoopTask2)");

            //await Task.WhenAny(builder2.RunConsoleAsync(), commandLoopTask2);
            //await Task.WhenAny(builder.RunConsoleAsync(), commandLoopTask);
        }

        #endregion

        #region Command Loops
        private static void CommandLoop(string url)
        {
            StartupService.StartupMenu();
            Thread.Sleep(1000);
            StartupService.MainMenu();

            while (true)
            {
                var command = Console.ReadLine();
                if (command == "/help" ||
                    command == "/menu" ||
                    command == "/info" ||
                    command == "/stopco" ||
                    command == "/unlock" ||
                    command == "/addpeer" ||
                    command == "/val" ||
                    command == "/mempool" ||
                    command == "/debug" ||
                    command == "1" ||
                    command == "5" ||
                    command == "6" ||
                    command == "7" ||
                    command == "/exit" ||
                    command == "/clear" ||
                    command == "/trillium")
                {
                    RunCommand(command);
                }
                else if (!string.IsNullOrWhiteSpace(Globals.WalletPassword))
                {
                    var now = DateTime.UtcNow;
                    if (Globals.AlwaysRequireWalletPassword == true)
                    {
                        Console.WriteLine("Please enter your wallet password");
                        var walletPass = Console.ReadLine();
                        var passCheck = Globals.WalletPassword.ToDecrypt(walletPass);
                        if (passCheck == walletPass && passCheck != "Fail")
                        {
                            //CLIWalletUnlockTime = DateTime.UtcNow.AddMinutes(WalletUnlockTime);
                            RunCommand(command);
                        }
                        else
                        {
                            Console.WriteLine("Incorrect password was entered.");
                        }

                    }
                    else if (now > Globals.CLIWalletUnlockTime && Globals.AlwaysRequireWalletPassword == false)
                    {
                        Console.WriteLine("Please enter your wallet password");
                        var walletPass = Console.ReadLine();
                        var passCheck = Globals.WalletPassword.ToDecrypt(walletPass);
                        if (passCheck == walletPass && passCheck != "Fail")
                        {
                            Globals.CLIWalletUnlockTime = DateTime.UtcNow.AddMinutes(Globals.WalletUnlockTime);
                            RunCommand(command);
                        }
                        else
                        {
                            Console.WriteLine("Incorrect password was entered.");
                        }
                    }
                    else
                    {
                        RunCommand(command);
                    }
                }
                else
                {
                    RunCommand(command);
                }

            }

        }

        private static async void RunCommand(string? command)
        {
            if (!string.IsNullOrWhiteSpace(command))
            {
                string commandResult = "";
                if (command.Contains(","))
                {
                    var splitCommand = command.Split(',');
                    commandResult = await BaseCommand.ProcessCommand(splitCommand[0], splitCommand[1]);
                }
                else
                {
                    commandResult = await BaseCommand.ProcessCommand(command);
                }


                if (commandResult == "_EXIT")
                {
                    Globals.StopAllTimers = true;
                    Console.WriteLine("Closing and Exiting Wallet Application.");
                    Thread.Sleep(2000);
                    Environment.Exit(0);
                }

                Console.WriteLine(commandResult);
            }
            else
            {
                Console.WriteLine("Please enter a command...");
            }
        }
        private static void CommandLoop2(string url)
        {
            //Console.ReadKey();
        }

        private static void CommandLoop3()
        {
            StartupService.StartBeacon();
        }

        #endregion

        #region Block Height Check
        private static async void blockHeightCheck_Elapsed(object sender)
        {
            if (Globals.HeightCheckLock == false)
            {
                Globals.HeightCheckLock = true;
                while (!Globals.Nodes.Any())                                   
                    await Task.Delay(4);                

                await P2PClient.UpdateNodeHeights();

                var maxHeight = Globals.Nodes.Values.Select(x => x.NodeHeight).OrderByDescending(x => x).FirstOrDefault();
                if (maxHeight > Globals.LastBlock.Height)
                {
                    P2PClient.UpdateMaxHeight(maxHeight);
                    await BlockDownloadService.GetAllBlocks();
                }
                else
                    P2PClient.UpdateMaxHeight(maxHeight);

                var MaxHeight = P2PClient.MaxHeight();
                foreach(var node in Globals.Nodes.Values)
                {
                    if(node.NodeHeight < MaxHeight - 3)                    
                        await P2PClient.RemoveNode(node);                    
                }

                Globals.HeightCheckLock = false;
            }
            try
            {
                DebugUtility.WriteToDebugFile();
            }
            catch (Exception ex)
            {

            }

        }

        #endregion

        #region DB Commits
        private static async void dbCommitCheckTimer_Elapsed(object sender)
        {
            if (Globals.StopAllTimers == false)
            {
                await DbContext.CheckPoint();
            }

        }

        #endregion

        #region Connection History Timer
        private static async void connectionHistoryTimer_Elapsed(object sender)
        {
            try
            {
                if (Globals.AdjudicateAccount != null)
                {
                    var connectionQueueList = Globals.ConnectionHistoryDict.Values.ToList();

                    foreach (var cq in connectionQueueList)
                    {
                        new ConnectionHistory().Process(cq.IPAddress, cq.Address, cq.ConnectionTime, cq.WasSuccess);
                        Globals.ConnectionHistoryDict.TryRemove(cq.Address, out _);
                    }

                    var conList = await ConnectionHistory.Read();

                    ConnectionHistory.WriteToConHistFile(conList);
                }
            }
            catch { }

        }

        #endregion
    }


}