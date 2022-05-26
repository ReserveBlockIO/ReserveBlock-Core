using ReserveBlockCore.Models.SmartContracts;
using System.Text;

namespace ReserveBlockCore.SmartContractSourceGenerator
{
    public class RoyaltySourceGenerator
    {
        public static async Task<(StringBuilder, StringBuilder)> Build(RoyaltyFeature royalty)
        {
            var appendChar = "\"|->\"";
            StringBuilder strRoyaltyBld = new StringBuilder();
            StringBuilder strBuild = new StringBuilder();

            strBuild.AppendLine("let RoyaltyType = \"" + ((int)royalty.RoyaltyType).ToString() + "\"");
            strBuild.AppendLine("let RoyaltyAmount = \"" + royalty.RoyaltyAmount.ToString() + "\"");
            strBuild.AppendLine("let RoyaltyPayToAddress = \"" + royalty.RoyaltyPayToAddress + "\"");

            strRoyaltyBld.AppendLine("function GetRoyaltyData(royaltyType  : string, royaltyAmount : string, royaltyPayToAddress : string) : string");
            strRoyaltyBld.AppendLine("{");
            strRoyaltyBld.AppendLine("   return (royaltyType + " + appendChar + " + royaltyAmount + " + appendChar + " + royaltyPayToAddress)");
            strRoyaltyBld.AppendLine("}");

            return (strBuild, strRoyaltyBld);
        }
    }
}
