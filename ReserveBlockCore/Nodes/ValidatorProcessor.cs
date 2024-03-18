using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
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
        static SemaphoreSlim BlockCheckLock = new SemaphoreSlim(1, 1);
        static SemaphoreSlim SendWinningVoteLock = new SemaphoreSlim(1, 1);
        static SemaphoreSlim LockWinnerLock = new SemaphoreSlim(1, 1);
        static SemaphoreSlim RequestCurrentWinnersLock = new SemaphoreSlim(1, 1);
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
            _ = SendCurrentWinners();
            _ = RequestCurrentWinners();
            _ = LockWinner();
            _ = BlockStart();
            _ = ProduceBlock();

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
                case "9999":
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

                         var finalBlock = Globals.LastBlock.Height + 30;

                        for(var i = 1; i <= finalBlock; i++)

                        if (!Globals.FinalizedWinner.TryGetValue(i, out var winner))
                        {
                            if (Globals.WinningProofs.TryGetValue(i, out var winningProof))
                            {
                                if (winningProof != null)
                                {
                                    if (ProofUtility.VerifyProofSync(winningProof.PublicKey, winningProof.BlockHeight, winningProof.ProofHash))
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
        //6
        public static async Task ReceiveQueueBlock(string data)
        {
            if (string.IsNullOrEmpty(data)) return;

            var nextBlock = JsonConvert.DeserializeObject<Block>(data);

            if(nextBlock == null ) return;

            var result = await BlockValidatorService.ValidateBlock(nextBlock, false, false, true);
            if (result)
            {
                Globals.NetworkBlockQueue.TryAdd(nextBlock.Height, nextBlock);

                var blockJson = JsonConvert.SerializeObject(nextBlock);

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

        private static async Task TxMessage(string data)
        {
            var transaction = JsonConvert.DeserializeObject<Models.Transaction>(data);
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

        private static async Task Broadcast(string messageType, string data, string method = "")
        {
            await HubContext.Clients.All.SendAsync("GetValMessage", messageType, data);

            if (method == "") return;

            if (!Globals.ValidatorNodes.Any()) return;

            var valNodeList = Globals.ValidatorNodes.Values.Where(x => x.IsConnected).ToList();

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
                    for(int i = 1; i < 5; i++)
                    {
                        var nextblock = Globals.LastBlock.Height + i;
                        if(Globals.FinalizedWinner.TryGetValue(nextblock, out var winner)) 
                        {
                            if(winner == Globals.ValidatorAddress)
                            {
                                if(Globals.LastBlock.Height + 1 == nextblock)
                                {
                                    Globals.WinningProofs.TryGetValue(nextblock, out var proof);
                                    if (proof != null)
                                    {
                                        var block = await BlockchainData.CraftBlock_V4(
                                            Globals.ValidatorAddress,
                                            Globals.NetworkValidators.Count(),
                                            proof.ProofHash);

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
                                    if(Globals.NetworkBlockQueue.TryGetValue(nextblock, out var networkBlock))
                                    {
                                        Globals.WinningProofs.TryGetValue(nextblock, out var proof);
                                        if (proof != null)
                                        {
                                            var block = await BlockchainData.CraftBlock_V4(
                                                Globals.ValidatorAddress,
                                                Globals.NetworkValidators.Count(),
                                                proof.ProofHash);

                                            if (block != null)
                                            {
                                                Globals.NetworkBlockQueue.TryAdd(nextblock, block);
                                            }
                                        }
                                    }
                                }
                                
                            }
                            else
                            {
                                //request block
                                //add to here --v--
                                //Globals.NetworkBlockQueue.TryAdd();
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
            while(true)
            {
                var delay = Task.Delay(new TimeSpan(0, 0, 5));
                if (Globals.StopAllTimers && !Globals.IsChainSynced)
                {
                    await delay;
                    continue;
                }
                await LockWinnerLock.WaitAsync();

                try
                {
                    var valCount = Globals.WinningProofs.Values.GroupBy(x => x.Address).Count();

                    if(valCount == 1)
                    {
                        await delay;
                        continue;
                    }

                    var nextBlock = Globals.LastBlock.Height + 30;
                    if (!Globals.FinalizedWinner.TryGetValue(nextBlock, out var winner))
                    {
                        if(Globals.WinningProofs.TryGetValue(nextBlock, out var winningProof))
                        {
                            if(winningProof != null)
                            {
                                if (ProofUtility.VerifyProofSync(winningProof.PublicKey, winningProof.BlockHeight, winningProof.ProofHash))
                                {
                                    Globals.FinalizedWinner.TryAdd(nextBlock, winningProof.Address);
                                }
                            }
                        }
                        else
                        {
                            //if missing must request winner from connected nodes
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
                var delay = Task.Delay(new TimeSpan(0, 2, 0));
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

        private async Task BlockCheck()
        {
            while (true)
            {
                var delay = Task.Delay(new TimeSpan(0, 0, 30));
                if (Globals.StopAllTimers && !Globals.IsChainSynced)
                {
                    await delay;
                    continue;
                }

                await BlockCheckLock.WaitAsync();
                try
                {
                    if(Globals.LastBlock.Timestamp + 20 <= TimeUtil.GetTime())
                    {
                        var nextBlock = Globals.LastBlock.Height + 1;
                        if(Globals.NetworkBlockQueue.TryGetValue(nextBlock, out var block))
                        {
                            //add block and broadcast
                            //do removals from proofs and other in memory variables
                        }
                        else
                        {
                            //request block
                        }
                    }
                }
                finally
                {
                    BlockCheckLock.Release();
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
                        var firstProof = Globals.LastBlock.Height == 0 ? false : true;
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
                        if (Globals.LastBlock.Height + 72 >= Globals.LastProofBlockheight)
                        {
                            var proofs = await ProofUtility.GenerateProofs(Globals.ValidatorAddress, account.PublicKey, Globals.LastProofBlockheight, false);
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
