using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace ReserveBlockCore.Nodes
{
    public class ValidatorProcessor : IHostedService, IDisposable
    {
        public static IHubContext<P2PValidatorServer> HubContext;
        private readonly IHubContext<P2PValidatorServer> _hubContext;
        private readonly IHostApplicationLifetime _appLifetime;
        static SemaphoreSlim BroadcastNetworkValidatorLock = new SemaphoreSlim(1, 1);
        static SemaphoreSlim CheckNetworkValidatorsLock = new SemaphoreSlim(1, 1);
        static SemaphoreSlim GenerateProofLock = new SemaphoreSlim(1, 1);
        static SemaphoreSlim ProduceBlockLock = new SemaphoreSlim(1, 1);
        static SemaphoreSlim ConfirmBlockLock = new SemaphoreSlim(1, 1);
        static SemaphoreSlim SendWinningVoteLock = new SemaphoreSlim(1, 1);
        static SemaphoreSlim LockWinnerLock = new SemaphoreSlim(1, 1);
        static SemaphoreSlim RequestCurrentWinnersLock = new SemaphoreSlim(1, 1);
        static SemaphoreSlim NotifyExplorerLock = new SemaphoreSlim(1, 1);
        static SemaphoreSlim HealthCheckLock = new SemaphoreSlim(1, 1);
        public static bool IsRunning { get; private set; }

        public ValidatorProcessor(IHubContext<P2PValidatorServer> hubContext, IHostApplicationLifetime appLifetime)
        {
            _hubContext = hubContext;
            HubContext = hubContext;
            _appLifetime = appLifetime;
        }
        public Task StartAsync(CancellationToken stoppingToken)
        {
            //TODO: Create NetworkValidator Broadcast loop.
            _ = CheckNetworkValidators();
            _ = BroadcastNetworkValidators();
            _ = BlockHeightCheckLoopForVals();
            _ = GenerateProofs();
            _ = RequestFinalizedWinners();
            _ = SendCurrentWinners();
            _ = RequestCurrentWinners();
            _ = LockWinner();
            _ = BlockStart();
            _ = ProduceBlock();
            _ = ConfirmBlock();
            _ = NotifyExplorer();
            _ = HealthCheck();

            return Task.CompletedTask;
        }
        public static async Task ProcessData(string message, string data, string ipAddress)
        {
            if (string.IsNullOrEmpty(message))
                return;
            
            switch(message)
            {
                case "1":
                    _ = IpMessage(data);
                    break;
                case "2":
                    _ = ValMessage(data);
                    break;
                case "3":
                    _ = NetworkValMessage(data);
                    break;
                case "4":
                    _ = ProofsMessage(data);
                    break;
                case "5":
                    _ = WinningProofsMessage(data);
                    break;
                case "6":
                    _ = ReceiveQueueBlock(data);
                    break;
                case "7":
                    _ = ReceiveConfirmedBlock(data);
                    break;
                case "7777":
                    _ = TxMessage(data);
                    break;
                case "9999": 
                    _ = FailedToConnect(data);
                    break;
            }
            
                                
        }

        #region Start Blocks
        public static async Task BlockStart()
        {
            while(true)
            {
                try
                {
                    if (Globals.V4Height == (Globals.LastBlock.Height + 1))
                    {
                        var valNodeList = Globals.ValidatorNodes.Values.Where(x => x.IsConnected).ToList();

                        if (valNodeList.Count() == 0)
                        {
                            await Task.Delay(new TimeSpan(0, 0, 10));
                            continue;
                        }

                        await Task.Delay(30000);
                        await P2PValidatorClient.RequestFinalizedWinners();

                        await P2PValidatorClient.SendCurrentWinners();
                        await Task.Delay(5000);
                        await P2PValidatorClient.RequestCurrentWinners();
                        await Task.Delay(5000);
                        await P2PValidatorClient.SendCurrentWinners();
                        await Task.Delay(5000);
                        await P2PValidatorClient.RequestCurrentWinners();
                        await Task.Delay(5000);
                        await P2PValidatorClient.SendCurrentWinners();
                        await Task.Delay(5000);
                        await P2PValidatorClient.RequestCurrentWinners();
                        await Task.Delay(5000);

                        break;
                    }
                    else
                    {
                        var valNodeList = Globals.ValidatorNodes.Values.Where(x => x.IsConnected).ToList();

                        if (valNodeList.Count() == 0)
                        {
                            await Task.Delay(new TimeSpan(0, 0, 10));
                            continue;
                        }

                        //Get Current winners so we can start validating.
                        //Compare against our own proofs
                        await P2PValidatorClient.RequestCurrentWinners();
                        await Task.Delay(5000);
                        await P2PValidatorClient.RequestCurrentWinners();
                        await Task.Delay(5000);
                        await P2PValidatorClient.RequestCurrentWinners();
                        await Task.Delay(5000);

                        break;
                    }
                }
                catch
                {

                }
            }
            
        }
        #endregion

        #region Messages
        //9999
        public static async Task FailedToConnect(string data)
        {
            
        }

        //7
        public static async Task ReceiveConfirmedBlock(string data)
        {
            if (string.IsNullOrEmpty(data)) return;

            var nextBlock = JsonConvert.DeserializeObject<Block>(data);

            if (nextBlock == null) return;

            await Task.Delay(2000);//wait as we might already be processing the block.

            var lastBlock = Globals.LastBlock;
            if (lastBlock.Height < nextBlock.Height)
            {
                var result = await BlockValidatorService.ValidateBlock(nextBlock, true, false, false, true);
                if (result)
                {
                    if (nextBlock.Height + 5 > Globals.LastBlock.Height)
                    {
                        _ = P2PValidatorClient.BroadcastBlock(nextBlock, false);
                    }
                    
                }
            }
        }

        //6
        public static async Task ReceiveQueueBlock(string data)
        {
            if (string.IsNullOrEmpty(data)) return;

            var nextBlock = JsonConvert.DeserializeObject<Block>(data);

            if(nextBlock == null ) return;

            if(!Globals.NetworkBlockQueue.ContainsKey(nextBlock.Height - 1))
                await P2PValidatorClient.RequestQueuedBlock(nextBlock.Height - 1);

            var lastBlock = Globals.LastBlock;
            if (lastBlock.Height < nextBlock.Height)
            {
                var result = await BlockValidatorService.ValidateBlock(nextBlock, false, false, true);
                if (result)
                {
                    var blockAdded = Globals.NetworkBlockQueue.TryAdd(nextBlock.Height, nextBlock);

                    if(blockAdded)
                    {
                        if (!Globals.BlockQueueBroadcasted.TryGetValue(nextBlock.Height, out var lastBroadcast))
                        {
                            Globals.BlockQueueBroadcasted.TryAdd(nextBlock.Height, DateTime.UtcNow);

                            _ = P2PValidatorClient.BroadcastBlock(nextBlock, true);
                        }
                        else
                        {
                            if (DateTime.UtcNow.AddSeconds(30) > lastBroadcast)
                            {
                                Globals.BlockQueueBroadcasted[nextBlock.Height] = DateTime.UtcNow;
                                _ = P2PValidatorClient.BroadcastBlock(nextBlock, true);
                            }
                        }
                    }
                }
            }
        }


        //5
        public static async Task WinningProofsMessage(string data)
        {
            if (string.IsNullOrEmpty(data)) return;

            var proofList = JsonConvert.DeserializeObject<List<Proof>>(data);

            if (proofList?.Count() == 0) return;

            if (proofList ==  null) return;

            await ProofUtility.SortProofs(proofList, true);
        }

        //4
        public static async Task ProofsMessage(string data)
        {
            if(string.IsNullOrEmpty(data)) return;

            var proofList = JsonConvert.DeserializeObject<List<Proof>>(data);

            if(proofList?.Count() == 0) return;

            await ProofUtility.SortProofs(proofList);

            var address = proofList.GroupBy(x => x.Address).OrderByDescending(x => x.Count()).FirstOrDefault();
            if(address != null)
            {
                if (Globals.ProofsBroadcasted.TryGetValue(address.Key, out var date))
                {
                    if(date < DateTime.UtcNow)
                    {
                        //broadcast
                        Globals.ProofsBroadcasted[address.Key] = DateTime.UtcNow.AddMinutes(20);
                        await Broadcast("4", data, "SendProofList");
                    }
                }
                else
                {
                    Globals.ProofsBroadcasted.TryAdd(address.Key, DateTime.UtcNow.AddMinutes(20));
                    //broadcast
                    await Broadcast("4", data, "SendProofList");
                }
            }
        }

        //3
        public static async Task NetworkValMessage(string data)
        {
            if (!string.IsNullOrEmpty(data))
            {
                var networkValList = JsonConvert.DeserializeObject<List<NetworkValidator>>(data);
                if (networkValList?.Count > 0)
                {
                    foreach (var networkValidator in networkValList)
                    {
                        if (Globals.NetworkValidators.TryGetValue(networkValidator.Address, out var networkValidatorVal))
                        {
                            var verifySig = SignatureService.VerifySignature(
                                networkValidator.Address,
                                networkValidator.SignatureMessage,
                                networkValidator.Signature);

                            //if(networkValidatorVal.PublicKey != networkValidator.PublicKey)

                            if (verifySig && networkValidator.Signature.Contains(networkValidator.PublicKey))
                                Globals.NetworkValidators[networkValidator.Address] = networkValidator;

                        }
                        else
                        {
                            Globals.NetworkValidators.TryAdd(networkValidator.Address, networkValidator);
                        }
                    }
                }
            }
        }

        //2
        private static async Task ValMessage(string data)
        {
            try
            {
                var netVal = JsonConvert.DeserializeObject<NetworkValidator>(data);
                if (netVal == null)
                    return;

                if (Globals.NetworkValidators.TryGetValue(netVal.Address, out var networkVal))
                {
                    if (networkVal != null)
                    {
                        Globals.NetworkValidators[networkVal.Address] = netVal;
                    }
                }
                else
                {
                    Globals.NetworkValidators.TryAdd(netVal.Address, netVal);
                }
            }
            catch (Exception ex)
            {

            }
        }

        //1
        private static async Task IpMessage(string data)
        {
            var IP = data.ToString();
            if (Globals.ReportedIPs.TryGetValue(IP, out int Occurrences))
                Globals.ReportedIPs[IP]++;
            else
                Globals.ReportedIPs[IP] = 1;
        }

        //7777
        private static async Task TxMessage(string data)
        {
            var transaction = JsonConvert.DeserializeObject<Transaction>(data);
            if (transaction != null)
            {
                var isTxStale = await TransactionData.IsTxTimestampStale(transaction);
                if (!isTxStale)
                {
                    var mempool = TransactionData.GetPool();

                    if (mempool.Count() != 0)
                    {
                        var txFound = mempool.FindOne(x => x.Hash == transaction.Hash);
                        if (txFound == null)
                        {
                            var txResult = await TransactionValidatorService.VerifyTX(transaction);
                            if (txResult.Item1 == true)
                            {
                                var dblspndChk = await TransactionData.DoubleSpendReplayCheck(transaction);
                                var isCraftedIntoBlock = await TransactionData.HasTxBeenCraftedIntoBlock(transaction);
                                var rating = await TransactionRatingService.GetTransactionRating(transaction);
                                transaction.TransactionRating = rating;

                                if (dblspndChk == false && isCraftedIntoBlock == false && rating != TransactionRating.F)
                                {
                                    mempool.InsertSafe(transaction);
                                    _ = Broadcast("7777", data, "SendTxToMempoolVals");

                                }
                            }

                        }
                        else
                        {
                            //TODO Add this to also check in-mem blocks
                            var isCraftedIntoBlock = await TransactionData.HasTxBeenCraftedIntoBlock(transaction);
                            if (isCraftedIntoBlock)
                            {
                                try
                                {
                                    mempool.DeleteManySafe(x => x.Hash == transaction.Hash);// tx has been crafted into block. Remove.
                                }
                                catch (Exception ex)
                                {
                                    //delete failed
                                }
                            }
                        }
                    }
                    else
                    {
                        var txResult = await TransactionValidatorService.VerifyTX(transaction);
                        if (txResult.Item1 == true)
                        {
                            var dblspndChk = await TransactionData.DoubleSpendReplayCheck(transaction);
                            var isCraftedIntoBlock = await TransactionData.HasTxBeenCraftedIntoBlock(transaction);
                            var rating = await TransactionRatingService.GetTransactionRating(transaction);
                            transaction.TransactionRating = rating;

                            if (dblspndChk == false && isCraftedIntoBlock == false && rating != TransactionRating.F)
                            {
                                mempool.InsertSafe(transaction);
                            }
                        }
                    }
                }

            }
        }

        #endregion

        #region Broadcast

        public static async Task Broadcast(string messageType, string data, string method = "")
        {
            await HubContext.Clients.All.SendAsync("GetValMessage", messageType, data);

            if (method == "") return;

            if (!Globals.ValidatorNodes.Any()) return;

            var valNodeList = Globals.ValidatorNodes.Values.Where(x => x.IsConnected).ToList();

            if(valNodeList == null || valNodeList.Count() == 0) return;

            foreach (var val in valNodeList)
            {
                var source = new CancellationTokenSource(2000);
                await val.Connection.InvokeCoreAsync(method, args: new object?[] { data }, source.Token);
            }
        }

        #endregion

        #region Services

        private async Task ProduceBlock()
        {
            while(true)
            {
                var delay = Task.Delay(new TimeSpan(0, 0, 2));
                if (Globals.StopAllTimers && !Globals.IsChainSynced)
                {
                    await delay;
                    continue;
                }
                await ProduceBlockLock.WaitAsync();
                try
                {
                    //TODO- CANT DO THIS. Must have previous block, so items must be queued fast.
                    for(int i = 1; i <= 5; i++)
                    {
                        var nextblock = Globals.LastBlock.Height + i;
                        if(Globals.FinalizedWinner.TryGetValue(nextblock, out var winner)) 
                        {
                            if(!Globals.NetworkBlockQueue.TryGetValue(nextblock, out _))
                            {
                                if (winner == Globals.ValidatorAddress)
                                {
                                    if (Globals.LastBlock.Height + 1 == nextblock)
                                    {
                                        Globals.WinningProofs.TryGetValue(nextblock, out var proof);
                                        if (proof != null)
                                        {
                                            await Task.Delay(2000); //await is here to ensure timestamps are greater.
                                            var block = await BlockchainData.CraftBlock_V5(
                                                Globals.ValidatorAddress,
                                                Globals.NetworkValidators.Count(),
                                                proof.ProofHash, nextblock);

                                            if (block != null)
                                            {
                                                Globals.NetworkBlockQueue.TryAdd(nextblock, block);
                                                var blockJson = JsonConvert.SerializeObject(block);

                                                await P2PValidatorClient.BroadcastBlock(block, true);

                                                await _hubContext.Clients.All.SendAsync("GetValMessage", "6", blockJson);

                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (!Globals.NetworkBlockQueue.TryGetValue(nextblock, out var networkBlock))
                                        {
                                            if (Globals.NetworkBlockQueue.TryGetValue((nextblock - 1), out _))
                                            {
                                                Globals.WinningProofs.TryGetValue(nextblock, out var proof);
                                                if (proof != null)
                                                {
                                                    if (proof.Address == Globals.ValidatorAddress)
                                                    {
                                                        await Task.Delay(2000); //await is here to ensure timestamps are greater.
                                                        var block = await BlockchainData.CraftBlock_V5(
                                                        Globals.ValidatorAddress,
                                                        Globals.NetworkValidators.Count(),
                                                        proof.ProofHash, nextblock);

                                                        if (block != null)
                                                        {
                                                            Globals.NetworkBlockQueue.TryAdd(nextblock, block);
                                                            var blockJson = JsonConvert.SerializeObject(block);

                                                            await P2PValidatorClient.BroadcastBlock(block, true);

                                                            await _hubContext.Clients.All.SendAsync("GetValMessage", "6", blockJson);
                                                        }
                                                    }

                                                }
                                            }
                                        }
                                    }

                                }
                                else
                                {
                                    if (!Globals.NetworkBlockQueue.TryGetValue(nextblock, out _))
                                        await P2PValidatorClient.RequestQueuedBlock(nextblock);
                                }
                            }
                            else
                            {
                                if (!Globals.NetworkBlockQueue.TryGetValue(nextblock, out _))
                                    await P2PValidatorClient.RequestQueuedBlock(nextblock);
                            }
                        }
                        else
                        {
                            //no winner found.
                            //Request winner list
                        }
                    }
                }
                catch (Exception ex)
                {

                }
                finally
                {
                    ProduceBlockLock.Release();
                    await delay;
                }
            }
        }

        private async Task LockWinner()
        {
            while (true)
            {
                var delay = Task.Delay(new TimeSpan(0, 0, 5));
                if (Globals.StopAllTimers && !Globals.IsChainSynced)
                {
                    await delay;
                    continue;
                }
                var proofCount = Globals.WinningProofs.Values.GroupBy(x => x.Address).Count();

                var minProofCount = 2;

                if (proofCount < minProofCount)
                {
                    await delay;
                    continue;
                }

                await LockWinnerLock.WaitAsync();

                try
                {
                    var nextBlock = Globals.LastBlock.Height + 1;

                    for(var i = nextBlock; i <= nextBlock + 2; i++)
                    {
                        if (!Globals.FinalizedWinner.TryGetValue(i, out var winner))
                        {
                            if (Globals.WinningProofs.TryGetValue(i, out var winningProof))
                            {
                                if (winningProof != null)
                                {
                                    if (ProofUtility.VerifyProof(winningProof.PublicKey, winningProof.BlockHeight, winningProof.ProofHash))
                                    {
                                        Globals.FinalizedWinner.TryAdd(i, winningProof.Address);
                                    }
                                }
                            }
                            else
                            {
                                //if missing must request winner from connected nodes
                            }
                        }
                    }                
                }
                catch (Exception ex)
                {

                }
                finally
                {
                    LockWinnerLock.Release();
                    await delay;
                }
            }
        }

        private async Task RequestCurrentWinners()
        {
            while(true)
            {
                var delay = Task.Delay(new TimeSpan(0, 0, 25));
                if (Globals.StopAllTimers && !Globals.IsChainSynced)
                {
                    await Task.Delay(new TimeSpan(0, 0, 20));
                    continue;
                }
                

                await RequestCurrentWinnersLock.WaitAsync();

                try
                {
                    await P2PValidatorClient.RequestCurrentWinners();
                }
                catch { }
                finally { RequestCurrentWinnersLock.Release(); await delay; }
            }
        }

        private async Task RequestFinalizedWinners()
        {
            

            if (Globals.StopAllTimers && !Globals.IsChainSynced)
            {
                await Task.Delay(new TimeSpan(0, 0, 20));
                return;
            }

            await RequestCurrentWinnersLock.WaitAsync();

            try
            {
                await P2PValidatorClient.RequestFinalizedWinners();
            }
            catch { }
            finally { RequestCurrentWinnersLock.Release(); }
            
        }

        private async Task SendCurrentWinners()
        {
            while (true)
            {
                var delay = Task.Delay(new TimeSpan(0, 0, 10));
                if (Globals.StopAllTimers && !Globals.IsChainSynced)
                {
                    await delay;
                    continue;
                }
                   
                await SendWinningVoteLock.WaitAsync();
                try
                {
                    var proofsJson = await P2PValidatorClient.SendCurrentWinners();
                    if(proofsJson != "0")
                        await _hubContext.Clients.All.SendAsync("GetValMessage", "5", proofsJson);
                }
                catch (Exception ex)
                {

                }
                finally
                {
                    SendWinningVoteLock.Release();
                    await delay;
                }
            }
        }

        

        private async Task ConfirmBlock()
        {
            while (true)
            {
                var delay = Task.Delay(new TimeSpan(0, 0, 2));
                if (Globals.StopAllTimers && !Globals.IsChainSynced)
                {
                    await delay;
                    continue;
                }

                if(!Globals.NetworkBlockQueue.Any())
                {
                    await delay;
                    continue;
                }

                await ConfirmBlockLock.WaitAsync();
                try
                {
                    var nextBlock = Globals.LastBlock.Height + 1;
                    var currentTime = TimeUtil.GetTime();
                    var currentDiff = (currentTime - Globals.LastBlockAddedTimestamp);
                    if (currentDiff >= 25)
                    {
                        if(Globals.NetworkBlockQueue.TryGetValue(nextBlock, out var block))
                        {
                            //add block and broadcast
                            var result = await BlockValidatorService.ValidateBlock(block, true, false, false, true);

                            if(result)
                            {
                                var blockJson = JsonConvert.SerializeObject(block);

                                _ = P2PValidatorClient.BroadcastBlock(block, false);

                                _ = _hubContext.Clients.All.SendAsync("GetValMessage", "7", blockJson);

                                
                            }
                        }
                        else
                        {
                            //request block
                            await P2PValidatorClient.RequestQueuedBlock(nextBlock);
                        }
                    }
                }
                catch { }
                finally
                {
                    ConfirmBlockLock.Release();
                    await delay;
                }
            }
        }

        private async Task GenerateProofs()
        {
            while(true)
            {
                var delay = Task.Delay(new TimeSpan(0, 0, 30));
                if (Globals.StopAllTimers && !Globals.IsChainSynced)
                {
                    await delay;
                    continue;
                }

                if (!Globals.ValidatorNodes.Any())
                {
                    await delay;
                    continue;
                }
                await GenerateProofLock.WaitAsync();
                try
                {
                    var account = AccountData.GetLocalValidator();
                    var validators = Validators.Validator.GetAll();
                    var validator = validators.FindOne(x => x.Address == account.Address);
                    if (validator == null)
                    {
                        await delay;
                        continue;
                    }

                    var valNodeList = Globals.ValidatorNodes.Values.Where(x => x.IsConnected).ToList();

                    if (valNodeList.Count() == 0)
                        continue;

                    if (Globals.LastProofBlockheight == 0)
                    {
                        var firstProof = Globals.LastBlock.Height == (Globals.V4Height - 1) ? false : true;
                        firstProof = Globals.IsTestNet ? false : true;

                        var proofs = await ProofUtility.GenerateProofs(Globals.ValidatorAddress, account.PublicKey, Globals.LastBlock.Height, firstProof);
                        await ProofUtility.SortProofs(proofs);
                        //send proofs
                        var proofsJson = JsonConvert.SerializeObject(proofs);
                        await _hubContext.Clients.All.SendAsync("GetValMessage", "4", proofsJson);

                        foreach (var val in valNodeList)
                        {
                            var source = new CancellationTokenSource(2000);
                            await val.Connection.InvokeCoreAsync("SendProofList", args: new object?[] { proofsJson }, source.Token);
                        }
                    }
                    else
                    {
                        var firstProof = Globals.IsTestNet ? false : true;

                        if (Globals.LastBlock.Height + 72 >= Globals.LastProofBlockheight)
                        {
                            var proofs = await ProofUtility.GenerateProofs(Globals.ValidatorAddress, account.PublicKey, Globals.LastProofBlockheight, firstProof);
                            await ProofUtility.SortProofs(proofs);
                            //send proofs
                            var proofsJson = JsonConvert.SerializeObject(proofs);
                            await _hubContext.Clients.All.SendAsync("GetValMessage", "4", proofsJson);

                            foreach (var val in valNodeList)
                            {
                                var source = new CancellationTokenSource(2000);
                                await val.Connection.InvokeCoreAsync("SendProofList", args: new object?[] { proofsJson }, source.Token);
                            }
                        }
                    }
                }
                catch(Exception ex)
                {
                    if(true)
                    {
                        //log
                    }
                }
                finally
                {
                    GenerateProofLock.Release();
                    await delay;
                }
                
            }
        }

        private async Task CheckNetworkValidators()
        {
            while(true)
            {
                var delay = Task.Delay(new TimeSpan(0, 0, 30));
                if (Globals.StopAllTimers && !Globals.IsChainSynced)
                {
                    await delay;
                    continue;
                }

                await CheckNetworkValidatorsLock.WaitAsync();

                try
                {
                    if (Globals.NetworkValidators.Count == 0)
                        continue;

                    foreach(var validator in Globals.NetworkValidators)
                    {
                        var portOpen = PortUtility.IsPortOpen(validator.Value.IPAddress, Globals.ValPort);
                        if(!portOpen)
                        {
                            //if port is not open remove them from pool
                            Globals.NetworkValidators.Remove(validator.Key, out _);
                        }
                    }
                }
                catch (Exception ex)
                {

                }
                finally { CheckNetworkValidatorsLock.Release(); await delay; }
            }
        }

        private async Task BroadcastNetworkValidators()
        {
            while (true)
            {
                var delay = Task.Delay(new TimeSpan(0,5,0));
                if (Globals.StopAllTimers && !Globals.IsChainSynced)
                {
                    await delay;
                    continue;
                }
                await BroadcastNetworkValidatorLock.WaitAsync();
                try
                {
                    if (Globals.NetworkValidators.Count == 0)
                        continue;

                    var networkValsJson = JsonConvert.SerializeObject(Globals.NetworkValidators.Values.ToList());

                    await _hubContext.Clients.All.SendAsync("GetValMessage", "3", networkValsJson);

                    if (Globals.ValidatorNodes.Count == 0)
                        continue;

                    var valNodeList = Globals.ValidatorNodes.Values.Where(x => x.IsConnected).ToList();

                    foreach (var val in valNodeList)
                    {
                        var source = new CancellationTokenSource(2000);
                        await val.Connection.InvokeCoreAsync("SendNetworkValidatorList", args: new object?[] { networkValsJson }, source.Token);
                    }
                }
                catch (Exception ex)
                {

                }
                finally
                {
                    BroadcastNetworkValidatorLock.Release();
                    await delay;
                }
            }
        }

        private static async Task BlockHeightCheckLoopForVals()
        {
            bool dupMessageShown = false;

            while (true)
            {
                try
                {
                    while (!Globals.ValidatorNodes.Any())
                        await Task.Delay(20);

                    await P2PValidatorClient.UpdateNodeHeights();

                    var maxHeight = Globals.ValidatorNodes.Values.Select(x => x.NodeHeight).OrderByDescending(x => x).FirstOrDefault();
                    if (maxHeight > Globals.LastBlock.Height)
                    {
                        P2PValidatorClient.UpdateMaxHeight(maxHeight);
                        //TODO: Update this method for getting block sync
                        _ = BlockDownloadService.GetAllBlocks();
                    }
                    else
                        P2PValidatorClient.UpdateMaxHeight(maxHeight);

                    var MaxHeight = P2PValidatorClient.MaxHeight();
                    foreach (var node in Globals.ValidatorNodes.Values)
                    {
                        if (node.NodeHeight < MaxHeight - 3)
                            await P2PValidatorClient.RemoveNode(node);
                    }

                }
                catch { }

                await Task.Delay(10000);
            }
        }

        #endregion

        #region Notify Explorer Status
        public static async Task NotifyExplorer()
        {
            while(true && !string.IsNullOrEmpty(Globals.ValidatorAddress))
            {
                var delay = Task.Delay(new TimeSpan(0, 1, 0));
                try
                {
                    if (Globals.StopAllTimers && !Globals.IsChainSynced)
                    {
                        await delay;
                        continue;
                    }
                    await NotifyExplorerLock.WaitAsync();


                    var account = AccountData.GetLocalValidator();
                    if (account == null)
                        return;

                    var validator = Validators.Validator.GetAll().FindOne(x => x.Address == account.Address);
                    if (validator == null)
                        return;

                    var fortis = new FortisPool
                    {
                        Address = Globals.ValidatorAddress,
                        ConnectDate = Globals.ValidatorStartDate,
                        IpAddress = P2PClient.MostLikelyIP(),
                        LastAnswerSendDate = DateTime.UtcNow,
                        UniqueName = validator.UniqueName,
                        WalletVersion = validator.WalletVersion
                    };

                    List<FortisPool> fortisPool = new List<FortisPool> { fortis };

                    var listFortisPool = fortisPool.Select(x => new
                    {
                        ConnectionId = "NA",
                        x.ConnectDate,
                        x.LastAnswerSendDate,
                        x.IpAddress,
                        x.Address,
                        x.UniqueName,
                        x.WalletVersion
                    }).ToList();

                    var fortisPoolStr = JsonConvert.SerializeObject(listFortisPool);

                    using (var client = Globals.HttpClientFactory.CreateClient())
                    {
                        string endpoint = Globals.IsTestNet ? "https://testnet-data.rbx.network/api/masternodes/send/" : "https://data.rbx.network/api/masternodes/send/";
                        var httpContent = new StringContent(fortisPoolStr, Encoding.UTF8, "application/json");
                        using (var Response = await client.PostAsync(endpoint, httpContent))
                        {
                            if (Response.StatusCode == System.Net.HttpStatusCode.OK)
                            {
                                //success
                                Globals.ExplorerValDataLastSend = DateTime.Now;
                                Globals.ExplorerValDataLastSendSuccess = true;
                            }
                            else
                            {
                                //ErrorLogUtility.LogError($"Error sending payload to explorer. Response Code: {Response.StatusCode}. Reason: {Response.ReasonPhrase}", "ClientCallService.DoFortisPoolWork()");
                                Globals.ExplorerValDataLastSendSuccess = false;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogUtility.LogError($"Failed to send validator list to explorer API. Error: {ex.ToString()}", "ValidatorService.NotifyExplorer()");
                    Globals.ExplorerValDataLastSendSuccess = false;
                }
                finally
                {
                    NotifyExplorerLock.Release();
                }
                await delay;
            }
            
        }

        #endregion

        #region Validator Health Check
        public static async Task HealthCheck()
        {
            int portCheckCount = 0;
            Dictionary<string, int> ErrorCountDict = new Dictionary<string, int>();
            while (true && !string.IsNullOrEmpty(Globals.ValidatorAddress))
            {
                var delay = Task.Delay(new TimeSpan(0, 1, 0));
                try
                {
                    if (Globals.StopAllTimers && !Globals.IsChainSynced)
                    {
                        await delay;
                        continue;
                    }
                    await HealthCheckLock.WaitAsync();

                    if (Globals.ValidatorNodes.Count() == 0)
                    {
                        Globals.ValidatorIssueCount += 1;
                        Globals.ValidatorErrorMessages.Add($"Validator Nodes are not connected.");
                    }

                    if (Globals.NetworkValidators.Count() == 0)
                    {
                        Globals.ValidatorIssueCount += 1;
                        Globals.ValidatorErrorMessages.Add($"Network Nodes are not connected.");
                    }  

                    if(Globals.NetworkBlockQueue.Count() == 0)
                    {
                        Globals.ValidatorIssueCount += 1;
                        Globals.ValidatorErrorMessages.Add($"Network Blocks were not found.");
                    }

                    var valAccount = AccountData.GetSingleAccount(Globals.ValidatorAddress);
                    if (valAccount != null)
                    {
                        if (valAccount.Balance < ValidatorService.ValidatorRequiredAmount())
                        {
                            Globals.ValidatorIssueCount += 1;
                            Globals.ValidatorErrorMessages.Add($"Time: {DateTime.Now} Balance Error. Please ensure you have proper amount.");
                            Globals.ValidatorBalanceGood = false;
                            await ValidatorService.DoMasterNodeStop();
                        }
                        else
                        {
                            Globals.ValidatorBalanceGood = true;
                        }
                    }
                    else
                    {
                        Globals.ValidatorIssueCount += 1;
                        Globals.ValidatorErrorMessages.Add($"Time: {DateTime.Now} Validator Account Missing");
                    }

                    if (Globals.TimeSyncError)
                    {
                        Globals.ValidatorIssueCount += 1;
                        Globals.ValidatorErrorMessages.Add($"Time: {DateTime.Now} Node system time is out of sync. Please correct.");
                    }

                    if(Globals.ReportedIPs.Count() != 0)
                    {
                        var ip = Globals.ReportedIPs.OrderByDescending(y => y.Value).Select(y => y.Key).First();
                        var portCheck = PortUtility.IsPortOpen(ip, Globals.ValPort);
                        if(!portCheck)
                        {
                            portCheckCount += 1;
                            if(portCheckCount >= 4)
                            {
                                await ValidatorService.DoMasterNodeStop();
                                ConsoleWriterService.OutputMarked($"[red]Validator Ports were found to not be open. Please ensure ports: {Globals.ValPort} & {Globals.Port} are open.[/]");
                                ErrorLogUtility.LogError($"Validator Ports were found to not be open. Please ensure ports: {Globals.ValPort} & {Globals.Port} are open.", "ValidatorProcessor.HealthCheck()");
                            }
                        }

                    }
                    

                    if (Globals.ValidatorIssueCount >= 15)
                    {
                        ConsoleWriterService.OutputMarked("[red]Validator has had the following issues to report. Please ensure node is operating correctly[/]");
                        foreach (var issue in Globals.ValidatorErrorMessages)
                        {
                            ConsoleWriterService.OutputMarked($"[yellow]{issue}[/]");
                        }

                        Globals.ValidatorIssueCount = 0;
                    }
                }
                catch (Exception ex)
                {

                }
                finally 
                {
                    HealthCheckLock.Release();
                }

                await delay;
            }
        }

        #endregion

        #region Deprecate

        public static async void RandomNumberTaskV3(long blockHeight)
        {
            if (string.IsNullOrWhiteSpace(Globals.ValidatorAddress))
                return;

            while (Globals.LastBlock.Height + 1 != blockHeight)
            {                
                await BlockDownloadService.GetAllBlocks();
            }

            if (TimeUtil.GetTime() - Globals.CurrentTaskNumberAnswerV3.Time < 4)
            {
                return;
            }

            if (Globals.CurrentTaskNumberAnswerV3.Height != blockHeight)
            {
                var num = TaskQuestionUtility.GenerateRandomNumber(blockHeight);                                
                Globals.CurrentTaskNumberAnswerV3 = (blockHeight, num, TimeUtil.GetTime());
            }

            //await P2PClient.SendTaskAnswerV3(Globals.CurrentTaskNumberAnswerV3.Answer + ":" + Globals.CurrentTaskNumberAnswerV3.Height);
        }

        #endregion

        #region Stop/Dispose
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {

        }

        #endregion
    }
}
