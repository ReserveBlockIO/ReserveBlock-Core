using ReserveBlockCore.Extensions;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models.SmartContracts;

namespace ReserveBlockCore.Models
{
    public class SmartContractStateTrei
    {
        public long Id { get; set; }
        public string SmartContractUID { get; set; }
        public string ContractData { get; set; }
        public string MinterAddress { get; set; }
        public string OwnerAddress { get; set; }
        public string? NextOwner { get; set; }
        public bool IsLocked { get; set; }
        public string? Locators { get; set; }
        public long Nonce { get; set; }
        public bool? IsToken { get; set; }
        public string? MD5List { get; set; }
        public bool? MinterManaged { get; set; }
        public decimal? PurchaseAmount { get; set; } //Royalty is included in this.
        public List<string>? PurchaseKeys { get; set; }
        public TokenDetails? TokenDetails { get; set; }

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

        public static IEnumerable<SmartContractStateTrei>? GetSmartContractsOwnedByAddress(string address)
        {
            var scs = GetSCST();
            if (scs != null)
            {
                var scList = scs.Query().Where(x => x.OwnerAddress == address).ToEnumerable();
                if (scList.Count() > 0)
                {
                    return scList;
                }
            }

            return null;
        }

        public static void SaveSmartContract(SmartContractStateTrei scMain)
        {
            var scs = GetSCST();

            var exist = scs.FindOne(x => x.SmartContractUID == scMain.SmartContractUID);

            if(exist == null)
            {
                scs.InsertSafe(scMain);
            }
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
