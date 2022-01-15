using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiteDB;
using ReserveBlockCore.Data;
using ReserveBlockCore.Services;

namespace ReserveBlockCore.Models
{
    public class Transaction
    {
        public string Hash { get; set; }
        public string ToAddress { get; set; }
        public string FromAddress { get; set; }
        public decimal Amount { get; set; }
        public decimal Fee { get; set; }
        public long Timestamp { get; set; }
        public string? NFTData { get; set; }

        [ForeignKey("Block")]
        public int BlockHeight { get; set; }
        public virtual Block Block { get; set; }

        public void Build()
        {
            Hash = GetHash();
        }
        public string GetHash()
        {
            var data = Timestamp + FromAddress + Amount + Fee + ToAddress;
            return HashingService.GenerateHash(HashingService.GenerateHash(data));
        }
        public static void Add(Transaction transaction)
        {
            var transactions = GetAll();
            transactions.Insert(transaction);
        }
        public static ILiteCollection<Transaction> GetAll()
        {
            var trans = DbContext.DB.GetCollection<Transaction>(DbContext.RSRV_TRANSACTIONS);
            return trans;
        }

    }
}
