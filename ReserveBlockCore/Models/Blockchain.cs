using LiteDB;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Extensions;
using ReserveBlockCore.Utilities;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Trillium.Syntax;

namespace ReserveBlockCore.Models
{
    public class Blockchain
    {
        [BsonId]
        public long Id { get; set; }
        public string Hash { get; set; }
        public long Height { get; set; }
        public long Size { get; set; }
        public long CumulativeSize { get; set; }

        public static LiteDB.ILiteCollection<Blockchain>? GetBlockchain()
        {
            try
            {
                var blockchainDb = DbContext.DB_Blockchain.GetCollection<Blockchain>(DbContext.RSRV_BLOCKCHAIN);
                return blockchainDb;
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError(ex.ToString(), "Blockchain.GetBlockchain()");
                return null;
            }
        }

        public static Blockchain? GetLastBlockchainPoint()
        {
            var blockchain = GetBlockchain();
            if (blockchain == null)
                return null;

            var header = blockchain.FindOne(Query.All(Query.Descending));

            if (header == null)
                return null;

            return header;
        }

        public static void AddBlock(Block block)
        {
            var blockchain = GetBlockchain();
            if (blockchain != null)
            {
                var blockExist = blockchain.Find(Query.All(Query.Descending)).Take(100).Where(x => x.Height == block.Height).FirstOrDefault();
                if (blockExist == null)
                {
                    var cumulativeSize = Globals.Blockchain != null ? Globals.Blockchain.CumulativeSize : 0;
                    Blockchain rec = new Blockchain
                    {
                        Hash = block.Hash,
                        Height = block.Height,
                        Size = block.Size,
                        CumulativeSize = cumulativeSize + block.Size
                    };

                    blockchain.InsertSafe(rec); //insert latest

                    Globals.Blockchain = rec; //update global record
                }
            }
        }

        public static async Task<(long, long)?> GetBlockSpan(long height, long byteSpan)
        {
            var blockchain = GetBlockchain();

            if (blockchain == null)
                return null;
            
            var initialBlock = blockchain.Query().Where(x => x.Height == height).FirstOrDefault();
            if (initialBlock == null)
                return null;

            var maxByteLength = initialBlock.CumulativeSize + byteSpan;

            var finalBlock = blockchain.Query().Where(x => x.Height >= height && x.CumulativeSize <= maxByteLength).OrderByDescending(x => x.Height).FirstOrDefault();

            if(finalBlock == null)
                return null;

            return (initialBlock.Height, finalBlock.Height);
        }

        public static async Task<List<Block>?> GetBlockListFromSpan((long, long) blockSpan)
        {
            var blockDb = BlockchainData.GetBlocks();

            if(blockDb == null) 
                return null;

            var initialBlockHeight = blockSpan.Item1;
            var maxBlockHeight = blockSpan.Item2;

            var blockList = blockDb.Query().Where(x => x.Height >= initialBlockHeight && x.Height <= maxBlockHeight).ToList();

            return blockList;
        }

        public static async Task PerformHeaderCreation(long startHeight = 0)
        {
            var blockchain = GetBlockchain();
            var blocksDb = BlockchainData.GetBlocks();
            var interval = Globals.SystemMemory < 2 ? 500 : Globals.SystemMemory >= 2 && Globals.SystemMemory < 6 ? 2000 : 5000;
            var lastBlock = Globals.LastBlock.Height;
            var increment = (double)1 / ((double)lastBlock - (double)startHeight) * (double)100;
            var currentRunHeight = startHeight;
            bool processBlocks = true;
            List<Blockchain> blockHeaders = new List<Blockchain>();
            AnsiConsole.MarkupLine("[green]|*****************************************************************************|[/]");
            AnsiConsole.MarkupLine("[red]| Syncing Blockchain Headers... This process may take a moment.               |[/]");
            AnsiConsole.MarkupLine("[yellow]| This process will only need to run once for the entire chain.               |[/]");
            AnsiConsole.MarkupLine("[yellow]| During this time please DO NOT CLOSE WALLET, or click cursor into the CLI.  |[/]");
            AnsiConsole.MarkupLine("[yellow]| This process is happening so faster block syncing can happen in the future. |[/]");
            AnsiConsole.MarkupLine("[green]|*****************************************************************************|[/]");
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
                    var task1 = ctx.AddTask("[purple]Running Block Header Sync[/]");
                    while (!ctx.IsFinished)
                    {
                        while (processBlocks)
                        {
                            var heightSpan = currentRunHeight + interval;

                            var blocks = blocksDb.Query()
                            .Where(x => x.Height >= currentRunHeight && x.Height < heightSpan)
                            .Limit((int)heightSpan - (int)currentRunHeight)
                            .ToList();

                            foreach (Block block in blocks)
                            {
                                AnsiConsole.Markup($"\rBlock: [blue]{block.Height}[/][red]/[/][green]{lastBlock}[/]");
                                var blockExist = blockchain.Find(Query.All(Query.Descending)).Where(x => x.Height == block.Height).FirstOrDefault();
                                if (blockExist == null)
                                {
                                    Blockchain bHeader = new Blockchain
                                    {
                                        Height = block.Height,
                                        Hash = block.Hash,
                                        Size = block.Size,
                                        CumulativeSize = block.Height == 0 ? block.Size : block.Size + Globals.Blockchain.CumulativeSize,
                                    };

                                    //blockchain.InsertSafe(bHeader);
                                    Globals.Blockchain = bHeader; //update global record
                                    blockHeaders.Add(bHeader);
                                }

                                if (block.Height == lastBlock)
                                {
                                    processBlocks = false;
                                    task1.Increment(100);
                                }
                                else
                                {
                                    task1.Increment(increment);
                                }
                            }

                            if (processBlocks)
                                currentRunHeight += interval;

                            blocks.Clear();
                            blocks = new List<Block>();
                           
                        }

                        blockchain.InsertBulkSafe(blockHeaders);
                        task1.Increment(100);

                    }
                });
        }
    }
}
