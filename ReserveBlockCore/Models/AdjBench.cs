using ReserveBlockCore.Data;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Models
{
    public class AdjBench
    {
        public int Id { get; set; }
        public string IPAddress { get; set; }
        public string RBXAddress { get; set; }
        public long TimeEntered { get; set; }
        public long TimeEligibleForConsensus { get; set; }
        public bool PulledFromBench { get; set; }
        public string TopicUID { get; set; }

        public static LiteDB.ILiteCollection<AdjBench> GetBench()
        {
            try
            {
                return DbContext.DB_Config.GetCollection<AdjBench>(DbContext.RSRV_ADJ_BENCH);
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError(ex.ToString(), "AdjBench.GetBench()");
                return null;
            }
        }

        public static bool SaveToBench(AdjBench adjB)
        {
            var adjBenchDB = GetBench();
            if(adjBenchDB != null)
            {
                var rec = adjBenchDB.Query().Where(x => x.RBXAddress == adjB.RBXAddress).FirstOrDefault();
                if(rec == null)
                {
                    adjBenchDB.InsertSafe(adjB);
                    return true;
                }
            }

            return false;
        }

        public static void SaveListToBench(List<AdjBench> adjBList)
        {
            var adjBenchDB = GetBench();
            if (adjBenchDB != null)
            {
                foreach(var adjB in adjBList)
                {
                    var rec = adjBenchDB.Query().Where(x => x.RBXAddress == adjB.RBXAddress).FirstOrDefault();
                    if (rec == null)
                    {
                        adjBenchDB.InsertSafe(adjB);                        
                    }
                }
            }            
        }
    }
}
