using ReserveBlockCore.Extensions;
using ReserveBlockCore.Data;
using ReserveBlockCore.Extensions;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Models
{
    public class Validators
    {
        public long Id { get; set; }
        public string Address { get; set; }
        public string UniqueName { get; set; }
        public long Position { get; set; }
        public decimal Amount { get; set; } //Must be 1000 or more.
        public long EligibleBlockStart { get; set; }
        public string Signature { get; set; }
        public bool IsActive { get; set; }
        public int FailCount { get; set; }
        public string NodeIP { get; set; } 
        public string NodeReferenceId { get; set; } 
        public string WalletVersion { get; set; }
        public DateTime LastChecked { get; set; }
        public class Validator
        {
            public static List<Validators> ValidatorList { get; set; }

            public static string backupValidator = Globals.GenesisAddress;

            public static int FailCountLimit = 10;

            public static void Add(Validators validator)
            {
                var validators = GetAll();

                // insert into database
                validators.InsertSafe(validator);
            }

            public static LiteDB.ILiteCollection<Validators> GetAll()
            {
                try
                {
                    var validators = DbContext.DB_Wallet.GetCollection<Validators>(DbContext.RSRV_VALIDATORS);
                    return validators;
                }
                catch (Exception ex)
                {
                    DbContext.Rollback();
                    ErrorLogUtility.LogError(ex.ToString(), "Validators.GetAll()");
                    return null;
                }
                
            }

            public static LiteDB.ILiteCollection<Validators> GetOldAll()
            {
                try
                {
                    var validators = DbContext.DB_Peers.GetCollection<Validators>(DbContext.RSRV_VALIDATORS);
                    return validators;
                }
                catch (Exception ex)
                {
                    DbContext.Rollback();
                    ErrorLogUtility.LogError(ex.ToString(), "Validators.GetAll()");
                    return null;
                }

            }

            internal static void Initialize()
            {
                ValidatorList = new List<Validators>();
                var staker = GetAll();
                if (staker.Count() < 1)
                {

                }
                else
                {
                    ValidatorList.AddRange(GetAll().FindAll());
                }


            }

        }
    }

}
