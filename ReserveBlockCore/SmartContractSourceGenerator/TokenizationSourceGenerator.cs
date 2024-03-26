using ReserveBlockCore.Models.SmartContracts;
using System.Text;

namespace ReserveBlockCore.SmartContractSourceGenerator
{
    public class TokenizationSourceGenerator
    {
        public static async Task<(StringBuilder, StringBuilder)> Build(TokenizationFeature tknz, StringBuilder strBuild)
        {
            var appendChar = "\"|->\"";
            StringBuilder strTknzBld = new StringBuilder();

            strBuild.AppendLine("let AssetName = \"" + tknz.AssetName + "\"");
            strBuild.AppendLine("let AssetTicker = \"" + tknz.AssetTicker + "\"");
            strBuild.AppendLine("let AssetDepositAddress = \"" + "{DEPO_ADDR}" + "\"");

            strTknzBld.AppendLine("function GetDepositAddressShares() : string");
            strTknzBld.AppendLine("{");
            strTknzBld.AppendLine("   var shares =  \"" + "{SHARES_REPLACE}" + "\"");
            strTknzBld.AppendLine("   return (shares)");
            strTknzBld.AppendLine("}");

            return (strBuild, strTknzBld);
        }
    }
}
