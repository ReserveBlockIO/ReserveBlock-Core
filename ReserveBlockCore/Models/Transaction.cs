using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReserveBlockCore.Models
{
    public class Transaction
    {
        public string TransactionHash { get; set; }
        public string ToAddress { get; set; }
        public string FromAddress { get; set; }
        public decimal Amount { get; set; }
        public decimal TransactionFee { get; set; }
        public string? NFTData { get; set; }

        [ForeignKey("Block")]
        public int BlockHeight { get; set; }
        public virtual Block Block { get; set; }
    }
}
