using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Utilities;
using System.Globalization;
using System.IO.Compression;
using System.Numerics;
using System.Security;

namespace ReserveBlockCore.Services
{
    public class ClientCallService : IHostedService, IDisposable
    {
        private readonly IHubContext<P2PAdjServer> _hubContext;
        private readonly IHostApplicationLifetime _appLifetime;
        private int executionCount = 0;
        private Timer _timer = null!;
        private Timer _fortisPoolTimer = null!;
        private Timer _checkpointTimer = null!;
        private Timer _blockStateSyncTimer = null;
        private Timer _encryptedPasswordTimer = null;
        private static bool FirstRun = false;
        private static bool StateSyncLock = false;

        public ClientCallService(IHubContext<P2PAdjServer> hubContext, IHostApplicationLifetime appLifetime)
        {
            _hubContext = hubContext;
            _appLifetime = appLifetime;
        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            _timer = new Timer(DoWork, null, TimeSpan.FromSeconds(60),
                TimeSpan.FromSeconds(2));

            _fortisPoolTimer = new Timer(DoFortisPoolWork, null, TimeSpan.FromSeconds(240),
                TimeSpan.FromMinutes(5));

            _blockStateSyncTimer = new Timer(DoBlockStateSyncWork, null, TimeSpan.FromSeconds(100),
                TimeSpan.FromHours(8));

            if (Globals.ChainCheckPoint == true)
            {
                var interval = Globals.ChainCheckPointInterval;
                
                _checkpointTimer = new Timer(DoCheckpointWork, null, TimeSpan.FromSeconds(240),
                TimeSpan.FromHours(interval));
            }

            _encryptedPasswordTimer = new Timer(DoPasswordClearWork, null, TimeSpan.FromSeconds(5),
                TimeSpan.FromMinutes(Globals.PasswordClearTime));

            return Task.CompletedTask;
        }

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
                ErrorLogUtility.LogError($"Error creating checkpoint. Error Message: {ex.Message}", "ClientCallService.DoCheckpointWork()");
            }
        }

        #endregion

        #region Password Clear Work

        private async void DoPasswordClearWork(object? state)
        {
            if(Globals.IsWalletEncrypted == true)
            {
                Globals.EncryptPassword.Dispose();
                Globals.EncryptPassword = new SecureString();
            }
        }
        #endregion

        #region Block State Sync Work
        private async void DoBlockStateSyncWork(object? state)
        {
            if(!StateSyncLock)
            {
                StateSyncLock = true;
                await StateTreiSyncService.SyncAccountStateTrei();
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
                    if (Globals.Adjudicate)
                    {
                        var currentTime = DateTime.Now.AddMinutes(-15);
                        var fortisPool = Globals.FortisPool.Where(x => x.LastAnswerSendDate >= currentTime);

                        var fortisPoolStr = "";
                        fortisPoolStr = JsonConvert.SerializeObject(fortisPool);

                        var explorerNode = fortisPool.Where(x => x.Address == "RHNCRbgCs7KGdXk17pzRYAYPRKCkSMwasf").FirstOrDefault();

                        if (explorerNode != null)
                        {
                            await _hubContext.Clients.Client(explorerNode.ConnectionId).SendAsync("GetAdjMessage", "fortisPool", fortisPoolStr);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //no node found
                Console.WriteLine("****************DoFortisPoolWork - Failed****************");
            }
        }

        #endregion

        #region Do work **Deprecated
        private async Task DoWork_Deprecated()
        {
            try
            {
                if (Globals.StopAllTimers == false)
                {
                    if (Globals.Adjudicate)
                    {
                        var fortisPool = Globals.FortisPool;

                        if (fortisPool.Count() > 0)
                        {
                            if (FirstRun == false)
                            {
                                //
                                FirstRun = true;
                                Console.WriteLine("Doing the work");
                            }
                            //get last block timestamp and current timestamp if they are more than 1 mins apart start new task
                            var lastBlockSubmitUnixTime = Globals.LastAdjudicateTime;
                            var currentUnixTime = TimeUtil.GetTime();
                            var timeDiff = (currentUnixTime - lastBlockSubmitUnixTime);

                            if (timeDiff > 25)
                            {
                                if (Globals.AdjudicateLock == false)
                                {
                                    Globals.AdjudicateLock = true;

                                    //once greater commit block winner
                                    var taskAnswerList = Globals.TaskAnswerList;
                                    var taskQuestion = Globals.CurrentTaskQuestion;
                                    List<TaskAnswer>? failedTaskAnswersList = null;

                                    if (taskAnswerList.Count() > 0)
                                    {
                                        ConsoleWriterService.Output("Beginning Solve. Received Answers: " + taskAnswerList.Count().ToString());
                                        bool findWinner = true;
                                        int taskFindCount = 0;
                                        while (findWinner)
                                        {
                                            taskFindCount += 1;
                                            ConsoleWriterService.Output($"Current Task Find Count: {taskFindCount}");
                                            var taskWinner = await TaskWinnerUtility.TaskWinner(taskQuestion, taskAnswerList, failedTaskAnswersList);
                                            if (taskWinner != null)
                                            {
                                                var taskWinnerAddr = taskWinner.Address;
                                                var acctStateTreiBalance = AccountStateTrei.GetAccountBalance(taskWinnerAddr);

                                                if (acctStateTreiBalance < 1000)
                                                {
                                                    ConsoleWriterService.Output("Address failed validation. Balance is too low.");
                                                    if (failedTaskAnswersList == null)
                                                    {
                                                        failedTaskAnswersList = new List<TaskAnswer>();
                                                    }
                                                    failedTaskAnswersList.Add(taskWinner);
                                                }
                                                else
                                                {
                                                    ConsoleWriterService.Output("Task Winner was Found! " + taskWinner.Address);
                                                    var nextBlock = taskWinner.Block;
                                                    if (nextBlock != null)
                                                    {
                                                        var result = await BlockValidatorService.ValidateBlock(nextBlock);
                                                        if (result == true)
                                                        {
                                                            ConsoleWriterService.Output("Task Completed and Block Found: " + nextBlock.Height.ToString());
                                                            ConsoleWriterService.Output(DateTime.Now.ToString());
                                                            string data = "";
                                                            data = JsonConvert.SerializeObject(nextBlock);

                                                            await _hubContext.Clients.All.SendAsync("GetAdjMessage", "taskResult", data);
                                                            ConsoleWriterService.Output("Sending Blocks Now - Height: " + nextBlock.Height.ToString());
                                                            //Update submit time to wait another 28 seconds to process.


                                                            //send new puzzle and wait for next challenge completion
                                                            string taskQuestionStr = "";
                                                            var nTaskQuestion = await TaskQuestionUtility.CreateTaskQuestion("rndNum");
                                                            ConsoleWriterService.Output("New Task Created.");
                                                            Globals.CurrentTaskQuestion = nTaskQuestion;
                                                            TaskQuestion nSTaskQuestion = new TaskQuestion();
                                                            nSTaskQuestion.TaskType = nTaskQuestion.TaskType;
                                                            nSTaskQuestion.BlockHeight = nTaskQuestion.BlockHeight;

                                                            taskQuestionStr = JsonConvert.SerializeObject(nSTaskQuestion);


                                                            await ProcessFortisPool_Deprecated(taskAnswerList);
                                                            ConsoleWriterService.Output("Fortis Pool Processed");

                                                            if (Globals.TaskAnswerList != null)
                                                            {
                                                                //P2PAdjServer.TaskAnswerList.Clear();
                                                                //P2PAdjServer.TaskAnswerList.TrimExcess();
                                                                Globals.TaskAnswerList.RemoveAll(x => x.Block.Height <= nextBlock.Height);
                                                            }

                                                            Thread.Sleep(1000);

                                                            await _hubContext.Clients.All.SendAsync("GetAdjMessage", "task", taskQuestionStr);
                                                            ConsoleWriterService.Output("Task Sent.");

                                                            findWinner = false;
                                                            taskFindCount = 0;
                                                            Globals.AdjudicateLock = false;
                                                            Globals.LastAdjudicateTime = TimeUtil.GetTime();

                                                            Globals.BroadcastedTrxList = new List<Transaction>();
                                                        }
                                                        else
                                                        {
                                                            Console.WriteLine("Block failed validation");
                                                            if (failedTaskAnswersList == null)
                                                            {
                                                                failedTaskAnswersList = new List<TaskAnswer>();
                                                            }
                                                            failedTaskAnswersList.Add(taskWinner);
                                                        }
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                ConsoleWriterService.Output("Task Winner was Not Found!");
                                                if (failedTaskAnswersList != null)
                                                {
                                                    List<TaskAnswer> validTaskAnswerList = taskAnswerList.Except(failedTaskAnswersList).ToList();
                                                    if (validTaskAnswerList.Count() == 0)
                                                    {
                                                        ConsoleWriterService.Output("Error in task list");
                                                        //If this happens that means not a single task answer yielded a validatable block.
                                                        //If this happens chain must be corrupt or zero validators are online.
                                                        findWinner = false;
                                                        Globals.AdjudicateLock = false;
                                                    }
                                                }
                                                else
                                                {
                                                    ConsoleWriterService.Output("Task list failed to find winner");
                                                    failedTaskAnswersList = new List<TaskAnswer>();
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {

                                        Globals.AdjudicateLock = false;
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
                Console.WriteLine("Error: " + ex.Message);
                Console.WriteLine("Client Call Service");
                Globals.AdjudicateLock = false;
            }
        }

        #endregion

        #region Do work **NEW
        public async Task DoWork_New()
        {
            try
            {
                if (Globals.StopAllTimers == false)
                {
                    if (Globals.Adjudicate)
                    {
                        var fortisPool = Globals.FortisPool;

                        if (fortisPool.Count() > 0)
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
                                if (Globals.AdjudicateLock == false)
                                {
                                    Globals.AdjudicateLock = true;

                                    var taskAnswerList = Globals.TaskAnswerList_New;
                                    var taskQuestion = Globals.CurrentTaskQuestion;
                                    List<TaskNumberAnswer>? failedTaskAnswersList = null;

                                    if (taskAnswerList.Count() > 0)
                                    {
                                        ConsoleWriterService.Output("Beginning Solve. Received Answers: " + taskAnswerList.Count().ToString());
                                        bool findWinner = true;
                                        int taskFindCount = 0;
                                        while (findWinner)
                                        {
                                            taskFindCount += 1;
                                            ConsoleWriterService.Output($"Current Task Find Count: {taskFindCount}");
                                            var taskWinner = await TaskWinnerUtility.TaskWinner_New(taskQuestion, taskAnswerList, failedTaskAnswersList);
                                            if (taskWinner != null)
                                            {
                                                var taskWinnerAddr = taskWinner.Address;
                                                var acctStateTreiBalance = AccountStateTrei.GetAccountBalance(taskWinnerAddr);

                                                if (acctStateTreiBalance < 1000)
                                                {
                                                    ConsoleWriterService.Output("Address failed validation. Balance is too low.");
                                                    if (failedTaskAnswersList == null)
                                                    {
                                                        failedTaskAnswersList = new List<TaskNumberAnswer>();
                                                    }
                                                    failedTaskAnswersList.Add(taskWinner);
                                                }
                                                else
                                                {
                                                    ConsoleWriterService.Output("Task Winner was Found! " + taskWinner.Address);
                                                    List<FortisPool> winners = new List<FortisPool>();
                                                    var winner = Globals.FortisPool.Where(x => x.Address == taskWinner.Address).FirstOrDefault();
                                                    if(winner != null)
                                                    {
                                                        winners.Add(winner);
                                                    }
                                                    foreach (var chosen in Globals.TaskSelectedNumbers)
                                                    {
                                                        var fortisRec = Globals.FortisPool.Where(x => x.Address == chosen.Address).FirstOrDefault();
                                                        if(fortisRec != null)
                                                        {
                                                            var alreadyIn = winners.Exists(x => x.Address == chosen.Address);
                                                            if(!alreadyIn)
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
                                                            await _hubContext.Clients.Client(fortis.ConnectionId).SendAsync("GetAdjMessage", "sendWinningBlock", secret)
                                                                .WaitAsync(new TimeSpan(0, 0, 0, 0, 100));
                                                        }
                                                        catch(Exception ex)
                                                        {

                                                        }
                                                        
                                                    }

                                                    //Give users time for responses to complete. They have 100ms + 3 secs here. Max 30 responses coming
                                                    await Task.Delay(3000);

                                                    var winningBlocks = Globals.TaskWinnerList;
                                                    var winnersBlock = winningBlocks.Where(x => x.Address == taskWinner.Address).FirstOrDefault();
                                                    if(winnersBlock != null)
                                                    {
                                                        //process winners block
                                                        //1. 
                                                        var signature = await AdjudicatorSignBlock(winnersBlock.WinningBlock.Hash);
                                                        winnersBlock.WinningBlock.AdjudicatorSignature = signature;
                                                        var result = await BlockValidatorService.ValidateBlock(winnersBlock.WinningBlock);
                                                        if(result == true)
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
                                                            var nTaskQuestion = await TaskQuestionUtility.CreateTaskQuestion("rndNum");
                                                            ConsoleWriterService.Output("New Task Created.");
                                                            Globals.CurrentTaskQuestion = nTaskQuestion;
                                                            TaskQuestion nSTaskQuestion = new TaskQuestion();
                                                            nSTaskQuestion.TaskType = nTaskQuestion.TaskType;
                                                            nSTaskQuestion.BlockHeight = nTaskQuestion.BlockHeight;

                                                            taskQuestionStr = JsonConvert.SerializeObject(nSTaskQuestion);
                                                            await ProcessFortisPool_New(taskAnswerList);
                                                            ConsoleWriterService.Output("Fortis Pool Processed");
                                                            if (Globals.TaskAnswerList_New.Count() > 0)
                                                            {
                                                                Globals.TaskAnswerList_New.RemoveAll(x => x.NextBlockHeight <= nextBlock.Height);
                                                            }
                                                            if (Globals.TaskAnswerList.Count() > 0)
                                                            {
                                                                Globals.TaskAnswerList.RemoveAll(x => x.Block.Height <= nextBlock.Height);
                                                            }
                                                            if (Globals.TaskSelectedNumbers.Count() > 0)
                                                            {
                                                                Globals.TaskSelectedNumbers.RemoveAll(x => x.NextBlockHeight <= nextBlock.Height);
                                                            }
                                                            if (Globals.TaskWinnerList.Count() > 0)
                                                            {
                                                                Globals.TaskWinnerList.RemoveAll(x => x.WinningBlock.Height <= nextBlock.Height);
                                                            }

                                                            Thread.Sleep(100);

                                                            Globals.VerifySecret = "";

                                                            await _hubContext.Clients.All.SendAsync("GetAdjMessage", "task", taskQuestionStr);
                                                            ConsoleWriterService.Output("Task Sent.");

                                                            findWinner = false;
                                                            taskFindCount = 0;
                                                            Globals.AdjudicateLock = false;
                                                            Globals.LastAdjudicateTime = TimeUtil.GetTime();

                                                            Globals.BroadcastedTrxList = new List<Transaction>();

                                                        }
                                                        else
                                                        {
                                                            ConsoleWriterService.Output("Block failed validation");
                                                            if (failedTaskAnswersList == null)
                                                            {
                                                                failedTaskAnswersList = new List<TaskNumberAnswer>();
                                                            }
                                                            failedTaskAnswersList.Add(taskWinner);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        //Selecting the other closest from winning numbers
                                                        //2.
                                                        var randChoice = new Random();
                                                        int index = randChoice.Next(winningBlocks.Count());
                                                        //winners block missing, process others randomly
                                                        var randomChosen = winningBlocks[index];

                                                        if(randomChosen != null)
                                                        {
                                                            winnersBlock = randomChosen;
                                                            var signature = await AdjudicatorSignBlock(winnersBlock.WinningBlock.Hash);
                                                            winnersBlock.WinningBlock.AdjudicatorSignature = signature;
                                                            var result = await BlockValidatorService.ValidateBlock(winnersBlock.WinningBlock);
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
                                                                var nTaskQuestion = await TaskQuestionUtility.CreateTaskQuestion("rndNum");
                                                                ConsoleWriterService.Output("New Task Created.");
                                                                Globals.CurrentTaskQuestion = nTaskQuestion;
                                                                TaskQuestion nSTaskQuestion = new TaskQuestion();
                                                                nSTaskQuestion.TaskType = nTaskQuestion.TaskType;
                                                                nSTaskQuestion.BlockHeight = nTaskQuestion.BlockHeight;

                                                                taskQuestionStr = JsonConvert.SerializeObject(nSTaskQuestion);
                                                                await ProcessFortisPool_New(taskAnswerList);
                                                                ConsoleWriterService.Output("Fortis Pool Processed");

                                                                if (Globals.TaskAnswerList_New.Count() > 0)
                                                                {
                                                                    Globals.TaskAnswerList_New.RemoveAll(x => x.NextBlockHeight <= nextBlock.Height);
                                                                }
                                                                if (Globals.TaskAnswerList.Count() > 0)
                                                                {
                                                                    Globals.TaskAnswerList.RemoveAll(x => x.Block.Height <= nextBlock.Height);
                                                                }
                                                                if (Globals.TaskSelectedNumbers.Count() > 0)
                                                                {
                                                                    Globals.TaskSelectedNumbers.RemoveAll(x => x.NextBlockHeight <= nextBlock.Height);
                                                                }
                                                                if (Globals.TaskWinnerList.Count() > 0)
                                                                {
                                                                    Globals.TaskWinnerList.RemoveAll(x => x.WinningBlock.Height <= nextBlock.Height);
                                                                }

                                                                Thread.Sleep(100);

                                                                Globals.VerifySecret = "";

                                                                await _hubContext.Clients.All.SendAsync("GetAdjMessage", "task", taskQuestionStr);
                                                                ConsoleWriterService.Output("Task Sent.");

                                                                findWinner = false;
                                                                taskFindCount = 0;
                                                                Globals.AdjudicateLock = false;
                                                                Globals.LastAdjudicateTime = TimeUtil.GetTime();

                                                                Globals.BroadcastedTrxList = new List<Transaction>();

                                                            }
                                                            else
                                                            {
                                                                ConsoleWriterService.Output("Block failed validation");
                                                                if (failedTaskAnswersList == null)
                                                                {
                                                                    failedTaskAnswersList = new List<TaskNumberAnswer>();
                                                                }
                                                                failedTaskAnswersList.Add(taskWinner);
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
                                                    List<TaskNumberAnswer> validTaskAnswerList = taskAnswerList.Except(failedTaskAnswersList).ToList();
                                                    if (validTaskAnswerList.Count() == 0)
                                                    {
                                                        ConsoleWriterService.Output("Error in task list");
                                                        //If this happens that means not a single task answer yielded a validatable block.
                                                        //If this happens chain must be corrupt or zero validators are online.
                                                        findWinner = false;
                                                        Globals.AdjudicateLock = false;
                                                    }
                                                }
                                                else
                                                {
                                                    ConsoleWriterService.Output("Task list failed to find winner");
                                                    failedTaskAnswersList = new List<TaskNumberAnswer>();
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Globals.AdjudicateLock = false;
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
                Console.WriteLine("Error: " + ex.Message);
                Console.WriteLine("Client Call Service");
                Globals.AdjudicateLock = false;
            }
        }

        #endregion

        private async void DoWork(object? state)
        {
            if(Globals.LastBlock.Height < Globals.BlockLock)
            {
                await DoWork_Deprecated();
            }
            else
            {
                await DoWork_New();
            }
            
        }

        private async Task<string> AdjudicatorSignBlock(string message)
        {
            var leadAdj = Globals.LeadAdjudicator;
            var account = AccountData.GetSingleAccount(leadAdj.Address);

            var accPrivateKey = GetPrivateKeyUtility.GetPrivateKey(account.PrivateKey, account.Address);

            BigInteger b1 = BigInteger.Parse(accPrivateKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
            PrivateKey privateKey = new PrivateKey("secp256k1", b1);

            var sig = SignatureService.CreateSignature(message, privateKey, account.PublicKey);

            return sig;
        }

        #region Process Fortis Pool **NEW
        public async Task ProcessFortisPool_New(List<TaskNumberAnswer> taskAnswerList)
        {
            try
            {
                var pool = Globals.FortisPool;
                var result = pool.GroupBy(x => x.Address).Where(x => x.Count() > 1).Select(y => y.OrderByDescending(z => z.ConnectDate).ToList()).ToList();

                if (result.Count() > 0)
                {
                    result.ForEach(x =>
                    {
                        var recKeep = x.First();
                        Globals.FortisPool.RemoveAll(f => f.ConnectionId != recKeep.ConnectionId && f.Address == recKeep.Address);
                    });
                }

                if (taskAnswerList != null)
                {
                    foreach (TaskNumberAnswer taskAnswer in taskAnswerList)
                    {
                        var validator = Globals.FortisPool.Where(x => x.Address == taskAnswer.Address).FirstOrDefault();
                        {
                            if (validator != null)
                            {
                                validator.LastAnswerSendDate = DateTime.Now;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: ClientCallService.ProcessFortisPool: " + ex.Message);
            }

        }

        #endregion

        #region Process Fortis Pool **Deprecated
        public async Task ProcessFortisPool_Deprecated(List<TaskAnswer> taskAnswerList)
        {
            try
            {
                var pool = Globals.FortisPool;
                var result = pool.GroupBy(x => x.Address).Where(x => x.Count() > 1).Select(y => y.OrderByDescending(z => z.ConnectDate).ToList()).ToList();

                if (result.Count() > 0)
                {
                    result.ForEach(x =>
                    {
                        var recKeep = x.First();
                        Globals.FortisPool.RemoveAll(f => f.ConnectionId != recKeep.ConnectionId && f.Address == recKeep.Address);
                    });
                }

                if (taskAnswerList != null)
                {
                    foreach (TaskAnswer taskAnswer in taskAnswerList)
                    {
                        var validator = Globals.FortisPool.Where(x => x.Address == taskAnswer.Address).FirstOrDefault();
                        {
                            if (validator != null)
                            {
                                validator.LastAnswerSendDate = DateTime.Now;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: ClientCallService.ProcessFortisPool: " + ex.Message);
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
