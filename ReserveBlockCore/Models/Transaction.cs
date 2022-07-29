using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReserveBlockCore.Extensions;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.Services;

namespace ReserveBlockCore.Models
{
    public class Transaction
    {
        [StringLength(128)]
        public string Hash { get; set; }
        [StringLength(36)]
        public string ToAddress { get; set; }
        [StringLength(36)]
        public string FromAddress { get; set; }
        public decimal Amount { get; set; }
        public long Nonce { get; set; }
        public decimal Fee { get; set; }
        public long Timestamp { get; set; }
        public string? Data { get; set; }
        [StringLength(512)]
        public string Signature { get; set; }
        public long Height { get; set; }
        public TransactionType TransactionType { get; set; }

        public void Build()
        {
            Hash = GetHash();
        }
        public string GetHash()
        {
            var data = Timestamp + FromAddress + ToAddress + Amount + Fee + Nonce + TransactionType + Data;
            return HashingService.GenerateHash(HashingService.GenerateHash(data));
        }
        public static void Add(Transaction transaction)
        {
            var transactions = GetAll();
            transactions.InsertSafe(transaction);
        }
        public static LiteDB.ILiteCollection<Transaction> GetAll()
        {
            var trans = DbContext.DB_Wallet.GetCollection<Transaction>(DbContext.RSRV_TRANSACTIONS);
            return trans;
        }
    }
    public enum TransactionType
    {
        TX,
        NODE,
        NFT_MINT, //mint
        NFT_TX, //transfer or other process (not for sale or burn)
        NFT_BURN,//burn nft
        NFT_SALE,//sale NFT
        ADNR, //address dnr
        DSTR, //DST shop registration
        VOTE_TOPIC, //voting topic for validators to vote on
        VOTE //cast vote for topic
    }
}
