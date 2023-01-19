using ReserveBlockCore.Extensions;
using ReserveBlockCore.Data;

namespace ReserveBlockCore.Models
{
    public class WorldTrei
    {
        public long Id { get; set; }
        public string StateRoot { get; set; }
        public static WorldTrei GetWorldTreiRecord()
        {
            var wTrei = DbContext.DB_WorldStateTrei.GetCollection<WorldTrei>(DbContext.RSRV_WSTATE_TREI);
            var worldState = wTrei.FindOne(x => true);
            return worldState;
        }

        public static void UpdateWorldTrei(Block block)
        {
            try
            {
                var wTrei = GetWorldTrei();
                var record = wTrei.FindOne(x => true);
                if (record == null)
                {
                    var worldTrei = new WorldTrei
                    {
                        StateRoot = block.StateRoot,
                    };
                    wTrei.InsertSafe(worldTrei);
                }
                else
                {
                    record.StateRoot = block.StateRoot;
                    wTrei.UpdateSafe(record);
                }
            }
            catch { }
        }

        public static LiteDB.ILiteCollection<WorldTrei> GetWorldTrei()
        {
            var wTrei = DbContext.DB_WorldStateTrei.GetCollection<WorldTrei>(DbContext.RSRV_WSTATE_TREI);
            return wTrei;
        }
    }

    
}
