using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.Utilities;
using System.Text;

namespace ReserveBlockCore.SmartContractSourceGenerator
{
    public class EvolveSourceGenerator
    {
        public static async Task<(StringBuilder, StringBuilder)> Build(List<EvolvingFeature> evolve, StringBuilder strBuild)
        {
            var appendChar = "\"|->\"";
            StringBuilder strEvolveBld = new StringBuilder();
            bool isDynamic = false;

            var maxEvoState = evolve.Count().ToString();
            var evolutionaryState = "\"{*0}\"";

            //Evolve Constants
            strBuild.AppendLine("var EvolutionaryState = " + evolutionaryState);
            strBuild.AppendLine("let EvolutionaryMaxState = \"" + maxEvoState + "\"");

            //Methods
            //Get Current Evolve State Method
            strEvolveBld.AppendLine("function GetCurrentEvolveState() : string");
            strEvolveBld.AppendLine("{");
            strEvolveBld.AppendLine("   var evoState = EvolutionaryState");
            strEvolveBld.AppendLine("   return evoState");
            strEvolveBld.AppendLine("}");

            //Get Evolve States
            strEvolveBld.AppendLine("function EvolveStates() : string");
            strEvolveBld.AppendLine("{");
            strEvolveBld.AppendLine(@"  return EvolutionaryMaxState");
            strEvolveBld.AppendLine("}");

            //Evolve
            strEvolveBld.AppendLine("function Evolve(evoState : int) : string");
            strEvolveBld.AppendLine("{");
            strEvolveBld.AppendLine("   if evoState < int(EvolutionaryMaxState)");
            strEvolveBld.AppendLine("   {");
            strEvolveBld.AppendLine("       var newEvolveState = evoState + 1");
            strEvolveBld.AppendLine("       if(newEvolveState > int(EvolutionaryMaxState))");
            strEvolveBld.AppendLine("       {");
            strEvolveBld.AppendLine(@"          return ""Failed to Evolve.""");
            strEvolveBld.AppendLine("       }");
            strEvolveBld.AppendLine(@"      EvolutionaryState = ""{*"" + string(newEvolveState) + ""}""");
            strEvolveBld.AppendLine("       return string(newEvolveState)");
            strEvolveBld.AppendLine("   }");
            strEvolveBld.AppendLine(@"  return ""Failed to Evolve.""");
            strEvolveBld.AppendLine("}");

            //Devolve
            strEvolveBld.AppendLine("function Devolve(evoState : int) : string");
            strEvolveBld.AppendLine("{");
            strEvolveBld.AppendLine("if evoState > 0");
            strEvolveBld.AppendLine("{");
            strEvolveBld.AppendLine("var newEvolveState = evoState - 1");
            strEvolveBld.AppendLine("if(newEvolveState < 0)");
            strEvolveBld.AppendLine("{");
            strEvolveBld.AppendLine(@"return ""Failed to Devolve.""");
            strEvolveBld.AppendLine("}");
            strEvolveBld.AppendLine(@"EvolutionaryState = ""{*"" + string(newEvolveState) + ""}""");
            strEvolveBld.AppendLine("return string(newEvolveState)");
            strEvolveBld.AppendLine("}");
            strEvolveBld.AppendLine(@"return ""Failed to Devolve.""");
            strEvolveBld.AppendLine("}");

            //Evolve Specific
            strEvolveBld.AppendLine("function ChangeEvolveStateSpecific(evoState : int) : string");
            strEvolveBld.AppendLine("{");
            strEvolveBld.AppendLine("if evoState <= int(EvolutionaryMaxState) && evoState >= 0");
            strEvolveBld.AppendLine("{");
            strEvolveBld.AppendLine(@"EvolutionaryState = ""{*"" + string(evoState) + ""}""");
            strEvolveBld.AppendLine("return string(evoState)");
            strEvolveBld.AppendLine("}");
            strEvolveBld.AppendLine(@"return ""Failed to Evolve.""");
            strEvolveBld.AppendLine("}");

            int counter = 1;
            evolve.ForEach(x =>
            {
                var evoLetter = FunctionNameUtility.GetFunctionLetter(x.EvolutionState);
                strEvolveBld.AppendLine("function EvolveState" + evoLetter + "() : string");
                strEvolveBld.AppendLine("{");
                strEvolveBld.AppendLine(@"var evoState = " + "\"" + x.EvolutionState.ToString() + "\"");
                strEvolveBld.AppendLine(@"var name = " + "\"" + x.Name + "\"");
                strEvolveBld.AppendLine(@"var description = " + "\"" + x.Description + "\"");
                strEvolveBld.AppendLine(@"var assetName = " + "\"" + (x.SmartContractAsset == null ? "" : x.SmartContractAsset.Name) + "\"");
                strEvolveBld.AppendLine(@"var evolveDate = " + "\"" + (x.EvolveDate == null ? "" : x.EvolveDate.Value.Ticks.ToString()) + "\"");
                strEvolveBld.AppendLine(@"var evolveAtBlock = " + "\"" + (x.EvolveBlockHeight == null ? "" : x.EvolveBlockHeight.Value.ToString()) + "\"");
                strEvolveBld.AppendLine("return (evoState + " + appendChar + " + name + " + appendChar + " + description + " + appendChar + " + assetName + " + appendChar + " + evolveDate + " + appendChar + " + evolveAtBlock)");
                strEvolveBld.AppendLine("}");

                if (x.IsDynamic == true)
                {
                    isDynamic = true;
                }
                counter += 1;
            });

            if (isDynamic == true)
            {

            }

            return (strBuild, strEvolveBld);
        }
    }
}
