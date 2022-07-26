﻿using ReserveBlockCore.Extensions;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Services
{
    public class BlockQueueService
    {
        public static bool QueueProcessing = false;
        public static long LastHeightBroadcasted = -1;

        public static void UpdateMemBlocks()
        {
            Program.MemBlocks.Clear();
            Program.MemBlocks.TrimExcess();
            Program.MemBlocks = null;

            var blockChain = BlockchainData.GetBlocks();
            Program.MemBlocks = blockChain.Find(LiteDB.Query.All(LiteDB.Query.Descending), 0, 300).ToList();
        }

        public static async Task ProcessBlockQueue()
        {
            if(!QueueProcessing)
            {
                QueueProcessing = true;
                var blockQueue = BlockchainData.GetBlockQueue();
                var blockQueueAll = blockQueue.FindAll();
                if (blockQueueAll.Count() > 0)
                {
                    var queueOrdered = blockQueueAll.OrderBy(x => x.Height).ThenBy(x => x.Timestamp).ToList();
                    var blockchain = BlockchainData.GetBlocks();
                    foreach (var block in queueOrdered)
                    {
                        try
                        {
                            var findBlock = blockchain.FindOne(x => x.Height == block.Height);
                            if (findBlock == null)
                            {
                                await BlockValidatorService.ValidateBlock(block);//insert into blockchain
                                blockQueue.DeleteMany(x => x.Height == block.Height);//delete from queue
                            }
                            else
                            {
                                blockQueue.DeleteMany(x => x.Height == block.Height); //delete from queue
                            }
                        }
                        catch (Exception ex)
                        {
                            ErrorLogUtility.LogError($"Failed to process block {block.Height} in queue. Error: {ex.Message}", 
                                "BlockQueueService.ProcessBlockQueue()");
                        }
                    }
                }

                QueueProcessing = false;
            }
        }

        public static async Task<bool> AddBlock(Block block)
        {
            var blockQueue = BlockchainData.GetBlockQueue();
            var blockchain = BlockchainData.GetBlocks();
            bool BroadcastBlock = false;

            var blockchainBlock = blockchain.FindOne(x => x.Height == block.Height);
            if(blockchainBlock == null)
            {
                var blockQueueBlock = blockQueue.FindOne(x => x.Height == block.Height);
                if(blockQueueBlock == null)
                {
                    BroadcastBlock = true;
                    blockQueue.Insert(block);
                    try
                    {
                        //REVIEW THIS. POTENTIAL LOOP BROADCAST
                        if(block.Height == LastHeightBroadcasted + 1)
                        {
                            await P2PClient.BroadcastBlock(block);
                            LastHeightBroadcasted = block.Height;
                        }
                    }
                    catch (Exception ex)
                    {

                    }
                }
            }

            return BroadcastBlock;
        }
    }
}
