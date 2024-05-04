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
using LiteDB;

namespace ReserveBlockCore.Models
{
    public class Transaction
    {
        public ObjectId Id { get; set; }

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
        public string? Data { get; set; } = null;
        public long? UnlockTime { get; set; } = null;
        
        [StringLength(512)]
        public string Signature { get; set; }
        public long Height { get; set; }
        public TransactionType TransactionType { get; set; }
        public TransactionRating? TransactionRating { get; set; }
        public TransactionStatus? TransactionStatus { get; set; }

        public void Build()
        {
            Hash = GetHash();
        }
        public string GetHash()
        {
            var data = UnlockTime == null ? Timestamp + FromAddress + ToAddress + Amount + Fee + Nonce + TransactionType + Data :
                Timestamp + FromAddress + ToAddress + Amount + Fee + Nonce + TransactionType + Data + UnlockTime;
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
        VOTE, //cast vote for topic
        RESERVE, //create a reserve TX
        SC_MINT, //standard sc mint
        SC_TX, //standard sc tx
        SC_BURN, //standard sc burn
        FTKN_MINT, //fungible token mint
        FTKN_TX, //fungible token tx
        FTKN_BURN, //fungible token burn
        TKNZ_MINT,//tokenization token mint
        TKNZ_TX,//tokenization token tx
        TKNZ_BURN,//tokenization token burn
    }

    public enum ReserveTransactionType
    {
        Register,
        Callback,
        Recover
    }

    public enum TransactionStatus
    {
        Pending,
        Success,
        Failed,
        Reserved,
        CalledBack,
        Recovered
    }

    public enum TransactionRating
    {
        A = 1,
        B = 2,
        C = 3,
        D = 4,
        E = 5,
        F = 6
    }
}
