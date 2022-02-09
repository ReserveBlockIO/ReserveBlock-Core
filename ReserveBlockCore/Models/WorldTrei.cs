using LiteDB;
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
            var worldState = wTrei.FindAll().FirstOrDefault();
            return worldState;
        }

        public static void UpdateWorldTrei(Block block)
        {
            var wTrei = GetWorldTrei();
            var record = wTrei.FindAll().FirstOrDefault();
            if (record == null)
            {
                var worldTrei = new WorldTrei
                {
                    StateRoot = block.StateRoot,
                };
                wTrei.Insert(worldTrei);
            }
            else
            {
                record.StateRoot = block.StateRoot;
                wTrei.Update(record);
            }
        }

        public static ILiteCollection<WorldTrei> GetWorldTrei()
        {
            var wTrei = DbContext.DB_AccountStateTrei.GetCollection<WorldTrei>(DbContext.RSRV_WSTATE_TREI);
            return wTrei;
        }
    }

    
}
