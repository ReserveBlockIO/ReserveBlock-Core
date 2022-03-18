using LiteDB;
using ReserveBlockCore.Models;

namespace ReserveBlockCore.Data
{
    public class ConfigData
    {
        public static ILiteCollection<ConfigRules> GetConfigRules()
        {
            var rules = DbContext.DB_Config.GetCollection<ConfigRules>(DbContext.RSRV_CONFIG_RULES);
            return rules;

        }
    }
}
