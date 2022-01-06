using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReserveBlockCore.Models
{
    public class Block
    {
		public int Index { get; set; }
		public string Timestamp { get; set; }
		public List<Transaction> Transactions { get; set; }
		public string Hash { get; set; }
		public string PrevHash { get; set; }
		public int Nonce { get; set; } = 0;

		public int NumberOfTransactions
        {
			get { return Transactions.Count(); }
        }
		public string NodeValidatorId { get; set; }
		public int BlockHeight { get; set; }
		public string ChainRev { get; set; }
		public string CurrencyMoved { get; set; }
		public decimal BlockReward { get; set; }
		public decimal FeeReward { get; set; }
		public string ValidatorKey { get; set; }

		//Add Confirmations in here. Next block number - current block height.

	}
}
