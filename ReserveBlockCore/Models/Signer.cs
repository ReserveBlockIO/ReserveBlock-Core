using ReserveBlockCore.Data;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Models
{
    public class Signer
    {
        public string Id { get; set; }
        public string Address { get; set; }
        public long StartHeight { get; set; }
        public long? EndHeight { get; set; }

        public static LiteDB.ILiteCollection<Signer> GetSigners()
        {
            try
            {
                return DbContext.DB_Config.GetCollection<Signer>(DbContext.RSRV_SIGNER);
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError(ex.ToString(), "AccountData.GetSigners()");
                return null;
            }
        }

        public static HashSet<string> CurrentSigningAddresses()
        {
            var Height = Globals.LastBlock.Height;
            return Globals.Signers.Where(x => x.Key.StartHeight <= Height && (x.Value == null || x.Value >= Height))
               .Select(x => x.Key.Address)
               .ToHashSet();
        }

        public static int NumSigners()
        {
            return CurrentSigningAddresses().Count;
        }
        public static int Majority()
        {
            return NumSigners() / 2 + 1;
        }
    }
}
