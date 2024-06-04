global using ReserveBlockCore.Extensions;

using ReserveBlockCore.Commands;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using Spectre.Console.Rendering;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using Spectre.Console;
using System.Security.AccessControl;
using System.Net.Sockets;
using System.Net.Http;
using System.Security.Principal;
using System.Runtime.Intrinsics.Arm;
using System.Reflection;
using ReserveBlockCore.DST;
using ReserveBlockCore.Engines;
using ReserveBlockCore.Config;
using ReserveBlockCore.Bitcoin.Utilities;
using ReserveBlockCore.Bitcoin.Integrations;

namespace ReserveBlockCore
{
    class Program
    {
        #region Main
        static async Task Main(string[] args)
        {
            bool keslog = false;
            bool signalrLog = false;
            bool runSingleRequest = false;
            bool skipStateSync = false;
            bool startGUI = false;

            var argList = args.ToList();
            //force culture info to US
            var culture = CultureInfo.GetCultureInfo("en-US");
            if (Thread.CurrentThread.CurrentCulture.Name != "en-US")
            {
                CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;

                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;
            }

            var httpClientBuilder = Host.CreateDefaultBuilder(args)
                     .ConfigureServices(services =>
                     {
                         services.AddHttpClient();
                         services.AddTransient<HttpService>();
                     })
                     .Build();

            await httpClientBuilder.StartAsync();
            Globals.HttpClientFactory = httpClientBuilder.Services.GetRequiredService<HttpService>().HttpClientFactory();

            //Forced Testnet
            Globals.IsTestNet = true;
            Globals.V4Height = Globals.IsTestNet ? 1 : 9999999;//change for mainnet.
            Globals.GenesisValidator = Globals.IsTestNet ? "xMpa8DxDLdC9SQPcAFBc2vqwyPsoFtrWyC" : "SomeMainnetAddress";


            //Perform network time sync
            _ = NetworkTimeService.Run();
            _ = VersionControlService.RunVersionControl();

            await Task.Delay(800);

            bool valEncryptCheck = false;
            string? valEncryptAddr = "";

            var isRestarted = Environment.GetEnvironmentVariable("RBX-Restart", EnvironmentVariableTarget.User);
            var isUpdated = Environment.GetEnvironmentVariable("RBX-Updated", EnvironmentVariableTarget.User);
            if (isRestarted == "1")
            {
                Console.WriteLine("Restarted Detected!");
                Environment.SetEnvironmentVariable("RBX-Restart", null, EnvironmentVariableTarget.User);
                bool exit = false;
                while(!exit)
                {
                    using (TcpClient tcpClient = new TcpClient())
                    {
                        try
                        {
                            var port = Globals.Port;
                            tcpClient.Connect("127.0.0.1", port);
                            //LogUtility.Log($"CLI Already Running on port {port}. Closing new instance.", "StartupService.AnotherInstanceCheck()");
                            //Environment.Exit(0);
                        }
                        catch (Exception)
                        {
                            exit = true;
                            Console.WriteLine("Application Starting...");
                        }
                    }
                    await Task.Delay(400);
                }
            }
            if (isUpdated == "1")
            {
                Console.WriteLine("Update Detected!");
                await VersionControlService.DeleteOldFiles();
                Environment.SetEnvironmentVariable("RBX-Updated", null, EnvironmentVariableTarget.User);
            }

            //to enable again right click the cmd -> Properties -> check Quick Edit 
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                WindowsUtilities.DisableConsoleQuickEdit.Go();


            Globals.BuildVer = WalletVersionUtility.GetBuildVersion();

            _ = NFTAssetFileUtility.GeneratevBTCDefaultLogo();

            Globals.CLIVersion = $"{Globals.MajorVer}.{Globals.MinorVer}.{Globals.RevisionVer}.{WalletVersionUtility.GetBuildVersion()}-beta";

            var logCLIVer = Globals.CLIVersion;

            if (argList.Count() > 0)
            {
                Globals.StartArguments = args.ToStringFromArray();//store for later in case of update restart.

                argList.ForEach(async x =>
                {
                    var argC = x.ToLower();
                    if(argC.Contains("cFork"))
                    {
                        Globals.IsFork = true;
                        var forkSplit = argC.Split(new char[] { '=' });
                        if (string.IsNullOrEmpty(forkSplit[1]))
                            await ForkConfiguration.RunForkedConfiguration();
                        else
                            await ForkConfiguration.RunForkedConfiguration(forkSplit[1]);
                    }
                    if(argC == "version")
                    {
                        Console.WriteLine(Globals.CLIVersion);
                        runSingleRequest = true;
                    }
                    if (argC == "testnet")
                    {
                        //Launch testnet
                        Globals.IsTestNet = true;
                    }
                    if (argC == "startgui")
                    {
                        startGUI = true;
                    }
                    if (argC == "stunmessages")
                    {
                        Globals.ShowSTUNMessagesInConsole = true;
                    }
                    if (argC == "stunport")
                    {
                        var stunPortSplit = argC.Split(new char[] { '=' });
                        var convRes = int.TryParse(stunPortSplit[1], out var result);
                        var stunPort = convRes ? result : Globals.IsTestNet ? 13340 : 3340;
                        Globals.SelfSTUNPort = stunPort;
                    }
                    if (argC.Contains("stunservers"))
                    {
                        var stunSplit = argC.Split(new char[] { '=' });
                        var stunServers = stunSplit[1].Split(',');
                        foreach (var server in stunServers)
                        {
                            Globals.STUNServers.Add(new StunServer { ServerIPPort = server, Group = 999, IsNetwork = false });
                        }
                    }
                    if (argC == "gui")
                    {
                        Globals.GUI = true;
                        Explorers.PopulateExplorerDictionary();
                        _ = NodeFinder.GetNode();
                    }
                    if(argC == "blockv2")
                    {
                        Globals.UseV2BlockDownload = true;
                    }
                    if (argC == "unsafe")
                    {
                        Globals.RunUnsafeCode = true;
                    }
                    if (argC == "skip")
                    {
                        skipStateSync = true;
                    }
                    if (argC.Contains("encpass"))
                    {
                        var encPassSplit = argC.Split(new char[] { '=' });
                        var encPassword = encPassSplit[1];
                        Globals.EncryptPassword = encPassword.ToSecureString();
                    }
                    if(argC.Contains("openapi"))
                    {
                        Globals.OpenAPI = true;
                    }
                    if (argC.Contains("basic"))
                    {
                        Globals.BasicCLI = true;//give previous session time to close.
                    }
                    if (argC.Contains("updating"))
                    {
                        await Task.Delay(5000);//give previous session time to close.
                    }
                    if(argC.Contains("apitoken"))
                    {
                        var apiTokens = argC.Split(new char[] { '=' });
                        var apiToken = apiTokens[1];
                        Globals.APIToken = apiToken.ToSecureString();
                    }
                    if(argC.Contains("blockseedcalls"))
                    {
                        Globals.BlockSeedCalls = true;
                    }
                    if(argC.Contains("keslog"))
                    {
                        keslog = true;
                    }
                    if (argC.Contains("signalrlog"))
                    {
                        signalrLog = true;
                    }
                    if (argC.Contains("snapshot"))
                    {
                        var snapshot = argC.Split(new char[] { '=' });
                        var response = snapshot[1];
                        if(response == "0")
                        {
                            //download auto
                            await SnapshotService.RunSnapshot();
                        }
                        if(response == "1")
                        {
                            //prompt cli commands here
                            AnsiConsole.MarkupLine($"You have added the snapshot param. Do you want to download snapshot? ([green]'y'[/] for [green]yes[/] and [red]'n'[/] for [red]no[/])");
                            AnsiConsole.MarkupLine($"[yellow]Please note this will completely wipe out your database folder. Please make sure you have your private keys backed up.[/])");
                            var snapshotResponse = Console.ReadLine();
                            if(!string.IsNullOrEmpty(snapshotResponse))
                            {
                                if(snapshotResponse == "y")
                                {
                                    await SnapshotService.RunSnapshot();
                                }
                                else
                                {
                                    Console.WriteLine("Snapshot Cancelled.");
                                }
                            }
                        }
                    }
                });
            }

            if(runSingleRequest)
            {
                Console.WriteLine("Press 'Enter' to exit...");
                var exit = Console.ReadLine();
                Environment.Exit(0);
            }

            Globals.Platform = PlatformUtility.GetPlatform();
            Globals.SystemMemory = MemoryService.GetTotalMemory();

            Config.Config.EstablishConfigFile();
            var config = Config.Config.ReadConfigFile();
            Config.Config.ProcessConfig(config);

            LogUtility.Log(logCLIVer, "Main", true);
            LogUtility.Log($"RBX Wallet - {logCLIVer}", "Main");

            SCLogUtility.Log(logCLIVer, "Main", true);

            SCLogUtility.Log($"RBX NFT ver. - {logCLIVer}", "Main");

            APILogUtility.Log(logCLIVer, "Main", true);

            APILogUtility.Log($"RBX API ver. - {logCLIVer}", "Main");

            StartupService.AnotherInstanceCheck(); //checks for another instance

            StartupService.StartupDatabase();// initializes databases

            await DbContext.CheckPoint(); //checkpoints db log files

            StartupService.SetBlockHeight(); //sets current block height
            StartupService.SetLastBlock(); //puts last known block into memory
            StartupService.StartupMemBlocks(); //puts 400 blocks into memory (height, hash)
            StartupService.StartupBlockHashes();
            StartupService.PopulateTokenDictionary();

            StartupService.SetBlockchainChainRef(); // sets blockchain reference id
            StartupService.CheckBlockRefVerToDb(); //checks check ID
            StartupService.HDWalletCheck();// checks for HD wallet
            StartupService.EncryptedWalletCheck(); //checks if wallet is encrypted
            SeedNodeService.SeedNodes(); //adds nodes to initial find blocks
            SeedNodeService.SeedBench(); //seeds adj bench
            await StartupService.GetArbiters();
            await BadTransaction.PopulateBadTXList(); //adds bad txs to ignore
            await WalletService.BalanceRectify(); //checks balance local against state

            Globals.V3Height = Globals.IsTestNet == true ? 0 : (int)Globals.V3Height;

            Globals.BlockLock = Globals.IsTestNet ? 0 : Globals.BlockLock;

            //uncomment to create genesis block
            //await BlockchainData.InitializeChain();

            StartupService.SetValidator();
            StartupService.ArbiterCheck();

            //To update this go to project -> right click properties -> go To debug -> general -> open debug launch profiles
            if (args.Length != 0)
            {
                argList.ForEach(async x =>
                {
                    var argC = x.ToLower();
                    if (argC == "enableapi")
                    {
                        Startup.APIEnabled = true; //api disabled by default
                    }
                    if (argC == "logmemory")
                    {
                        Globals.LogMemory = true;
                    }
                    if (argC == "stun")
                    {
                        Globals.SelfSTUNServer = true;
                    }
                    if (argC == "testurl")
                    {
                        //Launch testnet
                        Globals.TestURL = true;
                    }
                    if (argC == "gui")
                    {
                        _ = Bitcoin.Bitcoin.AccountCheck();
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

                                if (argList.Exists(x => x.ToLower() == "start-validator"))
                                {
                                    var name = argList.Where(x => x.ToLower().Contains("validator-name=")).FirstOrDefault();
                                    if(name != null)
                                    {
                                        if(string.IsNullOrEmpty(Globals.ValidatorAddress))
                                        {
                                            var nameSplit = name.Split(new char[] { '=' });
                                            var validatorName = nameSplit[1];
                                            var validatorAddress = account.Address;
                                            var accountDB = AccountData.GetAccounts();
                                            var accountRec = accountDB.Query().Where(x => x.Address == account.Address).FirstOrDefault();

                                            var valResult = await ValidatorService.StartValidating(accountRec, validatorName, true);
                                            Console.WriteLine(valResult);

                                            await Task.Delay(1000);
                                        }
                                        else
                                        {
                                            Console.WriteLine($"Validator Active: {Globals.ValidatorAddress}");
                                        }
                                        
                                    }
                                    else
                                    {
                                        Console.WriteLine("Error: 'start-validator' was present, but no name was entered");
                                    }
                                    
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            //bad key
                        }
                    }
                });
            }

            if (!string.IsNullOrWhiteSpace(Globals.ConfigValidator))
            {
                StartupService.SetConfigValidator();
            }

            //deprecate in v5.0.1 or greater
            StartupService.SetAdjudicatorAddresses();
            //deprecate in v5.0.1 or greater
            Signer.UpdateSigningAddresses();

            if (Globals.IsWalletEncrypted && !string.IsNullOrEmpty(Globals.ValidatorAddress) && !Globals.GUI)
            {
                StartupService.EncryptedPasswordEntry();
            }

            if (Globals.IsWalletEncrypted && Globals.AdjudicateAccount != null && !Globals.GUI)
            {
                StartupService.EncryptedPasswordEntryAdj();
            }

            if (Globals.IsWalletEncrypted && Globals.AdjudicateAccount != null && Globals.GUI)
            {
                Globals.GUIPasswordNeeded = true;
            }

            if (Globals.IsWalletEncrypted && !string.IsNullOrEmpty(Globals.ValidatorAddress) && Globals.GUI)
            {
                Globals.GUIPasswordNeeded = true;
                valEncryptAddr = await ValidatorService.SuspendMasterNode();//investigate this and ensure startup happens after suspend
                valEncryptCheck = true;
            }

            await StartupService.RunSettingChecks(skipStateSync);

            //This is for consensus start.

            //deprecate in v5.0.1 or greater
            //await StartupService.GetAdjudicatorPool();
            StartupService.DisplayValidatorAddress();
            StartupService.CheckForDuplicateBlocks();


            await StartupService.SetSelfBeacon();

            _ = Task.Run(LogUtility.LogLoop);

            //deprecate in v5.0.1 or greater
            _ = Task.Run(P2PClient.UpdateMethodCodes);

            _ = Task.Run(StartupService.StartupPeers);
            
            await StartupService.ClearStaleMempool();

            StartupService.RunRules(); //rules for cleaning up wallet data.
            StartupService.ClearValidatorDups();

            StartupService.LoadBeacons();
            await StartupService.EstablishBeaconReference();

            //Removes validator record from DB_Peers as its now within the wallet.
            StartupService.ClearOldValidatorDups();

            Globals.ConnectionHistoryTimer = new Timer(connectionHistoryTimer_Elapsed); // 1 sec = 1000, 60 sec = 60000
            Globals.ConnectionHistoryTimer.Change(90000, 3 * 10 * 6000); //waits 1.5 minute, then runs every 3 minutes

            //API Port URL
            string url = !Globals.TestURL ? "http://*:" + Globals.APIPort : "https://*:" + Globals.APIPortSSL;
            //P2P Port URL
            string url2 = "http://*:" + Globals.Port;
            //Consensus Port URL

            var commandLoopTask = Task.Run(() => CommandLoop(url));
            var commandLoopTask2 = Task.Run(() => CommandLoop2(url2));
            var commandLoopTask3 = Task.Run(() => CommandLoop3());

            while (Globals.StopAllTimers || Globals.BlocksDownloadSlim.CurrentCount == 0)
                await Task.Delay(20);

            //for web API using Kestrel
            var builder = Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseKestrel(options =>
                    {
                        if(Globals.OpenAPI)
                        {
                            options.ListenAnyIP(Globals.APIPort + 1, listenOption => { listenOption.UseHttps(GetSelfSignedCertificate()); });
                            options.ListenAnyIP(Globals.APIPort);
                        }
                        else
                        {
                            options.ListenLocalhost(Globals.APIPort + 1, listenOption => { listenOption.UseHttps(GetSelfSignedCertificate()); });
                            options.ListenLocalhost(Globals.APIPort);
                        }
                        
                    })
                    .UseStartup<Startup>()
                    //.UseUrls(new string[] {$"http://*:{Globals.APIPort}", $"https://*:{Globals.APIPort}" })
                    .ConfigureLogging(!keslog ? loggingBuilder => loggingBuilder.ClearProviders() : loggingBuilder => loggingBuilder.AddSimpleConsole());
                });

            //for p2p using signalr
            var builder2 = Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseKestrel()
                    .UseStartup<StartupP2P>()
                    .UseUrls(url2)
                    .ConfigureLogging(!signalrLog ? loggingBuilder => loggingBuilder.ClearProviders() : loggingBuilder => loggingBuilder.AddSimpleConsole());
                    webBuilder.ConfigureKestrel(options =>
                    {


                    });
                });


            _ = builder.RunConsoleAsync();
            _ = builder2.RunConsoleAsync();

            StartupService.SetLastBlockchainPoint();

            if (!string.IsNullOrEmpty(Globals.ValidatorAddress))
            {
                _ = ValidatorService.StartValidatorServer();
                _ = ValidatorService.StartupValidators();
                _ = Task.Run(ValidatorService.BlockHeightCheckLoop);
            }
            
            Globals.StopAllTimers = true;
            _ = Task.Run(BlockHeightCheckLoop);
            _ = StartupService.DownloadBlocksOnStart();

            //TODO need new validator running method
            //_ = Task.Run(ClientCallService.DoWorkV3);
            
            LogUtility.Log("Wallet Starting...", "Program:Before CheckLastBlock()");

            if (Globals.DatabaseCorruptionDetected == true)
            {
                while (true)
                {
                    Console.WriteLine("Please correct database corruption before continuing.");
                    var read = Console.ReadLine();
                }
            }

            if (Globals.ConnectToMother)
            {
                //connect to mother
                _ = StartupService.StartupMother();
            }

            if (valEncryptCheck && valEncryptAddr != null)
            {
                while (Globals.EncryptPassword.Length == 0)
                {
                    await Task.Delay(1000);
                }
                var accounts = AccountData.GetAccounts();
                var myAccount = accounts.FindOne(x => x.IsValidating == false && x.Address == valEncryptAddr);
                if (myAccount != null)
                {
                    myAccount.IsValidating = true;
                    accounts.UpdateSafe(myAccount);
                    Globals.ValidatorAddress = myAccount.Address;
                    Globals.GUIPasswordNeeded = false;
                    LogUtility.Log("Validator Address set: " + Globals.ValidatorAddress, "StartupService:StartupPeers()");
                }
            }

            await TransactionData.UpdateWalletTXTask();

            ////deprecate in v5.0.1 or greater
            //_ = StartupService.ConnectToAdjudicators();//MODIFY - Connect to other VALS

            _ = BanService.PeerBanUnbanService();
            _ = BeaconService.BeaconRunService();
            _ = SeedNodeService.Start();
            _ = SeedNodeService.CallToSeed();
            _ = FortisPoolService.PopulateFortisPoolCache();
            _ = MempoolBroadcastService.RunBroadcastService();

            _ = ValidatorService.ValidatingMonitorService();

            _ = ValidatorService.GetActiveValidators();
            _ = ValidatorService.ValidatorCountRun();
            _ = DSTClient.Run();

            if(startGUI)
            {
                Process[] pname = Process.GetProcessesByName("RBXWallet");

                if(pname.Length == 0)
                {
                    Globals.GUIProcess = new Process();
                    Globals.GUIProcess.StartInfo = new ProcessStartInfo
                    {
                        FileName = @"C:\Program Files (x86)\RBXWallet\RBXWallet.exe",
                        Verb = "runas",
                        WorkingDirectory = @"C:\Program Files (x86)\RBXWallet\"
                    };
                    Globals.GUIProcess.Start();
                }
            }

            var decShop = DecShop.GetMyDecShopInfo();
            if(decShop != null)
            {
                if(decShop.STUNServerGroup == 0)
                {
                    if(Globals.IsTestNet)
                    {
                        decShop.STUNServerGroup = 1;
                        await DecShop.SaveMyDecShopLocal(decShop);
                    }
                }
                if (!decShop.IsOffline)
                {
                    _ = AuctionEngine.StartBidProcessing();
                }
                _ = AuctionEngine.StartAuctioneer();
            }

            if (Globals.SelfSTUNServer)
                _ = Task.Run(() => { StartupService.StartDSTServer(); });//launching off the main thread.

            //deprecate in v5.0.1 or greater
            //if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            //    _ = WindowsUtilities.AdjAutoRestart();

            //if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            //    _ = LinuxUtilities.AdjAutoRestart();

            await Task.Delay(1000);

            var tasks = new Task[] {
                commandLoopTask, //CLI console
                commandLoopTask2, //awaiting parameters
                commandLoopTask3//Beacon client/server
            };

            try
            {
                Process proc = Process.GetCurrentProcess();
                var workingSetMem = proc.WorkingSet64;
                Globals.StartMemory = Math.Round((decimal)workingSetMem / 1024 / 1024, 2);
                Globals.CurrentMemory = Math.Round((decimal)workingSetMem / 1024 / 1024, 2);
            }
            catch { }

            _ = MemoryService.Run();
            _ = MemoryService.RunGlobals();

            _ = ArbiterService.GetArbiterSigningAddress();

            await Task.WhenAll(tasks);

            LogUtility.Log("Line Reached. Should not be reached Program.cs", "Program:Before Task.WaitAll(commandLoopTask, commandLoopTask2)");

        }

        #endregion

        #region Command Loops
        private static void CommandLoop(string url)
        {
            StartupService.StartupMenu();
            if (Globals.AdjudicateAccount == null)
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
                    while (Globals.TreisUpdating)
                    {
                        await Task.Delay(100);
                        //waiting for treis to stop
                    }

                    await Settings.InitiateShutdownUpdate();
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
            _ = StartupService.StartBeacon();
            _ = StartupService.StartArbiter();
        }

        #endregion

        #region Block Height Check
        private static async Task BlockHeightCheckLoop()
        {
            bool dupMessageShown = false;

            while (true)
            {
                try
                {
                    while (!Globals.Nodes.Any())
                        await Task.Delay(20);

                    await P2PClient.UpdateNodeHeights();

                    var maxHeight = Globals.Nodes.Values.Select(x => x.NodeHeight).OrderByDescending(x => x).FirstOrDefault();
                    if (maxHeight > Globals.LastBlock.Height)
                    {
                        P2PClient.UpdateMaxHeight(maxHeight);
                        _ = BlockDownloadService.GetAllBlocks();
                    }
                    else
                        P2PClient.UpdateMaxHeight(maxHeight);

                    var MaxHeight = P2PClient.MaxHeight();
                    foreach (var node in Globals.Nodes.Values)
                    {
                        if (node.NodeHeight < MaxHeight - 3)
                            await P2PClient.RemoveNode(node);
                    }

                    DebugUtility.WriteToDebugFile("debug.txt", await StaticVariableUtility.GetStaticVars());
                    if (Globals.DuplicateAdjAddr)
                    {
                        if (!dupMessageShown)
                            StartupService.MainMenu();
                        dupMessageShown = true;
                    }

                    if (Globals.DuplicateAdjIP)
                    {
                        if (!dupMessageShown)
                            StartupService.MainMenu();
                        dupMessageShown = true;
                    }

                    if (!Globals.DuplicateAdjIP && !Globals.DuplicateAdjAddr)
                        dupMessageShown = false;

                    var Now = TimeUtil.GetTime();
                    foreach (var sig in Globals.Signatures.Where(x => Now - x.Value > 300))
                        Globals.Signatures.TryRemove(sig.Key, out _);
                }
                catch { }

                if (Globals.AdjudicateAccount != null)
                    await Task.Delay(1000);
                else
                    await Task.Delay(10000);
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
                        Globals.ConnectionHistoryDict.TryRemove(cq.IPAddress, out _);
                    }

                    var conList = await ConnectionHistory.Read();

                    ConnectionHistory.WriteToConHistFile(conList);
                }
            }
            catch { }

        }

        #endregion

        #region Self Signed Cert
        private static X509Certificate2 GetSelfSignedCertificate()
        {
            var password = Guid.NewGuid().ToString();
            var commonName = "RBXSelfSignedCertAPI";
            var rsaKeySize = 2048;
            var years = 100;
            var hashAlgorithm = HashAlgorithmName.SHA256;

            using (var rsa = RSA.Create(rsaKeySize))
            {
                var request = new CertificateRequest($"cn={commonName}", rsa, hashAlgorithm, RSASignaturePadding.Pkcs1);

                request.CertificateExtensions.Add(
                  new X509KeyUsageExtension(X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, false)
                );
                request.CertificateExtensions.Add(
                  new X509EnhancedKeyUsageExtension(
                    new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false)
                );

                var certificate = request.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(years));
                if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    certificate.FriendlyName = commonName;

                // Return the PFX exported version that contains the key
                return new X509Certificate2(certificate.Export(X509ContentType.Pfx, password), password, X509KeyStorageFlags.MachineKeySet);
            }
        }

        #endregion
    }
}
