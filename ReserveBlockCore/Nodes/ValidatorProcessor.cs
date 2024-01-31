using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.DST;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System;
using System.Reflection.Metadata.Ecma335;
using System.Xml.Linq;

namespace ReserveBlockCore.Nodes
{
    public class ValidatorProcessor : IHostedService, IDisposable
    {
        public static IHubContext<P2PValidatorServer> HubContext;
        private readonly IHubContext<P2PValidatorServer> _hubContext;
        private readonly IHostApplicationLifetime _appLifetime;
        static SemaphoreSlim BroadcastNetworkValidatorLock = new SemaphoreSlim(1, 1);

        public ValidatorProcessor(IHubContext<P2PValidatorServer> hubContext, IHostApplicationLifetime appLifetime)
        {
            _hubContext = hubContext;
            HubContext = hubContext;
            _appLifetime = appLifetime;
        }
        public Task StartAsync(CancellationToken stoppingToken)
        {
            //TODO: Create NetworkValidator Broadcast loop.
            _ = BroadcastNetworkValidators();
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
                    break;
                case "9999":
                    break;
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

                    await _hubContext.Clients.All.SendAsync("3", networkValsJson);

                    if (Globals.ValidatorNodes.Count == 0)
                        continue;

                    var valNodeList = Globals.ValidatorNodes.Values.Where(x => x.IsConnected).ToList();

                    foreach (var val in valNodeList)
                    {
                        var source = new CancellationTokenSource(2000);
                        await val.Connection.InvokeCoreAsync("SendNetworkValidatorList", args: new object?[] { networkValsJson }, source.Token);
                    }
                }
                finally
                {
                    BroadcastNetworkValidatorLock.Release();
                    await delay;
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            
        }
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
    }
}
