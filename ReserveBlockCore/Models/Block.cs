using LiteDB;
using ReserveBlockCore.Data;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReserveBlockCore.Models
{
    public class Block
    {
		public long Height { get; set; }
		public string ChainRefId { get; set; }
		public long Timestamp { get; set; }
		public string Hash { get; set; }
		public string PrevHash { get; set; }
		public string MerkleRoot { get; set; }
		public string StateRoot { get; set; }
		public decimal TotalAmount { get; set; }
		public string Validator { get; set; }
		public string ValidatorSignature { get; set; }
		public string NextValidators { get; set; }
		public decimal TotalReward { get; set; }
		public int Version { get; set; }
		public int NumOfTx { get; set; }
		public long Size { get; set; }
		public int BCraftTime { get; set; }

		public IList<Transaction> Transactions { get; set; }
		//Methods
		public void Build()
		{
			Version = 1; //have this version increase if invalid/malformed block is submitted to auto branch and avoid need for fork.
			NumOfTx = Transactions.Count;
			TotalAmount = GetTotalAmount();
			TotalReward = GetTotalFees();
			MerkleRoot = GetMerkleRoot();
			PrevHash = GetLastBlack() != null ? GetLastBlack().Hash : "Genesis Block"; //This is done because chain starting there won't be a previous hash. 
			Hash = GetBlockHash();
			StateRoot = GetStateRoot();
			NextValidators = ""; //Create method to get next validators.
		}

		public void BuildBlockSignature()
        {
			
        }

		public void GetNextValidators()
        {
			
        }
		public int NumberOfTransactions
		{
			get { return Transactions.Count(); }
		}
		private decimal GetTotalFees()
		{
			var totFee = Transactions.AsEnumerable().Sum(x => x.Fee) + HalvingUtility.GetBlockReward();
			return totFee;
		}
		public static ILiteCollection<Block> GetBlocks()
		{
			var block = DbContext.DB.GetCollection<Block>(DbContext.RSRV_BLOCKS);
			block.EnsureIndex(x => x.Height);
			return block;
		}
		private static Block GetLastBlack()
		{
			var blockchain = GetBlocks();
			var block = blockchain.FindOne(Query.All(Query.Descending));
			return block;
		}
		private decimal GetTotalAmount()
		{
			var totalAmount = Transactions.AsEnumerable().Sum(x => x.Amount);
			return totalAmount;
		}
		public string GetBlockHash()
		{
			var strSum = Version + PrevHash + MerkleRoot + Timestamp + NumOfTx + Validator + NextValidators + ChainRefId;
			var hash = HashingService.GenerateHash(strSum);
			return hash;
		}
		public string GetStateRoot()
		{
			var strSum = Hash.Substring(0,6) + PrevHash.Substring(0, 6) + MerkleRoot.Substring(0, 6) + Timestamp;
			var hash = HashingService.GenerateHash(strSum);
			return hash;
		}
		private string GetMerkleRoot()
		{
			// List<Transaction> txList = JsonConvert.DeserializeObject<List<Transaction>>(jsonTxs);
			var txsHash = new List<string>();

			Transactions.ToList().ForEach(x => { txsHash.Add(x.Hash); });

			var hashRoot = MerkleService.CreateMerkleRoot(txsHash.ToArray());
			return hashRoot;
		}
	}
}
