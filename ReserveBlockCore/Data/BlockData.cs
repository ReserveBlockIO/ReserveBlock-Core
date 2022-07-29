using Newtonsoft.Json;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.Models;
using ReserveBlockCore.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using ReserveBlockCore.Extensions;

namespace ReserveBlockCore.Data
{
    internal class BlockData
    {
        public static LiteDB.ILiteCollection<Models.Block> GetBlocks()
        {
            var coll = DbContext.DB.GetCollection<Block>(DbContext.RSRV_BLOCKS);
            coll.EnsureIndexSafe(x => x.Height);
            return coll;
        }

        public static Block CreateGenesisBlock(IList<Transaction> gTrxList)
        {
            var startTimer = DateTime.UtcNow;
            var validatorAccount = AccountData.GetSingleAccount(Program.GenesisAddress);

            var timeStamp = 1643932800; //4 Feb. 2022 | This value is hard coded for the start of the chain.

            Block block = new Block
            {
                Height = 0,
                Timestamp = timeStamp,
                Transactions = gTrxList,
                Validator = "Genesis Validator",
                ChainRefId = BlockchainData.ChainRef,
                TotalValidators = 0,
                ValidatorAnswer = "Genesis Answer"
            };

            block.Build();

            block.ValidatorSignature = "Genesis Signature";

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
