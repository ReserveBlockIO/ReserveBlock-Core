using ReserveBlockCore.Extensions;
using ReserveBlockCore.Data;

namespace ReserveBlockCore.Models
{
    public class SmartContractStateTrei
    {
        public long Id { get; set; }
        public string SmartContractUID { get; set; }
        public string ContractData { get; set; }
        public string MinterAddress { get; set; }
        public string OwnerAddress { get; set; }
        public string? Locators { get; set; }
        public long Nonce { get; set; }
        public string? MD5List { get; set; }
        public bool? MinterManaged { get; set; }

        public static LiteDB.ILiteCollection<SmartContractStateTrei> GetSCST()
        {
            var scs = DbContext.DB_SmartContractStateTrei.GetCollection<SmartContractStateTrei>(DbContext.RSRV_SCSTATE_TREI);
            return scs;
        }

        public static SmartContractStateTrei? GetSmartContractState(string smartContractUID)
        {
            var scs = GetSCST();
            if (scs != null)
            {
                var sc = scs.FindOne(x => x.SmartContractUID == smartContractUID);
                if (sc != null)
                {
                    return sc;
                }
            }

            return null;
        }

        public static void SaveSmartContract(SmartContractStateTrei scMain)
        {
            var scs = GetSCST();

            scs.InsertSafe(scMain);
        }
        public static void UpdateSmartContract(SmartContractStateTrei scMain)
        {
            var scs = GetSCST();

            scs.UpdateSafe(scMain);
        }

        public static void DeleteSmartContract(SmartContractStateTrei scMain)
        {
            var scs = GetSCST();

            scs.DeleteManySafe(x => x.SmartContractUID == scMain.SmartContractUID);
        }
    }
}
