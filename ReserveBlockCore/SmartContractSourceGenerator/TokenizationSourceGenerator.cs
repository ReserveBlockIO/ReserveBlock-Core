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
            strBuild.AppendLine("let DepositAddress = \"" + tknz.DepositAddress + "\"");

            strTknzBld.AppendLine("function GetPublicKeyProofs() : string");
            strTknzBld.AppendLine("{");
            strTknzBld.AppendLine("   var proof =  \"" + tknz.PublicKeyProofs + "\"");
            strTknzBld.AppendLine("   return (proof)");
            strTknzBld.AppendLine("}");
            
            return (strBuild, strTknzBld);
        }
    }
}
