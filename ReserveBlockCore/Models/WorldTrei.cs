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
    }

    
}
