using LiteDB;
using ReserveBlockCore.Data;

namespace ReserveBlockCore.Models
{
    public class WorldTrei
    {
        public long Id { get; set; }
        public string StateRoot { get; set; }
        public static WorldTrei GetWorldTrei()
        {
            var wTrei = DbContext.DB_WorldStateTrei.GetCollection<WorldTrei>(DbContext.RSRV_WSTATE_TREI);
            var worldState = wTrei.FindAll().FirstOrDefault();
            return worldState;
        }

        public static void UpdateWorldTrei(Block block)
        {
            var wTrei = DbContext.DB_WorldStateTrei.GetCollection<WorldTrei>(DbContext.RSRV_WSTATE_TREI);
            var worldState = wTrei.FindAll().FirstOrDefault();
            worldState.StateRoot = block.StateRoot;

            wTrei.Update(worldState);
        }
    }

    
}
