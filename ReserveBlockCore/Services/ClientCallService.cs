using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using ReserveBlockCore.Beacon;
using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Utilities;
using ReserveBlockCore.Extensions;
using System;
using System.Collections.Concurrent;
using System.Data.Common;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Numerics;
using System.Security;
using System.Transactions;
using System.Xml.Linq;
using static ReserveBlockCore.P2P.ConsensusClient;

namespace ReserveBlockCore.Services
{
    public class ClientCallService : IHostedService, IDisposable
    {
        #region Timers and Private Variables
        public static IHubContext<P2PAdjServer> HubContext;
        private readonly IHubContext<P2PAdjServer> _hubContext;
        private readonly IHostApplicationLifetime _appLifetime;
        private int executionCount = 0;
        private Timer _timer = null!;
        private Timer _fortisPoolTimer = null!;
        private Timer _checkpointTimer = null!;
        private Timer _blockStateSyncTimer = null!;
        private Timer _encryptedPasswordTimer = null!;
        private Timer _assetTimer = null!;
        private static bool FirstRun = false;
        private static bool StateSyncLock = false;
        private static bool AssetLock = false;

        public ClientCallService(IHubContext<P2PAdjServer> hubContext, IHostApplicationLifetime appLifetime)
        {
            _hubContext = hubContext;
            HubContext = hubContext;
            _appLifetime = appLifetime;
        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            _timer = new Timer(DoWork, null, TimeSpan.FromSeconds(40),
                TimeSpan.FromSeconds(2));

            _fortisPoolTimer = new Timer(DoFortisPoolWork, null, TimeSpan.FromSeconds(90),
                TimeSpan.FromSeconds(Globals.IsTestNet ? 30 : 180));

            if (Globals.ChainCheckPoint == true)
            {
                var interval = Globals.ChainCheckPointInterval;
                
                _checkpointTimer = new Timer(DoCheckpointWork, null, TimeSpan.FromSeconds(240),
                TimeSpan.FromHours(interval));
            }

            _encryptedPasswordTimer = new Timer(DoPasswordClearWork, null, TimeSpan.FromSeconds(5),
                TimeSpan.FromMinutes(Globals.PasswordClearTime));

            _assetTimer = new Timer(DoAssetWork, null, TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(5));

            return Task.CompletedTask;
        }

        #endregion

        #region Checkpoint Work
        private async void DoCheckpointWork(object? state)
        {
            var retain = Globals.ChainCheckPointRetain;
            var path = GetPathUtility.GetDatabasePath();
            var checkpointPath = Globals.ChainCheckpointLocation;
            var zipPath = checkpointPath + "checkpoint_" + DateTime.Now.Ticks.ToString();

            try
            {
                var directoryCount = Directory.GetFiles(checkpointPath).Length;
                if(directoryCount >= retain)
                {
                    FileSystemInfo fileInfo = new DirectoryInfo(checkpointPath).GetFileSystemInfos()
                        .OrderBy(fi => fi.CreationTime).First();
                    fileInfo.Delete();
                }

                ZipFile.CreateFromDirectory(path, zipPath);
                var createDate = DateTime.Now.ToString();
                LogUtility.Log($"Checkpoint successfully created at: {createDate}", "ClientCallService.DoCheckpointWork()");
            }
            catch(Exception ex)
            {
                ErrorLogUtility.LogError($"Error creating checkpoint. Error Message: {ex.ToString()}", "ClientCallService.DoCheckpointWork()");
            }
        }

        #endregion

        #region Password Clear Work

        private async void DoPasswordClearWork(object? state)
        {
            if(Globals.IsWalletEncrypted == true)
            {
                if(string.IsNullOrEmpty(Globals.ValidatorAddress) && Globals.AdjudicateAccount == null)
                {
                    Globals.EncryptPassword.Dispose();
                    Globals.EncryptPassword = new SecureString();
                }
                //Password must remain in order to continue validating. It is not recommend to have your validator be your main source of funds wallet.
                //Recommend transferring funds out to a secure offline wallet. 
            }
        }
        #endregion

        #region Asset Download/Upload Work

        private async void DoAssetWork(object? state)
        {
            if (!AssetLock && Globals.AutoDownloadNFTAsset)
            {
                AssetLock = true;
                {
                    var currentDate = DateTime.UtcNow;
                    var aqDB = AssetQueue.GetAssetQueue();
                    if(aqDB != null)
                    {
                        var aqList = aqDB.Find(x => x.NextAttempt != null && x.NextAttempt <= currentDate && x.IsComplete != true && 
                            x.AssetTransferType == AssetQueue.TransferType.Download).ToList();

                        if(aqList.Count() > 0)
                        {
                            foreach(var aq in aqList)
                            {
                                aq.Attempts = aq.Attempts < 4 ? aq.Attempts + 1 : aq.Attempts;
                                var nextAttemptValue = AssetQueue.GetNextAttemptInterval(aq.Attempts);
                                aq.NextAttempt = DateTime.UtcNow.AddSeconds(nextAttemptValue);
                                try
                                {
                                    var result = await NFTAssetFileUtility.DownloadAssetFromBeacon(aq.SmartContractUID, aq.Locator, "NA", aq.MD5List);
                                    if(result == "Success")
                                    {
                                        NFTLogUtility.Log($"Download Request has been sent", "ClientCallService.DoAssetWork()");
                                        aq.IsComplete = true;
                                        aq.Attempts = 0;
                                        aq.NextAttempt = DateTime.UtcNow;
                                        aqDB.UpdateSafe(aq);
                                    }
                                    else
                                    {
                                        NFTLogUtility.Log($"Download Request has not been sent. Reason: {result}", "ClientCallService.DoAssetWork()");
                                        aqDB.UpdateSafe(aq);
                                    }
                                    
                                }
                                catch(Exception ex)
                                {
                                    NFTLogUtility.Log($"Error Performing Asset Download. Error: {ex.ToString()}", "ClientCallService.DoAssetWork()");
                                }
                            }
                        }

                        var aqCompleteList = aqDB.Find(x =>  x.IsComplete == true && x.IsDownloaded == false &&
                            x.AssetTransferType == AssetQueue.TransferType.Download).ToList();

                        if(aqCompleteList.Count() > 0)
                        {
                            foreach(var aq in aqCompleteList)
                            {
                                try
                                {
                                    var curDate = DateTime.UtcNow;
                                    if(aq.NextAttempt <= curDate)
                                    {
                                        await NFTAssetFileUtility.CheckForAssets(aq);
                                        aq.Attempts = aq.Attempts < 4 ? aq.Attempts + 1 : aq.Attempts;
                                        var nextAttemptValue = AssetQueue.GetNextAttemptInterval(aq.Attempts);
                                        aq.NextAttempt = DateTime.UtcNow.AddSeconds(nextAttemptValue);
                                        //attempt to get file again. call out to beacon
                                        if (aq.MediaListJson != null)
                                        {
                                            var assetList = JsonConvert.DeserializeObject<List<string>>(aq.MediaListJson);
                                            if (assetList != null)
                                            {
                                                if (assetList.Count() > 0)
                                                {
                                                    foreach (string asset in assetList)
                                                    {
                                                        var path = NFTAssetFileUtility.NFTAssetPath(asset, aq.SmartContractUID);
                                                        var fileExist = File.Exists(path);
                                                        if (!fileExist)
                                                        {
                                                            try
                                                            {
                                                                var fileCheckResult = await P2PClient.BeaconFileReadyCheck(aq.SmartContractUID, asset);
                                                                if (fileCheckResult)
                                                                {
                                                                    var beaconString = Globals.Locators.Values.FirstOrDefault().ToStringFromBase64();
                                                                    var beacon = JsonConvert.DeserializeObject<BeaconInfo.BeaconInfoJson>(beaconString);

                                                                    if (beacon != null)
                                                                    {
                                                                        BeaconResponse rsp = BeaconClient.Receive(asset, beacon.IPAddress, beacon.Port, aq.SmartContractUID);
                                                                        if (rsp.Status != 1)
                                                                        {
                                                                            //failed to download
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                            catch { }
                                                        }
                                                    }
                                                   
                                                }
                                            }
                                            
                                        }
                                        
                                    }
                                    //Look to see if media exist
                                    await NFTAssetFileUtility.CheckForAssets(aq);
                                }
                                catch { }
                            }
                        }
                    }
                }

                AssetLock = false;
            }
        }
        #endregion

        #region Block State Sync Work
        private async void DoBlockStateSyncWork(object? state)
        {
            if(!StateSyncLock)
            {
                StateSyncLock = true;
                //await StateTreiSyncService.SyncAccountStateTrei();
                StateSyncLock = false;
            }
            else
            {
                //overlap has occurred.
            }
        }

        #endregion

        #region Fortis Pool Work
        private async void DoFortisPoolWork(object? state)
        {
            try
            {
                if (Globals.StopAllTimers == false)
                {
                    if (Globals.AdjudicateAccount != null && !Globals.IsTestNet)
                    {
                        var currentTime = DateTime.Now.AddMinutes(-15);
                        var fortisPool = Globals.FortisPool.Values
                            .Select(x => new
                            {
                                x.Context.ConnectionId,
                                x.ConnectDate,
                                x.LastAnswerSendDate,
                                x.IpAddress,
                                x.Address,
                                x.UniqueName,
                                x.WalletVersion
                            }).ToList();

                        var fortisPoolStr = "";
                        fortisPoolStr = JsonConvert.SerializeObject(fortisPool);

                        var explorerNode = fortisPool.Where(x => x.Address == "RHNCRbgCs7KGdXk17pzRYAYPRKCkSMwasf").FirstOrDefault();

                        if (explorerNode != null)
                        {
                            try
                            {
                                await _hubContext.Clients.Client(explorerNode.ConnectionId).SendAsync("GetAdjMessage", "fortisPool", fortisPoolStr);
                            }
                            catch 
                            {
                                ErrorLogUtility.LogError("Failed to send fortis pool to RHNCRbgCs7KGdXk17pzRYAYPRKCkSMwasf", "ClientCallSerivce.DoFortisPoolWork()");
                            }
                        }
                    }
                }                

                //rebroadcast TXs
                var pool = TransactionData.GetPool();
                var mempool = TransactionData.GetMempool();
                var blockHeight = Globals.LastBlock.Height;
                if(mempool != null)
                {
                    var currentTime = TimeUtil.GetTime(-60);
                    if (mempool.Count() > 0)
                    {
                        foreach(var tx in mempool)
                        {
                            var txBeenBroadcasted = Globals.BroadcastedTrxDict.ContainsKey(tx.Hash);
                            if(!txBeenBroadcasted)
                            {
                                var txTime = tx.Timestamp;
                                var sendTx = currentTime > txTime ? true : false;
                                if (sendTx)
                                {
                                    var txResult = await TransactionValidatorService.VerifyTX(tx);
                                    if (txResult == true)
                                    {
                                        var dblspndChk = await TransactionData.DoubleSpendReplayCheck(tx);
                                        var isCraftedIntoBlock = await TransactionData.HasTxBeenCraftedIntoBlock(tx);

                                        if (dblspndChk == false && isCraftedIntoBlock == false && tx.TransactionRating != TransactionRating.F)
                                        {
                                            var txOutput = "";
                                            txOutput = JsonConvert.SerializeObject(tx);
                                            await _hubContext.Clients.All.SendAsync("GetAdjMessage", "tx", txOutput);//sends messages to all in fortis pool
                                            Globals.BroadcastedTrxDict[tx.Hash] = tx;
                                        }
                                        else
                                        {
                                            try
                                            {
                                                pool.DeleteManySafe(x => x.Hash == tx.Hash);// tx has been crafted into block. Remove.
                                            }
                                            catch (Exception ex)
                                            {                                                
                                                //delete failed
                                            }
                                        }
                                    }
                                    else
                                    {
                                        try
                                        {
                                            pool.DeleteManySafe(x => x.Hash == tx.Hash);// tx has been crafted into block. Remove.
                                        }
                                        catch (Exception ex)
                                        {                                            
                                            //delete failed
                                        }
                                    }

                                }
                            }
                        }
                }
                }
                
            }
            catch (Exception ex)
            {
                //no node found
                Console.WriteLine("Error: ClientCallService.DoFortisPoolWork(): " + ex.ToString());
            }
        }

        #endregion

        #region Do work V2
        private async Task DoWork_New()
        {
            try
            {
                if (Globals.StopAllTimers == false)
                {
                    if (Globals.AdjudicateAccount?.Address == Globals.LeadAddress)
                    {
                        var fortisPool = Globals.FortisPool.Values;

                        if (Globals.FortisPool.Count > 0)
                        {
                            if (FirstRun == false)
                            {
                                //
                                FirstRun = true;
                                Console.WriteLine("Doing the work **New**");
                            }
                            //get last block timestamp and current timestamp if they are more than 1 mins apart start new task
                            var lastBlockSubmitUnixTime = Globals.LastAdjudicateTime;
                            var currentUnixTime = TimeUtil.GetTime();
                            var timeDiff = (currentUnixTime - lastBlockSubmitUnixTime);
                            if (timeDiff > 20)
                            {
                                if (Globals.AdjudicateLockV2 == false)
                                {
                                    Globals.AdjudicateLockV2 = true;

                                    var taskAnswerList = Globals.TaskAnswerDict_New.Values.ToList();                                    
                                    List<TaskNumberAnswerV2>? failedTaskAnswersList = null;

                                    if (taskAnswerList.Count() > 0)
                                    {
                                        await ProcessFortisPoolV2(taskAnswerList);
                                        ConsoleWriterService.Output("Beginning Solve. Received Answers: " + taskAnswerList.Count().ToString());
                                        bool findWinner = true;
                                        int taskFindCount = 0;
                                        while (findWinner)
                                        {
                                            taskFindCount += 1;
                                            ConsoleWriterService.Output($"Current Task Find Count: {taskFindCount}");
                                            var taskWinner = await TaskWinnerUtility.TaskWinner_New(taskAnswerList, failedTaskAnswersList);
                                            if (taskWinner != null)
                                            {
                                                var taskWinnerAddr = taskWinner.Address;
                                                var acctStateTreiBalance = AccountStateTrei.GetAccountBalance(taskWinnerAddr);

                                                if (acctStateTreiBalance < 1000)
                                                {
                                                    if (Globals.FortisPool.TryRemoveFromKey2(taskWinnerAddr, out var Out))
                                                        Out.Item2.Context?.Abort();

                                                    ConsoleWriterService.Output("Address failed validation. Balance is too low.");
                                                    if (failedTaskAnswersList == null)
                                                    {
                                                        failedTaskAnswersList = new List<TaskNumberAnswerV2>();
                                                    }
                                                    failedTaskAnswersList.Add(taskWinner);
                                                }
                                                else
                                                {
                                                    ConsoleWriterService.Output("Task Winner was Found! " + taskWinner.Address);
                                                    List<FortisPool> winners = new List<FortisPool>();
                                                    var winner = fortisPool.Where(x => x.Address == taskWinner.Address).FirstOrDefault();
                                                    if (winner != null)
                                                    {
                                                        winners.Add(winner);
                                                    }
                                                    foreach (var chosen in Globals.TaskSelectedNumbersV2.Values)
                                                    {
                                                        var fortisRec = fortisPool.Where(x => x.Address == chosen.Address).FirstOrDefault();
                                                        if (fortisRec != null)
                                                        {
                                                            var alreadyIn = winners.Exists(x => x.Address == chosen.Address);
                                                            if (!alreadyIn)
                                                                winners.Add(fortisRec);
                                                        }
                                                    }

                                                    var secret = TaskWinnerUtility.GetVerifySecret();
                                                    Globals.VerifySecret = secret;

                                                    foreach (var fortis in winners)
                                                    {
                                                        //Give winners time to respond - exactly 3 seconds in total with 100ms response times per.
                                                        try
                                                        {
                                                            _ = _hubContext.Clients.Client(fortis.Context.ConnectionId).SendAsync("GetAdjMessage", "sendWinningBlock", secret)
                                                                .WaitAsync(new TimeSpan(0, 0, 0, 0, 10000));
                                                        }
                                                        catch (Exception ex)
                                                        {

                                                        }

                                                    }

                                                    //Give users time for responses to complete. They have 100ms + 3 secs here. Max 30 responses coming
                                                    await Task.Delay(3000);

                                                    while (!Globals.TaskWinnerDictV2.Any())
                                                        await Task.Delay(4);

                                                    var winningBlocks = Globals.TaskWinnerDictV2;
                                                    if (winningBlocks.TryGetValue(taskWinner.Address, out var winnersBlock))
                                                    {
                                                        //process winners block
                                                        //1.                                                         
                                                        var signature = await AdjudicatorSignBlock(winnersBlock.WinningBlock.Hash, Globals.LeadAddress);
                                                        winnersBlock.WinningBlock.AdjudicatorSignature = signature;
                                                        var result = await BlockValidatorService.ValidateBlock(winnersBlock.WinningBlock, true);
                                                        if (result == true)
                                                        {
                                                            var nextBlock = winnersBlock.WinningBlock;
                                                            ConsoleWriterService.Output("Task Completed and Block Found: " + nextBlock.Height.ToString());
                                                            ConsoleWriterService.Output(DateTime.Now.ToString());
                                                            string data = "";
                                                            data = JsonConvert.SerializeObject(nextBlock);

                                                            ConsoleWriterService.Output("Sending Blocks Now - Height: " + nextBlock.Height.ToString());
                                                            await _hubContext.Clients.All.SendAsync("GetAdjMessage", "taskResult", data);
                                                            ConsoleWriterService.Output("Done sending - Height: " + nextBlock.Height.ToString());

                                                            string taskQuestionStr = "";
                                                            TaskQuestionUtility.CreateTaskQuestion("rndNum");
                                                            ConsoleWriterService.Output("New Task Created.");
                                                            
                                                            TaskQuestion nSTaskQuestion = new TaskQuestion();
                                                            nSTaskQuestion.TaskType = "rndNum";
                                                            nSTaskQuestion.BlockHeight = Globals.LastBlock.Height + 1;

                                                            taskQuestionStr = JsonConvert.SerializeObject(nSTaskQuestion);

                                                            await ProcessFortisPoolV2(taskAnswerList);
                                                            ConsoleWriterService.Output("Fortis Pool Processed");

                                                            foreach (var answer in Globals.TaskAnswerDict_New.Values)
                                                                if (answer.NextBlockHeight <= nextBlock.Height)
                                                                    Globals.TaskAnswerDict_New.TryRemove(answer.Address, out _);

                                                            foreach (var number in Globals.TaskSelectedNumbersV2.Values)
                                                                if (number.NextBlockHeight <= nextBlock.Height)
                                                                    Globals.TaskSelectedNumbersV2.TryRemove(number.Address, out _);

                                                            foreach (var number in Globals.TaskWinnerDictV2.Values)
                                                                if (number.WinningBlock.Height <= nextBlock.Height)
                                                                    Globals.TaskWinnerDictV2.TryRemove(number.Address, out _);

                                                            Thread.Sleep(100);

                                                            Globals.VerifySecret = "";

                                                            await _hubContext.Clients.All.SendAsync("GetAdjMessage", "task", taskQuestionStr);
                                                            ConsoleWriterService.Output("Task Sent.");

                                                            findWinner = false;
                                                            taskFindCount = 0;
                                                            Globals.AdjudicateLockV2 = false;
                                                            Globals.LastAdjudicateTime = TimeUtil.GetTime();

                                                            Globals.BroadcastedTrxDict.Clear();

                                                        }
                                                        else
                                                        {
                                                            ConsoleWriterService.Output("Block failed validation");
                                                            if (failedTaskAnswersList == null)
                                                            {
                                                                failedTaskAnswersList = new List<TaskNumberAnswerV2>();
                                                            }
                                                            failedTaskAnswersList.Add(taskWinner);

                                                            while (findWinner)
                                                            {
                                                                var randChoice = new Random();
                                                                int index = randChoice.Next(winningBlocks.Count());
                                                                //winners block missing, process others randomly
                                                                var randomChosen = winningBlocks.Skip(index).First().Value;

                                                                if (randomChosen != null)
                                                                {
                                                                    winnersBlock = null;
                                                                    winnersBlock = randomChosen;
                                                                    var rSignature = await AdjudicatorSignBlock(winnersBlock.WinningBlock.Hash, Globals.LeadAddress);
                                                                    winnersBlock.WinningBlock.AdjudicatorSignature = rSignature;
                                                                    var nResult = await BlockValidatorService.ValidateBlock(winnersBlock.WinningBlock, true);
                                                                    if (nResult == true)
                                                                    {
                                                                        var nextBlock = winnersBlock.WinningBlock;
                                                                        ConsoleWriterService.Output("Task Completed and Block Found: " + nextBlock.Height.ToString());
                                                                        ConsoleWriterService.Output(DateTime.Now.ToString());
                                                                        string data = "";
                                                                        data = JsonConvert.SerializeObject(nextBlock);

                                                                        ConsoleWriterService.Output("Sending Blocks Now - Height: " + nextBlock.Height.ToString());
                                                                        await _hubContext.Clients.All.SendAsync("GetAdjMessage", "taskResult", data);
                                                                        ConsoleWriterService.Output("Done sending - Height: " + nextBlock.Height.ToString());

                                                                        string taskQuestionStr = "";
                                                                        TaskQuestionUtility.CreateTaskQuestion("rndNum");
                                                                        ConsoleWriterService.Output("New Task Created.");                                                                        
                                                                        TaskQuestion nSTaskQuestion = new TaskQuestion();
                                                                        nSTaskQuestion.TaskType = "rndNum";
                                                                        nSTaskQuestion.BlockHeight = Globals.LastBlock.Height + 1;

                                                                        taskQuestionStr = JsonConvert.SerializeObject(nSTaskQuestion);
                                                                        //await ProcessFortisPool_New(taskAnswerList);
                                                                        ConsoleWriterService.Output("Fortis Pool Processed");

                                                                        foreach (var answer in Globals.TaskAnswerDict_New.Values)
                                                                            if (answer.NextBlockHeight <= nextBlock.Height)
                                                                                Globals.TaskAnswerDict_New.TryRemove(answer.Address, out _);

                                                                        foreach (var number in Globals.TaskSelectedNumbersV2.Values)
                                                                            if (number.NextBlockHeight <= nextBlock.Height)
                                                                                Globals.TaskSelectedNumbersV2.TryRemove(number.Address, out _);

                                                                        foreach (var number in Globals.TaskWinnerDictV2.Values)
                                                                            if (number.WinningBlock.Height <= nextBlock.Height)
                                                                                Globals.TaskWinnerDictV2.TryRemove(number.Address, out _);

                                                                        Thread.Sleep(100);

                                                                        Globals.VerifySecret = "";

                                                                        await _hubContext.Clients.All.SendAsync("GetAdjMessage", "task", taskQuestionStr);
                                                                        ConsoleWriterService.Output("Task Sent.");

                                                                        findWinner = false;
                                                                        taskFindCount = 0;
                                                                        Globals.AdjudicateLockV2 = false;
                                                                        Globals.LastAdjudicateTime = TimeUtil.GetTime();

                                                                        Globals.BroadcastedTrxDict.Clear();

                                                                    }
                                                                    else
                                                                    {
                                                                        var nTaskNumAnswer = taskAnswerList.Where(x => x.Address == winnersBlock.Address).FirstOrDefault();
                                                                        ConsoleWriterService.Output("Block failed validation");
                                                                        if (nTaskNumAnswer != null)
                                                                        {
                                                                            if (failedTaskAnswersList == null)
                                                                            {
                                                                                failedTaskAnswersList = new List<TaskNumberAnswerV2>();
                                                                            }
                                                                            failedTaskAnswersList.Add(nTaskNumAnswer);
                                                                        }
                                                                        winningBlocks.TryRemove(randomChosen.Address, out _);
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        //Selecting the other closest from winning numbers
                                                        //2.
                                                        while (findWinner)
                                                        {
                                                            var randChoice = new Random();
                                                            int index = randChoice.Next(winningBlocks.Count());
                                                            //winners block missing, process others randomly
                                                            var randomChosen = winningBlocks.Skip(index).First().Value;

                                                            if (randomChosen != null)
                                                            {
                                                                winnersBlock = randomChosen;
                                                                var signature = await AdjudicatorSignBlock(winnersBlock.WinningBlock.Hash, Globals.LeadAddress);
                                                                winnersBlock.WinningBlock.AdjudicatorSignature = signature;
                                                                var result = await BlockValidatorService.ValidateBlock(winnersBlock.WinningBlock, true);
                                                                if (result == true)
                                                                {
                                                                    var nextBlock = winnersBlock.WinningBlock;
                                                                    ConsoleWriterService.Output("Task Completed and Block Found: " + nextBlock.Height.ToString());
                                                                    ConsoleWriterService.Output(DateTime.Now.ToString());
                                                                    string data = "";
                                                                    data = JsonConvert.SerializeObject(nextBlock);

                                                                    ConsoleWriterService.Output("Sending Blocks Now - Height: " + nextBlock.Height.ToString());
                                                                    await _hubContext.Clients.All.SendAsync("GetAdjMessage", "taskResult", data);
                                                                    ConsoleWriterService.Output("Done sending - Height: " + nextBlock.Height.ToString());

                                                                    string taskQuestionStr = "";
                                                                    TaskQuestionUtility.CreateTaskQuestion("rndNum");
                                                                    ConsoleWriterService.Output("New Task Created.");
                                                                    TaskQuestion nSTaskQuestion = new TaskQuestion();
                                                                    nSTaskQuestion.TaskType = "rndNum";
                                                                    nSTaskQuestion.BlockHeight = Globals.LastBlock.Height + 1;

                                                                    taskQuestionStr = JsonConvert.SerializeObject(nSTaskQuestion);
                                                                    //await ProcessFortisPool_New(taskAnswerList);
                                                                    ConsoleWriterService.Output("Fortis Pool Processed");

                                                                    foreach (var answer in Globals.TaskAnswerDict_New.Values)
                                                                        if (answer.NextBlockHeight <= nextBlock.Height)
                                                                            Globals.TaskAnswerDict_New.TryRemove(answer.Address, out _);

                                                                    foreach (var number in Globals.TaskSelectedNumbersV2.Values)
                                                                        if (number.NextBlockHeight <= nextBlock.Height)
                                                                            Globals.TaskSelectedNumbersV2.TryRemove(number.Address, out _);

                                                                    foreach (var number in Globals.TaskWinnerDictV2.Values)
                                                                        if (number.WinningBlock.Height <= nextBlock.Height)
                                                                            Globals.TaskWinnerDictV2.TryRemove(number.Address, out _);

                                                                    Thread.Sleep(100);

                                                                    Globals.VerifySecret = "";

                                                                    await _hubContext.Clients.All.SendAsync("GetAdjMessage", "task", taskQuestionStr);
                                                                    ConsoleWriterService.Output("Task Sent.");

                                                                    findWinner = false;
                                                                    taskFindCount = 0;
                                                                    Globals.AdjudicateLockV2 = false;
                                                                    Globals.LastAdjudicateTime = TimeUtil.GetTime();

                                                                    Globals.BroadcastedTrxDict.Clear();

                                                                }
                                                                else
                                                                {
                                                                    var nTaskNumAnswer = taskAnswerList.Where(x => x.Address == winnersBlock.Address).FirstOrDefault();
                                                                    ConsoleWriterService.Output("Block failed validation");
                                                                    if (nTaskNumAnswer != null)
                                                                    {
                                                                        if (failedTaskAnswersList == null)
                                                                        {
                                                                            failedTaskAnswersList = new List<TaskNumberAnswerV2>();
                                                                        }
                                                                        failedTaskAnswersList.Add(nTaskNumAnswer);
                                                                    }
                                                                    winningBlocks.TryRemove(randomChosen.Address, out _);
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                ConsoleWriterService.Output("Task Winner was Not Found!");
                                                if (failedTaskAnswersList != null)
                                                {
                                                    List<TaskNumberAnswerV2> validTaskAnswerList = taskAnswerList.Except(failedTaskAnswersList).ToList();
                                                    if (validTaskAnswerList.Count() == 0)
                                                    {
                                                        ConsoleWriterService.Output("Error in task list");
                                                        //If this happens that means not a single task answer yielded a validatable block.
                                                        //If this happens chain must be corrupt or zero validators are online.
                                                        findWinner = false;
                                                        Globals.AdjudicateLockV2 = false;
                                                    }
                                                }
                                                else
                                                {
                                                    ConsoleWriterService.Output("Task list failed to find winner");
                                                    failedTaskAnswersList = new List<TaskNumberAnswerV2>();
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Globals.AdjudicateLockV2 = false;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        //dipose timer.
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.ToString());
                Console.WriteLine("Client Call Service");
                Globals.AdjudicateLockV2 = false;
            }
        }

        #endregion

        #region Do work V3

        public static int CombineRandoms(IList<int> randoms, int minValue, int maxValue)
        {
            return Modulo(randoms.Sum(x => (long)x), maxValue - minValue) + minValue;
        }

        public static int Modulo(long k, int n)
        {
            var mod = k % n;
            return mod < 0 ? (int)mod + n : (int)mod;
        }        
        public static async Task DoWorkV3()
        {           
            if (Interlocked.Exchange(ref Globals.AdjudicateLock, 1) == 1 || Globals.AdjudicateAccount == null)
                return;

            while (Globals.Nodes.Count == 0 || Globals.StopAllTimers)
                await Task.Delay(20);
            
            var PreviousHeight = -1L;
            var PreviousRunSucceeded = false;
            var BlockDelay = Task.CompletedTask;
            ConsoleWriterService.Output("Booting up consensus loop");            
            while (true)
            {                               
                try
                {                    
                    var Height = Globals.LastBlock.Height + 1;
                    ClearRoundDicts(Height);
                    TaskQuestionUtility.CreateTaskQuestion("rndNum");
                    var State = ConsensusServer.GetState();
                    var Answer = State.Answer;
                    var Signers = Signer.CurrentSigningAddresses();
                    var Majority = Signers.Count / 2 + 1;

                    while (Globals.BlocksDownloadSlim.CurrentCount == 0 || Globals.Nodes.Values.Where(x => x.IsConnected).Count() < Majority - 1)
                        await Task.Delay(20);

                    if (Height != Globals.LastBlock.Height + 1)
                        continue;
                    
                    if(PreviousHeight != Height)
                    {                                               
                        PreviousHeight = Height;
                        var LocalTime = BlockLocalTime.GetFirstAtLeast(Math.Max(Height - 24000, (Height + Globals.BlockLock) / 2));
                        await BlockDelay;
                        var CurrentTime = TimeUtil.GetMillisecondTime();
                        var DelayTime = LocalTime != null ? 25000 - (CurrentTime - LocalTime.LocalTime) + 25000 * (Height - LocalTime.Height) : 25000;
                        DelayTime = Math.Max(DelayTime, 0);
                        BlockDelay = Task.Delay((int)DelayTime);

                        if (PreviousRunSucceeded)
                        {
                            var localTimeDb = BlockLocalTime.GetBlockLocalTimes();
                            localTimeDb.InsertSafe(new BlockLocalTime { Height = Height - 1, LocalTime = CurrentTime });
                        }
                        PreviousRunSucceeded = false;
                    }                    

                    if (Height != Globals.LastBlock.Height + 1)
                        continue;

                    ConsoleWriterService.Output("Start of Consensus at height " + Height);
                    var MyDecryptedAnswer = Height + ":" + Answer;
                    var MyEncryptedAnswer = State.EncryptedAnswer;
                    var MyEncryptedAnswerSignature = SignatureService.AdjudicatorSignature(MyEncryptedAnswer);
                    var EncryptedAnswers = await ConsensusClient.ConsensusRun(0, MyEncryptedAnswer, MyEncryptedAnswerSignature, 2000, RunType.Initial);
                    
                    if (Globals.LastBlock.Height + 1 != Height || EncryptedAnswers == null)                    
                        continue;                   

                    ConsoleWriterService.Output("EncryptedAnswer Consensus at height " + Height);
                                        
                    var MySubmissions = Globals.TaskAnswerDictV3.Where(x => x.Key.Height == Height).Select(x => x.Value).ToArray();
                    ConsoleWriterService.Output("My submission count " + MySubmissions.Length);
                    var MySubmissionsString = SerializeSubmissions(MySubmissions);
                    var MySubmissionsSignature = SignatureService.AdjudicatorSignature(MySubmissionsString);
                    var Submissions = await ConsensusClient.ConsensusRun(1, MySubmissionsString, MySubmissionsSignature, 2000, RunType.Middle);

                    if (Globals.LastBlock.Height + 1 != Height || Submissions == null)
                        continue;

                    ConsoleWriterService.Output("Submissions Consensus at height " + Height);

                    var ValidSubmissions = Submissions.Select(x => DeserializeSubmissions(x.Message))
                        .SelectMany(x => x)
                        .Where(x => SignatureService.VerifySignature(x.RBXAddress, Height + ":" + x.Answer, x.Signature))
                        .Select(x => (x.IPAddress, x.RBXAddress, x.Answer))
                        .Distinct()
                        .ToArray();

                    if (!ValidSubmissions.Any())
                        continue;

                    ConsoleWriterService.Output("Number of valid submissions: " + ValidSubmissions.Length);                    

                    var BadIPs = ValidSubmissions.Select(x => x.IPAddress).GroupBy(x => x).Where(x => x.Count() > 1)
                        .Select(x => x.First()).ToHashSet();
                    var BadAddresses = ValidSubmissions.Select(x => x.RBXAddress).GroupBy(x => x).Where(x => x.Count() > 1)
                        .Select(x => x.First()).ToHashSet();

                    try
                    {
                        foreach (var ip in BadIPs)
                            if (Globals.FortisPool.TryRemoveFromKey1(ip, out var pool))
                                pool.Item2.Context?.Abort();

                        foreach (var address in BadAddresses)
                            if (Globals.FortisPool.TryRemoveFromKey2(address, out var pool))
                                pool.Item2.Context?.Abort();
                    }
                    catch { }

                    ConsensusServer.UpdateState(isUsed: true);
                    var DecryptedAnswers = await ConsensusClient.ConsensusRun(2, MyDecryptedAnswer, MyEncryptedAnswer, 2000, RunType.Middle);

                    if (Globals.LastBlock.Height + 1 != Height || DecryptedAnswers == null)
                        continue;

                    ConsoleWriterService.Output("DecryptedAnswer Consensus at height " + Height);

                    var Answers = DecryptedAnswers.Select(x =>
                    {
                        var split = x.Message.Split(':');
                        var encryptedAnswer = EncryptedAnswers.Where(y => y.Address == x.Address).FirstOrDefault();
                        if (split.Length != 2 || long.Parse(split[0]) != Height || encryptedAnswer.Address == null ||
                            !SignatureService.VerifySignature(x.Address, x.Message, encryptedAnswer.Message))
                            return -1;
                        return int.Parse(split[1]);
                    })
                    .Where(x => x != -1)
                    .ToArray();
                    var ChosenAnswer = CombineRandoms(Answers, 0, int.MaxValue);
                    ConsoleWriterService.Output("Chosen answer: " + ChosenAnswer);

                    if (Answers.Length < Majority)
                        continue;

                    var PotentialWinners = ValidSubmissions
                        .Where(x => !BadIPs.Contains(x.IPAddress) && !BadAddresses.Contains(x.RBXAddress))
                        .GroupBy(x => x.Answer)
                        .Where(x => x.Count() == 1)
                        .Select(x => x.First())
                        .OrderBy(x => Math.Abs(x.Answer - ChosenAnswer))
                        .ThenBy(x => x.Answer)                        
                        .Where(x => AccountStateTrei.GetAccountBalance(x.RBXAddress) >= 1000M)
                        .Take(30)
                        .ToArray();

                    foreach (var winner in PotentialWinners)
                        Globals.TaskSelectedNumbersV3[(winner.RBXAddress, Height)] = winner;

                    var WinnerPool = PotentialWinners
                        .Select(x =>
                        {
                            if (Globals.FortisPool.TryGetFromKey2(x.RBXAddress, out var Out))
                                return Out.Value;
                            return null;
                        })
                        .Where(x => x != null)
                        .ToArray();

                    ConsoleWriterService.Output("Potential winners total: " + PotentialWinners.Length + 
                        ". Requesting total: " + WinnerPool.Length);
                    foreach (var fortis in WinnerPool)
                    {
                        try
                        {
                            _ = HubContext.Clients.Client(fortis.Context.ConnectionId).SendAsync("GetAdjMessage", "sendWinningBlock", "")
                                                                    .WaitAsync(new TimeSpan(0, 0, 0, 0, 3000));
                        }
                        catch (Exception ex)
                        {
                        }
                    }
                    
                    await Task.Delay(3000);
                    var MySubmittedWinners = PotentialWinners.Select(x =>
                    {
                        if (Globals.TaskWinnerDictV3.TryGetValue((x.RBXAddress, Height), out var block))
                            return block;
                        return null;
                    })
                    .Where(x => x != null)
                    .ToArray();

                    ConsoleWriterService.Output("Received winners total: " + MySubmittedWinners.Length);

                    var MySubmittedWinnersString = JsonConvert.SerializeObject(MySubmittedWinners);
                    var MySubmittedWinnersSignature = SignatureService.AdjudicatorSignature(MySubmittedWinnersString);
                    var SubmittedWinners = await ConsensusClient.ConsensusRun(3, MySubmittedWinnersString, MySubmittedWinnersSignature, 2000, RunType.Middle);

                    if (Globals.LastBlock.Height + 1 != Height || SubmittedWinners == null)
                        continue;

                    ConsoleWriterService.Output("Submitted Winner Consensus at height " + Height);                    

                    var WinnerDict = SubmittedWinners.Select(x => JsonConvert.DeserializeObject<Block[]>(x.Message))
                        .SelectMany(x => x)
                        .GroupBy(x => x.Hash)
                        .Select(x => x.First())
                        .GroupBy(x => x.Validator)
                        .Select(x => x.First())
                        .ToDictionary(x => x.Validator, x => x);

                    var OrderedWinners = PotentialWinners.Select(x => WinnerDict.GetValueOrDefault(x.RBXAddress)).Where(x => x != null).ToArray();

                    ConsoleWriterService.Output("Winner collected total: " + OrderedWinners.Length);

                    Block Winner = null;
                    foreach(var winner in OrderedWinners)
                        if(await BlockValidatorService.ValidateBlockForTask(winner, true))
                        {
                            Winner = winner;
                            break;
                        }

                    if(Winner == null)
                        continue;

                    ConsoleWriterService.Output("Winner found. Hash: " + Winner.Hash);

                    var WinnerHasheSignature = Winner.Hash + ":" + SignatureService.AdjudicatorSignature(Winner.Hash);                    
                    var WinnerHashSignature = SignatureService.AdjudicatorSignature(WinnerHasheSignature);                    
                                        
                    var HashResult = await ConsensusClient.ConsensusRun(4, WinnerHasheSignature, WinnerHashSignature, 2000, RunType.Last);

                    if (Globals.LastBlock.Height + 1 != Height || HashResult == null)
                        continue;

                    ConsoleWriterService.Output("WinnerHashSignature Consensus at height " + Height);                    

                    var Hashes = HashResult.Select(x => {
                        var split = x.Message.Split(':');
                        return (x.Address, Hash: split[0], Signature: split[1]);
                    })
                    .Where(x => x.Hash == Winner.Hash)
                    .Take(Majority)
                    .ToArray();

                    if(Hashes.Length != Majority)
                        continue;
                  
                    var signature = string.Join("|", Hashes.Select(x => x.Address + ":" + x.Signature));
                    Winner.AdjudicatorSignature = signature;

                    ConsensusServer.UpdateState(methodCode: 0, status: (int)ConsensusStatus.Processing);
                    if (await BlockValidatorService.ValidateBlock(Winner, false))
                        PreviousRunSucceeded = true;
                }
                catch(Exception ex)
                {
                    Console.WriteLine("Error: " + ex.ToString());
                    Console.WriteLine("Client Call Service");
                }
            }
        }

        public static async Task FinalizeWork(Block block)
        {
            try
            {
                if (Globals.BlocksDownloadSlim.CurrentCount == 0)
                    return;

                ConsoleWriterService.Output("Task Completed and Block Found: " + block.Height.ToString());
                ConsoleWriterService.Output(DateTime.Now.ToString());

                var Now = TimeUtil.GetMillisecondTime();
                Console.WriteLine("Sending Blocks Now - Height: " + block.Height.ToString() + " at " + Now);
                var data = JsonConvert.SerializeObject(block);

                foreach (var node in Globals.Nodes.Values.Where(x => x.Address != Globals.AdjudicateAccount.Address))
                {
                    try
                    {
                        _ = node.InvokeAsync<string>("ReceiveBlock", args: new object?[] { block });
                    }
                    catch { }
                }

                if (HubContext?.Clients != null)
                    await HubContext.Clients.All.SendAsync("GetAdjMessage", "taskResult", data);
                Now = TimeUtil.GetMillisecondTime();
                Console.WriteLine("Done sending - Height: " + block.Height.ToString() + " at " + Now);

                await ProcessFortisPoolV3(Globals.TaskAnswerDictV3.Keys.Select(x => x.RBXAddress).ToArray());
                ConsoleWriterService.Output("Fortis Pool Processed");

                foreach (var key in Globals.TaskAnswerDictV3.Keys)
                    if (key.Height <= block.Height)
                        Globals.TaskAnswerDictV3.TryRemove(key, out _);

                Globals.LastAdjudicateTime = TimeUtil.GetTime();
                Globals.BroadcastedTrxDict.Clear();
            }
            catch { }
        }

        public static void ClearRoundDicts(long height)
        {
            ConsensusServer.Messages.Clear();
            ConsensusServer.Hashes.Clear();

            foreach (var key in Globals.TaskSelectedNumbersV3.Keys)
                if (key.Height <= height)
                    Globals.TaskSelectedNumbersV3.TryRemove(key, out _);

            foreach (var key in Globals.TaskWinnerDictV3.Keys)
                if (key.Height <= height)
                    Globals.TaskWinnerDictV3.TryRemove(key, out _);           
        }

        public static string SerializeSubmissions((string IPAddress, string RBXAddress, int Answer, string Signature)[] submissions)
        {
            return string.Join("|", submissions.Select(x => x.IPAddress + ":" + x.RBXAddress + ":" + x.Answer + ":" + x.Signature));
        }

        public static (string IPAddress, string RBXAddress, int Answer, string Signature)[] DeserializeSubmissions(string submisisons)
        {
            return submisisons.Split('|').Where(x => !string.IsNullOrWhiteSpace(x)).Select(x =>
            {
                var split = x.Split(':');
                return (split[0], split[1], int.Parse(split[2]), split[3]);
            }).ToArray();
        }

        #endregion

        #region Do Work()

        private async void DoWork(object? state)
        {
            if(Globals.LastBlock.Height < Globals.BlockLock)
            {
                await DoWork_New();
            }
            else
            {
                await DoWorkV3();
            }
        }

        #endregion

        #region Adjudicator Sign Block 

        private async Task<string> AdjudicatorSignBlock(string message, string address)
        {            
            var account = AccountData.GetSingleAccount(address);

            BigInteger b1 = BigInteger.Parse(account.GetKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
            PrivateKey privateKey = new PrivateKey("secp256k1", b1);

            var sig = SignatureService.CreateSignature(message, privateKey, account.PublicKey);

            return sig;
        }

        #endregion

        #region Process Fortis Pool
        public async Task ProcessFortisPoolV2(IList<TaskNumberAnswerV2> taskAnswerList)
        {
            try
            {
                if (taskAnswerList != null)
                {
                    foreach (TaskNumberAnswerV2 taskAnswer in taskAnswerList)
                    {
                        if (Globals.FortisPool.TryGetFromKey2(taskAnswer.Address, out var validator))
                            validator.Value.LastAnswerSendDate = DateTime.Now;
                    }
                }

                var nodeWithAnswer = Globals.FortisPool.Values.Where(x => x.LastAnswerSendDate != null).ToList();
                var deadNodes = nodeWithAnswer.Where(x => x.LastAnswerSendDate.Value.AddMinutes(15) <= DateTime.Now).ToList();
                foreach (var deadNode in deadNodes)
                {
                    Globals.FortisPool.TryRemoveFromKey1(deadNode.IpAddress, out _);
                    deadNode.Context?.Abort();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: ClientCallService.ProcessFortisPool: " + ex.ToString());
            }
        }

        public static async Task ProcessFortisPoolV3(IList<string> rbxAddressSubmissions)
        {
            try
            {
                if (rbxAddressSubmissions != null)
                {
                    foreach (var address in rbxAddressSubmissions)
                    {
                        if (Globals.FortisPool.TryGetFromKey2(address, out var validator))
                            validator.Value.LastAnswerSendDate = DateTime.Now;
                    }
                }

                var nodeWithAnswer = Globals.FortisPool.Values.Where(x => x.LastAnswerSendDate != null).ToList();
                var deadNodes = nodeWithAnswer.Where(x => x.LastAnswerSendDate.Value.AddMinutes(15) <= DateTime.Now).ToList();
                foreach (var deadNode in deadNodes)
                {
                    Globals.FortisPool.TryRemoveFromKey1(deadNode.IpAddress, out _);
                    deadNode.Context?.Abort();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: ClientCallService.ProcessFortisPool: " + ex.ToString());
            }
        }

        #endregion

        #region Stop and Dispose

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer.Dispose();
            _fortisPoolTimer.Dispose();
            _blockStateSyncTimer.Dispose();
            _checkpointTimer.Dispose();
        }

        #endregion

        #region Send Message

        public async Task SendMessage(string message, string data)
        {
            await _hubContext.Clients.All.SendAsync("GetAdjMessage", message, data);
        }

        #endregion
    }
}
