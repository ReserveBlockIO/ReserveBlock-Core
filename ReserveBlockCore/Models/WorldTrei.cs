using LiteDB;
using ReserveBlockCore.Data;

namespace ReserveBlockCore.Models
{
    public class WorldTrei
    {
        public string StateRoot { get; set; }
        public static WorldTrei GetWorldTrei()
        {
            var wTrei = DbContext.DB.GetCollection<WorldTrei>(DbContext.RSRV_WSTATE_TREI);
            var worldState = wTrei.FindAll().FirstOrDefault();
            return worldState;
        }

        public static void UpdateWorldTrei(Block block)
        {
            var wTrei = DbContext.DB.GetCollection<WorldTrei>(DbContext.RSRV_WSTATE_TREI);
            var worldState = wTrei.FindAll().FirstOrDefault();
            worldState.StateRoot = block.StateRoot;

            wTrei.Update(worldState);
        }
    }

    
}
