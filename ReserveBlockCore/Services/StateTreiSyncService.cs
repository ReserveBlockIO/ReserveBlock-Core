using LiteDB;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Utilities;
using Spectre.Console;

namespace ReserveBlockCore.Services
{
    public class StateTreiSyncService
    {
        private static bool IsRunning = false;
        public static async Task SyncAccountStateTrei()
        {
            try
            {
                AnsiConsole.MarkupLine("[red]Syncing State Treis... This process may take a moment.[/]");
                AnsiConsole.MarkupLine("[yellow]This is running due to an incorrect shutdown of wallet.[/]");
                AnsiConsole.MarkupLine("[yellow]During this time please do not close wallet, or click cursor into the CLI.[/]");

                if (IsRunning == false)
                {
                    IsRunning = true;
                    var height = BlockchainData.GetHeight();
                    long currenRunHeight = 0;
                    long interval = 10000;
                    bool processBlocks = true;
                    double progress = new double();
                    double increment = ((double)interval / (double)height) * (double)100;
                    List<AccountStateTrei> blockBalances = new List<AccountStateTrei>();
                    var blockChain = BlockchainData.GetBlocks();

                    await AnsiConsole.Progress()
                    .Columns(new ProgressColumn[] {
                        new TaskDescriptionColumn(),    // Task description
                        new ProgressBarColumn(),        // Progress bar
                        new PercentageColumn(),         // Percentage
                        new RemainingTimeColumn(),      // Remaining time
                        new SpinnerColumn()             // Spinner
                        })
                    .StartAsync(async ctx =>
                    {
                        var task1 = ctx.AddTask("[purple]Running State Trei Sync[/]");
                        while (!ctx.IsFinished)
                        {
                            while (processBlocks)
                            {
                                var heightSpan = currenRunHeight + interval;
                                
                                var blocks = blockChain.Query()
                                .Where(x => x.Height >= currenRunHeight && x.Height < heightSpan)
                                .Limit((int)heightSpan - (int)currenRunHeight)
                                .ToEnumerable();

                                foreach (Block block in blocks)
                                {
                                    var txList = block.Transactions.ToList();
                                    txList.ForEach(x =>
                                    {
                                        if (block.Height == 0)
                                        {
                                            var acctStateTreiFrom = new AccountStateTrei
                                            {
                                                Key = x.FromAddress,
                                                Nonce = x.Nonce + 1, //increase Nonce for next use
                                                Balance = 0, //subtract from the address
                                                StateRoot = block.StateRoot
                                            };

                                            blockBalances.Add(acctStateTreiFrom);
                                        }
                                        else
                                        {
                                            if (x.FromAddress != "Coinbase_TrxFees" && x.FromAddress != "Coinbase_BlkRwd")
                                            {
                                                var from = blockBalances.Where(a => a.Key == x.FromAddress).FirstOrDefault();

                                                from.Nonce += 1;
                                                from.StateRoot = block.StateRoot;
                                                from.Balance -= (x.Amount + x.Fee);

                                            }
                                            else
                                            {
                                                //do nothing as its the coinbase fee
                                            }

                                        }
                                        if (x.ToAddress != "Adnr_Base" && x.ToAddress != "DecShop_Base" && x.ToAddress != "Topic_Base" && x.ToAddress != "Vote_Base")
                                        {
                                            if (x.TransactionType == TransactionType.TX)
                                            {
                                                var to = blockBalances.Where(a => a.Key == x.ToAddress).FirstOrDefault();

                                                if (to == null)
                                                {
                                                    var acctStateTreiTo = new AccountStateTrei
                                                    {
                                                        Key = x.ToAddress,
                                                        Nonce = 0,
                                                        Balance = x.Amount,
                                                        StateRoot = block.StateRoot
                                                    };

                                                    blockBalances.Add(acctStateTreiTo);
                                                }
                                                else
                                                {
                                                    to.Balance += x.Amount;
                                                    to.StateRoot = block.StateRoot;
                                                }
                                            }
                                        }
                                    });

                                    if (block.Height == height)
                                    {
                                        processBlocks = false;
                                        task1.Increment(100);
                                        progress = (double)100;
                                        var messageEnd = JsonConvert.SerializeObject(new { NextBlock = currenRunHeight.ToString(), CurrentPercent = (progress.ToString("#.##") + "%") });
                                        await StateTreiSyncLogUtility.Log(messageEnd);
                                        //break;
                                    }
                                    
                                }
                                if(processBlocks)
                                {
                                    //This is needed if ToList is used.
                                    //blocks.Clear();
                                    //blocks = new List<Block>();
                                    task1.Increment(increment);
                                    progress += increment;
                                    currenRunHeight += interval;
                                }
                                
                                var message = JsonConvert.SerializeObject(new {NextBlock = currenRunHeight.ToString(), CurrentPercent = (progress.ToString("#.##") + "%")});
                                await StateTreiSyncLogUtility.Log(message);
                            }
                        }
                    });

                    var stateTrei = StateData.GetAccountStateTrei();
                    
                    foreach (var bb in blockBalances)
                    {
                        var stateTreiRec = stateTrei.Query().Where(x => x.Key == bb.Key).FirstOrDefault();
                        if (stateTreiRec != null)
                        {
                            if (stateTreiRec.Balance != bb.Balance)
                            {
                                ErrorLogUtility.LogError(
                                    $"Balance Off: {stateTreiRec.Key} | Reported: {stateTreiRec.Balance} - Actual: {bb.Balance}",
                                    "StateTreiSyncService()");
                                stateTreiRec.Balance = bb.Balance;
                                stateTrei.UpdateSafe(stateTreiRec);
                            }
                        }
                        else
                        {
                            if (bb.Key != "rbx_genesis_transaction")
                            {
                                AccountStateTrei nAcctST = new AccountStateTrei
                                {
                                    Key = bb.Key,
                                    Nonce = 0,
                                    Balance = bb.Balance,
                                    StateRoot = bb.StateRoot

                                };

                                stateTrei.InsertSafe(nAcctST);
                            }

                        }
                    }
                    await StateTreiSyncLogUtility.DeleteLog();
                }

                Console.WriteLine("Done Syncing State Treis...");
                IsRunning = false;
            }
            catch(Exception ex)
            {
                ErrorLogUtility.LogError($"Erroring Running SyncAccountStateTrei. Error : {ex.ToString()}", "StateTreiSyncService.SyncAccountStateTrei()");
            }
        }
    }
}
