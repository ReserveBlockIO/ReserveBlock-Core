using ReserveBlockCore.Extensions;
using ReserveBlockCore.Data;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Models
{
    public enum ConsensusStatus : byte
    {
        Processing,
        Finalized        
    }

    public class ConsensusState
    {
        public long Id { get; set; }        
        public int MethodCode { get; set; }
        public ConsensusStatus Status { get; set; }                
        public int RandomNumber { get; set; }           
        public bool IsUsed { get; set; }
    }
}
