using ReserveBlockCore.Extensions;
using ReserveBlockCore.Data;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Models
{
    public class Consensus
    {
        public long Id { get; set; }
        public long Height { get; set; }
        public int MethodCode { get; set; }
        public string Address { get; set; }
        public string Signature { get; set; }
        public string Message { get; set; }        

    }
}
