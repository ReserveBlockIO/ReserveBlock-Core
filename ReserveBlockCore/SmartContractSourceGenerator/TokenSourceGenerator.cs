using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.Utilities;
using System.Text;

namespace ReserveBlockCore.SmartContractSourceGenerator
{
    public class TokenSourceGenerator
    {
        public static async Task<(StringBuilder, StringBuilder)> Build(TokenFeature token, StringBuilder strBuild)
        {
            StringBuilder strTokenBld = new StringBuilder();
            var appendChar = "\"|->\"";
            try
            {
                strBuild.AppendLine("let TokenName = \"" + token.TokenName + "\"");
                strBuild.AppendLine("let TokenTicker = \"" + token.TokenTicker + "\"");
                strBuild.AppendLine("let TokenDecimalPlaces = " + token.TokenDecimalPlaces.ToString());
                strBuild.AppendLine("let TokenSupply = " + token.TokenSupply.ToString());
                strBuild.AppendLine("let TokenBurnable = " + token.TokenBurnable.ToString());
                strBuild.AppendLine("let TokenVoting = " + token.TokenVoting.ToString());
                strBuild.AppendLine("let TokenMintable = " + token.TokenVoting.ToString());
                strBuild.AppendLine("let TokenImageURL = \"" + token.TokenImageURL + "\"");
                strBuild.AppendLine("let TokenImageBase = \"" + token.TokenImageBase + "\"");

                strTokenBld.AppendLine("function GetTokenDetails() : any");
                strTokenBld.AppendLine("{");
                strTokenBld.AppendLine("   return getTokenDetails(TokenName, TokenTicker, TokenDecimalPlaces, TokenSupply, TokenVoting, TokenBurnable, TokenImageURL, TokenImageBase)");
                strTokenBld.AppendLine("}");

                var tokenFuncs = "";

                if (token.TokenVoting)
                {
                    var votingToken = RandomStringUtility.GetRandomStringOnlyLetters(18);
                    strTokenBld.AppendLine("function GetVotingRules() : any");
                    strTokenBld.AppendLine("{");
                    strTokenBld.AppendLine($"   return getVotingRules(1, \"" + votingToken + "\", 30, true");
                    strTokenBld.AppendLine("}");
                    tokenFuncs = tokenFuncs + " + " + appendChar + " + \"TokenVote()\" + " + appendChar + " + \"TokenCreateVote()\"";
                }

                if (token.TokenBurnable)
                {
                    strTokenBld.AppendLine("function GetBurnRules() : any");
                    strTokenBld.AppendLine("{");
                    strTokenBld.AppendLine("   return getBurnRules(0)");
                    strTokenBld.AppendLine("}");
                    tokenFuncs = tokenFuncs + " + " + appendChar + " + \"TokenBurn()\"";
                }

                if(token.TokenMintable)
                {
                    tokenFuncs = tokenFuncs + " + " + appendChar + " + \"TokenMint()\"";
                }

                strTokenBld.AppendLine("function GetTokenFunctions() : any");
                strTokenBld.AppendLine("{");
                strTokenBld.AppendLine("   return \"TokenTransfer()\"" + " + " + appendChar + "\"TokenDeploy()\"" + tokenFuncs);
                strTokenBld.AppendLine("}");

                return (strBuild, strTokenBld);
            }
            catch
            {
                strBuild.Clear();
                strBuild.Append("Failed");
                return (strBuild, strTokenBld);
            }
        }
    }
}
