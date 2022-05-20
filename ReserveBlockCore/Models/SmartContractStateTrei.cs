using LiteDB;
using ReserveBlockCore.Data;

namespace ReserveBlockCore.Models
{
    public class SmartContractStateTrei
    {
        public string SmartContractUID { get; set; }
        public string ContractData { get; set; }
        public string MinterAddress { get; set; }
        public string OwnerAddress { get; set; }
        public long Nonce { get; set; }

        public static ILiteCollection<SmartContractStateTrei> GetSCST()
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

            scs.Insert(scMain);
        }
        public static void UpdateSmartContract(SmartContractStateTrei scMain)
        {
            var scs = GetSCST();

            scs.Update(scMain);
        }

        public static void DeleteSmartContract(SmartContractStateTrei scMain)
        {
            var scs = GetSCST();

            scs.DeleteMany(x => x.SmartContractUID == scMain.SmartContractUID);
        }
    }
}
