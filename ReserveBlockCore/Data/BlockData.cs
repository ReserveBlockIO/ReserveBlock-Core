using LiteDB;
using Newtonsoft.Json;
using ReserveBlockCore.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReserveBlockCore.Data
{
    internal class BlockData
    {
        public static ILiteCollection<Models.Block> GetBlocks()
        {
            var coll = DbContext.DB.GetCollection<Block>(DbContext.RSRV_BLOCKS);
            coll.EnsureIndex(x => x.Height);
            return coll;
        }

        public static Block CreateGenesisBlock(IList<Transaction> gTrxList)
        {
            var startTimer = DateTime.UtcNow;

            var timeStamp = 1643932800; //2 Feb. 2022 | This value is hard coded for the start of the chain.

            //var validator = Genesis.GetAll().FirstOrDefault();

            Block block = new Block
            {
                Height = 0,
                Timestamp = timeStamp,
                Transactions = gTrxList,
                Validator = "Alpha Validator",
                ChainRefId = BlockchainData.ChainRef,
                NextValidators = "GenesisBlock",
                ValidatorSignature = "GenesisBlock"
            };

            block.Build();

            //Get the block size
            var str = JsonConvert.SerializeObject(block);
            block.Size = str.Length;

            //Get block crafting time
            var endTimer = DateTime.UtcNow;
            var buildTime = endTimer - startTimer;
            block.BCraftTime = buildTime.Milliseconds;

            return block;
        }
    }
}
