using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using ReserveBlockCore.Models;

namespace ReserveBlockCore.Utilities
{
    public class TransactionUtility
    {
        public static (bool, bool, string, string, JArray?) GetSCTXFunctionAndUID(Transaction tx)
        {
            string scUID = "";
            string function = "";
            bool skip = false;
            JToken? scData = null;
            JArray? scDataArray = null;
            try
            {
                scDataArray = JsonConvert.DeserializeObject<JArray>(tx.Data);
                scData = scDataArray[0];

                function = (string?)scData["Function"];
                scUID = (string?)scData["ContractUID"];
                skip = true;
            }
            catch { }

            try
            {
                if (!skip)
                {
                    var jobj = JObject.Parse(tx.Data);
                    scUID = jobj["ContractUID"]?.ToObject<string?>();
                    function = jobj["Function"]?.ToObject<string?>();
                }
            }
            catch { }

            if(!string.IsNullOrEmpty(scUID) && !string.IsNullOrEmpty(function))
            {
                return (true, skip, scUID, function, scDataArray);
            }

            return (false, skip, "FAIL", "FAIL", scDataArray);
        }
    }
}
