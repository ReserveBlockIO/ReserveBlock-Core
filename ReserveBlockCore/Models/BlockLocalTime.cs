using ReserveBlockCore.Data;

namespace ReserveBlockCore.Models
{
    public class BlockLocalTime
    {
        public long Height { get; set; }
        public long LocalTime { get; set; }

        public static LiteDB.ILiteCollection<BlockLocalTime> GetBlockLocalTimes()
        {
            var coll = DbContext.DB.GetCollection<BlockLocalTime>(DbContext.RSRV_LOCAL_TIMES);
            return coll;
        }

        public static BlockLocalTime GetFirstAtLeast(long height)
        {
            var coll = DbContext.DB.GetCollection<BlockLocalTime>(DbContext.RSRV_LOCAL_TIMES);
            return coll.Query().Where(x => x.Height >= height).OrderBy(x => x.Height).Limit(1).FirstOrDefault();            
        }
    }
}
