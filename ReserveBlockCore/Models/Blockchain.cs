using LiteDB;
using ReserveBlockCore.Data;
using ReserveBlockCore.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public static async Task AddBlock(Block block)
        {
            var blockchain = GetBlockchain();
            if (blockchain != null)
            {
                var blockExist = blockchain.Query().Where(x => x.Height == block.Height).FirstOrDefault();
                if (blockExist == null)
                {
                    Blockchain rec = new Blockchain {
                        Hash = block.Hash,
                        Height = block.Height,
                        Size = block.Size,
                        CumulativeSize = Globals.Blockchain.CumulativeSize + block.Size
                    };

                    blockchain.InsertSafe(rec); //insert latest

                    Globals.Blockchain = rec; //update global record
                }
            }
        }
    }
}
