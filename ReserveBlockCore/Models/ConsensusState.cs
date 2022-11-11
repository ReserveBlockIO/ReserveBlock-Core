using ReserveBlockCore.Extensions;
using ReserveBlockCore.Data;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Models
{
    public enum ConsensusStatus : byte
    {
        Processing,
        Finalizing,
        Done
    }

    public class ConsensusState
    {
        public long Id { get; set; }
        public long Height { get; set; }
        public int MethodCode { get; set; }
        public ConsensusStatus Status { get; set; }                
        public int RandomNumber { get; set; }
        public string Salt { get; set; }

        public class ConsensusStateData
        {
            public static LiteDB.ILiteCollection<ConsensusState> GetAll()
            {
                try
                {
                    return DbContext.DB_Consensus.GetCollection<ConsensusState>(DbContext.RSRV_CONSENSUS_STATE);
                }
                catch (Exception ex)
                {
                    DbContext.Rollback();
                    ErrorLogUtility.LogError(ex.ToString(), "ConsensusState.GetAll()");
                    return null;
                }

            }
        }

    }
}
