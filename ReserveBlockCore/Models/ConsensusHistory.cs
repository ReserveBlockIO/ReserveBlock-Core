using ReserveBlockCore.Extensions;
using ReserveBlockCore.Data;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Models
{
    public class ConsensusHistory
    {
        public long Id { get; set; }
        public long Height { get; set; }
        public int MethodCode { get; set; }
        public string SendingAddress { get; set; }
        public string MessageAddress { get; set; }

        public class ConsensusHistoryData
        {
            public static LiteDB.ILiteCollection<ConsensusHistory> GetAll()
            {
                try
                {
                    return DbContext.DB_Consensus.GetCollection<ConsensusHistory>(DbContext.RSRV_CONSENSUS_HISTORY);
                }
                catch (Exception ex)
                {
                    DbContext.Rollback();
                    ErrorLogUtility.LogError(ex.ToString(), "ConsensusHistory.GetAll()");
                    return null;
                }

            }
        }

    }
}
