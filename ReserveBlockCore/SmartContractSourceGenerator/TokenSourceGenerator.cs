using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.Utilities;
using System.Text;

namespace ReserveBlockCore.SmartContractSourceGenerator
{
    public class TokenSourceGenerator
    {
        private static string DefaultImageBase64 = " data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAYAAACqaXHeAAAAGXRFWHRTb2Z0d2FyZQBBZG9iZSBJbWFnZVJlYWR5ccllPAAAAyVpVFh0WE1MOmNvbS5hZG9iZS54bXAAAAAAADw/eHBhY2tldCBiZWdpbj0i77u/IiBpZD0iVzVNME1wQ2VoaUh6cmVTek5UY3prYzlkIj8+IDx4OnhtcG1ldGEgeG1sbnM6eD0iYWRvYmU6bnM6bWV0YS8iIHg6eG1wdGs9IkFkb2JlIFhNUCBDb3JlIDYuMC1jMDAyIDc5LjE2NDQ4OCwgMjAyMC8wNy8xMC0yMjowNjo1MyAgICAgICAgIj4gPHJkZjpSREYgeG1sbnM6cmRmPSJodHRwOi8vd3d3LnczLm9yZy8xOTk5LzAyLzIyLXJkZi1zeW50YXgtbnMjIj4gPHJkZjpEZXNjcmlwdGlvbiByZGY6YWJvdXQ9IiIgeG1sbnM6eG1wTU09Imh0dHA6Ly9ucy5hZG9iZS5jb20veGFwLzEuMC9tbS8iIHhtbG5zOnN0UmVmPSJodHRwOi8vbnMuYWRvYmUuY29tL3hhcC8xLjAvc1R5cGUvUmVzb3VyY2VSZWYjIiB4bWxuczp4bXA9Imh0dHA6Ly9ucy5hZG9iZS5jb20veGFwLzEuMC8iIHhtcE1NOkRvY3VtZW50SUQ9InhtcC5kaWQ6NjAwQUZFRTcxNkNDMTFFRTgxQTBDMjREMzU2OTMyMDMiIHhtcE1NOkluc3RhbmNlSUQ9InhtcC5paWQ6NjAwQUZFRTYxNkNDMTFFRTgxQTBDMjREMzU2OTMyMDMiIHhtcDpDcmVhdG9yVG9vbD0iQWRvYmUgUGhvdG9zaG9wIDIyLjAgKE1hY2ludG9zaCkiPiA8eG1wTU06RGVyaXZlZEZyb20gc3RSZWY6aW5zdGFuY2VJRD0ieG1wLmlpZDoxOEM3NkQwM0Y5NTIxMUVDOTA0RUZBMTM3MjUzMjMwQyIgc3RSZWY6ZG9jdW1lbnRJRD0ieG1wLmRpZDoxOEM3NkQwNEY5NTIxMUVDOTA0RUZBMTM3MjUzMjMwQyIvPiA8L3JkZjpEZXNjcmlwdGlvbj4gPC9yZGY6UkRGPiA8L3g6eG1wbWV0YT4gPD94cGFja2V0IGVuZD0iciI/PleOFJoAABVuSURBVHja7NtZ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";
        public static async Task<(StringBuilder, StringBuilder)> Build(TokenFeature token, StringBuilder strBuild)
        {
            StringBuilder strTokenBld = new StringBuilder();
            var appendChar = "\"|->\"";
            try
            {
                var imageBase = token.TokenImageURL == null && token.TokenImageBase == null ? DefaultImageBase64 : token.TokenImageBase;
                strBuild.AppendLine("let TokenName = \"" + token.TokenName + "\"");
                strBuild.AppendLine("let TokenTicker = \"" + token.TokenTicker + "\"");
                strBuild.AppendLine("let TokenDecimalPlaces = " + token.TokenDecimalPlaces.ToString());
                strBuild.AppendLine("let TokenSupply = " + token.TokenSupply.ToString());
                strBuild.AppendLine("let TokenBurnable = " + token.TokenBurnable.ToString());
                strBuild.AppendLine("let TokenVoting = " + token.TokenVoting.ToString());
                strBuild.AppendLine("let TokenMintable = " + token.TokenVoting.ToString());
                strBuild.AppendLine("let TokenImageURL = \"" + token.TokenImageURL + "\"");
                strBuild.AppendLine("let TokenImageBase = \"" + imageBase + "\"");

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
