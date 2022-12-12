using ReserveBlockCore.Extensions;
using ReserveBlockCore.Data;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Models
{
    public class Adjudicators
    {
        public long Id { get; set; }
        public string Address { get; set; }
        public string UniqueName { get; set; }
        public string Signature { get; set; }
        public bool IsActive { get; set; }
        public bool IsLeadAdjuidcator { get; set; }
        public string NodeIP { get; set; }
        public string WalletVersion { get; set; }
        public DateTime LastChecked { get; set; }

        public class AdjudicatorData
        {
            public static LiteDB.ILiteCollection<Adjudicators> GetAll()
            {
                try
                {
                    var adjudicators = DbContext.DB_Peers.GetCollection<Adjudicators>(DbContext.RSRV_ADJUDICATORS);
                    return adjudicators;
                }
                catch (Exception ex)
                {                    
                    ErrorLogUtility.LogError(ex.ToString(), "Adjudicators.GetAll()");
                    return null;
                }

            }

            public static Adjudicators GetLeadAdjudicator()
            {
                var adjudicators = GetAll();
                if (Globals.IsTestNet == true)
                    return adjudicators.FindOne(x => x.IsLeadAdjuidcator && x.Address.StartsWith("x"));


                return adjudicators.FindOne(x => x.IsLeadAdjuidcator);
            }

            public static Adjudicators[] GetAdjudicators()
            {
                var adjudicators = GetAll();
                if (Globals.IsTestNet == true)                
                    return adjudicators.Find(x => x.Address.StartsWith("x")).ToArray();
                

                return adjudicators.FindAll().ToArray();
            }

            public static Guid GetAdjudicatorKey()
            {
                return Guid.NewGuid();
            }
        }

    }
}
