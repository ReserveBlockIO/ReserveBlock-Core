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

            var depositAddress = !string.IsNullOrWhiteSpace(tknz.DepositAddress) ? tknz.DepositAddress.ToString() : "{DEPO_ADDR}";
            var share = !string.IsNullOrWhiteSpace(tknz.Share) ? tknz.Share.ToString() : "{SHARES_REPLACE}";
            var backupShare = !string.IsNullOrWhiteSpace(tknz.BackupShare) ? tknz.BackupShare.ToString() : "{SHARES_BACKUP_REPLACE}";
            var txHash = !string.IsNullOrWhiteSpace(tknz.KeyRevealRequestHash) ? tknz.KeyRevealRequestHash.ToString() : "{TX_HASH}";

            strBuild.AppendLine("let AssetName = \"" + tknz.AssetName + "\"");
            strBuild.AppendLine("let AssetTicker = \"" + tknz.AssetTicker + "\"");
            strBuild.AppendLine("let AssetDepositAddress = \"" + depositAddress + "\"");
            strBuild.AppendLine("let KeyRevealRequestHash = \"" + txHash + "\"");

            strTknzBld.AppendLine("function GetDepositAddressShare() : string");
            strTknzBld.AppendLine("{");
            strTknzBld.AppendLine("   var share =  \"" + share + "\"");
            strTknzBld.AppendLine("   return (share)");
            strTknzBld.AppendLine("}");

            strTknzBld.AppendLine("function GetDepositAddressShareBackup() : string");
            strTknzBld.AppendLine("{");
            strTknzBld.AppendLine("   var backupShare =  \"" + backupShare + "\"");
            strTknzBld.AppendLine("   return (backupShare)");
            strTknzBld.AppendLine("}");
            
            
            return (strBuild, strTknzBld);
        }
    }
}
