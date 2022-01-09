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
		public int BlockHeight { get; set; }
		public long Timestamp { get; set; }
		public byte[] Hash { get; set; }
		public byte[] PrevHash { get; set; }
		public string NodeValidatorId { get; set; }
		public string ChainRev { get; set; }
		public string CurrencyMoved { get; set; }
		public decimal BlockReward { get; set; }
		public decimal FeeReward { get; set; }
		public string ValidatorKey { get; set; }

		//FK Relationships
		[Required]
		[ForeignKey("Blockchain")]
		public string ChainHash { get; set; }
		public virtual Blockchain Blockchain { get; set; }
		public virtual ICollection<Transaction> Transactions { get; set; }
		//Methods
		public int NumberOfTransactions
		{
			get { return Transactions.Count(); }
		}

	}
}
