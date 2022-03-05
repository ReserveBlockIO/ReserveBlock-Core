using LiteDB;
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
            var validatorAccount = AccountData.GetSingleAccount("RBdwbhyqwJCTnoNe1n7vTXPJqi5HKc6NTH");

            var timeStamp = 1643932800; //4 Feb. 2022 | This value is hard coded for the start of the chain.

            Block block = new Block
            {
                Height = 0,
                Timestamp = timeStamp,
                Transactions = gTrxList,
                Validator = "RBdwbhyqwJCTnoNe1n7vTXPJqi5HKc6NTH",
                ChainRefId = BlockchainData.ChainRef,
                NextValidators = "RBdwbhyqwJCTnoNe1n7vTXPJqi5HKc6NTH:RBdwbhyqwJCTnoNe1n7vTXPJqi5HKc6NTH",
            };

            block.Build();

            BigInteger b1 = BigInteger.Parse(validatorAccount.PrivateKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
            PrivateKey privateKey = new PrivateKey("secp256k1", b1);

            //Add validator signature
            block.ValidatorSignature = SignatureService.CreateSignature(block.Hash, privateKey, validatorAccount.PublicKey);

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
