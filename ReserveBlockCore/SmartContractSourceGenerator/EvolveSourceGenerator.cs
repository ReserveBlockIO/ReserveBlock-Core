using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.Utilities;
using System.Text;

namespace ReserveBlockCore.SmartContractSourceGenerator
{
    public class EvolveSourceGenerator
    {
        public static async Task<(StringBuilder, StringBuilder)> Build(List<EvolvingFeature> evolve, StringBuilder strBuild, string scUID, int? activeEvoState = null, bool isReading = false)
        {
            var appendChar = "\"|->\"";
            StringBuilder strEvolveBld = new StringBuilder();
            bool isDynamic = false;
            bool isDynamicDate = false;
            bool isDynamicBlock = false;
            bool failedToSaveAsset = false;

            var maxEvoState = evolve.Count().ToString();
            var evolutionaryState = "\"{*0}\"";
            if (activeEvoState != null)
            {
                var newEvoNum = activeEvoState.Value.ToString();
                evolutionaryState = "\"{*" + activeEvoState + "}\"";
            }

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
            strEvolveBld.AppendLine("      return string(newEvolveState)");
            strEvolveBld.AppendLine("   }");
            strEvolveBld.AppendLine(@"  return ""Failed to Evolve.""");
            strEvolveBld.AppendLine("}");

            //Devolve
            strEvolveBld.AppendLine("function Devolve(evoState : int) : string");
            strEvolveBld.AppendLine("{");
            strEvolveBld.AppendLine("   if evoState > 0");
            strEvolveBld.AppendLine("   {");
            strEvolveBld.AppendLine("       var newEvolveState = evoState - 1");
            strEvolveBld.AppendLine("       if(newEvolveState < 0)");
            strEvolveBld.AppendLine("       {");
            strEvolveBld.AppendLine(@"          return ""Failed to Devolve.""");
            strEvolveBld.AppendLine("       }");
            strEvolveBld.AppendLine(@"      EvolutionaryState = ""{*"" + string(newEvolveState) + ""}""");
            strEvolveBld.AppendLine("      return string(newEvolveState)");
            strEvolveBld.AppendLine("   }");
            strEvolveBld.AppendLine(@"  return ""Failed to Devolve.""");
            strEvolveBld.AppendLine("}");

            //Evolve Specific
            strEvolveBld.AppendLine("function ChangeEvolveStateSpecific(evoState : int) : string");
            strEvolveBld.AppendLine("{");
            strEvolveBld.AppendLine("   if evoState <= int(EvolutionaryMaxState) && evoState >= 0");
            strEvolveBld.AppendLine("   {");
            strEvolveBld.AppendLine(@"      EvolutionaryState = ""{*"" + string(evoState) + ""}""");
            strEvolveBld.AppendLine("      return string(evoState)");
            strEvolveBld.AppendLine("   }");
            strEvolveBld.AppendLine(@"  return ""Failed to Evolve.""");
            strEvolveBld.AppendLine("}");

            int counter = 1;
            evolve.OrderBy(x => x.EvolutionState).ToList().ForEach(x =>
            {
                var evoLetter = FunctionNameUtility.GetFunctionLetter(x.EvolutionState);
                strEvolveBld.AppendLine("function EvolveState" + evoLetter + "() : string");
                strEvolveBld.AppendLine("{");
                strEvolveBld.AppendLine(@"  var evoState = " + "\"" + x.EvolutionState.ToString() + "\"");
                strEvolveBld.AppendLine(@"  var name = " + "\"" + x.Name + "\"");
                strEvolveBld.AppendLine(@"  var description = " + "\"" + x.Description + "\"");
                strEvolveBld.AppendLine(@"  var assetName = " + "\"" + (x.SmartContractAsset == null ? "" : x.SmartContractAsset.Name) + "\"");
                strEvolveBld.AppendLine(@"  var evolveDate = " + "\"" + (x.EvolveDate == null ? "" : x.EvolveDate.Value.Ticks.ToString()) + "\"");
                strEvolveBld.AppendLine(@"  var evolveAtBlock = " + "\"" + (x.EvolveBlockHeight == null ? "" : x.EvolveBlockHeight.Value.ToString()) + "\"");
                strEvolveBld.AppendLine("  return (evoState + " + appendChar + " + name + " + appendChar + " + description + " + appendChar + " + assetName + " + appendChar + " + evolveDate + " + appendChar + " + evolveAtBlock)");
                strEvolveBld.AppendLine("}");


                if(x.SmartContractAsset != null && isReading == false)
                {
                    if(!x.SmartContractAsset.Location.Contains("Asset Folder"))
                    {
                        var result = NFTAssetFileUtility.MoveAsset(x.SmartContractAsset.Location, x.SmartContractAsset.Name, scUID);
                        if (result == false)
                        {
                            //did not copy files
                            failedToSaveAsset = true;
                        }
                    }
                }

                if (x.IsDynamic == true)
                {
                    isDynamic = true;
                    if(x.EvolveBlockHeight != null)
                    {
                        isDynamicBlock = true;
                    }
                    if(x.EvolveDate != null)
                    {
                        isDynamicDate = true;
                    }
                }
                counter += 1;
            });

            if (isDynamic == true)
            {
                strBuild.AppendLine("let EvolveDynamic = true");

                //Creates the DynamicEvolve Method
                //This method is responsible for comparing the block or date
                //and determining which evolve state should be returned.
                strEvolveBld.AppendLine("function DynamicEvolve(evoDate : int, evoBlock : int) : int");
                strEvolveBld.AppendLine("{");
                strEvolveBld.AppendLine("   var evoDateState = 0");
                strEvolveBld.AppendLine("   var evoBlockState = 0");
                if(isDynamicDate != false)
                {
                    strEvolveBld.AppendLine(@"  if(evoDate != 0)");
                    strEvolveBld.AppendLine("   {");
                    strEvolveBld.AppendLine("       evoDateState = DynamicEvolveDate()");
                    strEvolveBld.AppendLine("   }");
                }
                if(isDynamicBlock != false)
                {
                    strEvolveBld.AppendLine(@"  if(evoBlock != 0)");
                    strEvolveBld.AppendLine("   {");
                    strEvolveBld.AppendLine("       evoBlockState = DynamicEvolveBlock(evoBlock)");
                    strEvolveBld.AppendLine("   }");
                }
                
                strEvolveBld.AppendLine("   if(evoDateState == evoBlockState)");
                strEvolveBld.AppendLine("   {");
                strEvolveBld.AppendLine("       return evoDateState");
                strEvolveBld.AppendLine("   }");
                strEvolveBld.AppendLine("   else if(evoDateState > evoBlockState)");
                strEvolveBld.AppendLine("   {");
                strEvolveBld.AppendLine("       return evoDateState");
                strEvolveBld.AppendLine("   }");
                strEvolveBld.AppendLine("   else if(evoDateState < evoBlockState)");
                strEvolveBld.AppendLine("   {");
                strEvolveBld.AppendLine("       return evoBlockState");
                strEvolveBld.AppendLine("   }");
                strEvolveBld.AppendLine("   else");
                strEvolveBld.AppendLine("   {");
                strEvolveBld.AppendLine("       return 0");
                strEvolveBld.AppendLine("   }");
                strEvolveBld.AppendLine("}");

                if(isDynamicDate != false)
                {
                    strEvolveBld.AppendLine("function DynamicEvolveDate() : int");
                    strEvolveBld.AppendLine("{");
                    strEvolveBld.AppendLine("   var stateD = 0");
                    evolve.OrderBy(x => x.EvolutionState).ToList().ForEach(x =>
                    {
                        if(x.EvolveDate != null)
                        {
                            var evoLetter = FunctionNameUtility.GetFunctionLetter(x.EvolutionState);
                            strEvolveBld.AppendLine(@"  var stateDate" + evoLetter + " = dateProc(\"" + x.EvolveDate.Value.Ticks.ToString() +"\")");
                            strEvolveBld.AppendLine("   if(stateDate" + evoLetter +  " == true)");
                            strEvolveBld.AppendLine("   {");
                            strEvolveBld.AppendLine("       stateD = " + x.EvolutionState.ToString());
                            strEvolveBld.AppendLine("   }");

                        }

                    });
                    strEvolveBld.AppendLine("   return stateD");
                    strEvolveBld.AppendLine("}");
                }
                if (isDynamicBlock != false)
                {
                    strEvolveBld.AppendLine("function DynamicEvolveBlock(dynamicBlock : int) : int");
                    strEvolveBld.AppendLine("{");
                    strEvolveBld.AppendLine("   var stateB = 0");
                    evolve.OrderBy(x => x.EvolutionState).ToList().ForEach(x =>
                    {
                        if (x.EvolveBlockHeight != null)
                        {
                            var evoLetter = FunctionNameUtility.GetFunctionLetter(x.EvolutionState);
                            strEvolveBld.AppendLine("   var stateBlock" + evoLetter + " = " + x.EvolveBlockHeight.ToString());
                            strEvolveBld.AppendLine("   if(dynamicBlock >= stateBlock" + evoLetter + ")");
                            strEvolveBld.AppendLine("   {");
                            strEvolveBld.AppendLine("       stateB = " + x.EvolutionState.ToString());
                            strEvolveBld.AppendLine("}");
                        }

                    });
                    strEvolveBld.AppendLine("   return stateB");
                    strEvolveBld.AppendLine("}");
                }
            }
            else
            {
                strBuild.AppendLine("let EvolveDynamic = false");
            }

            if (failedToSaveAsset == true)
            {
                strBuild.Clear();
                strBuild.Append("Failed");
                return (strBuild, strEvolveBld);
            }

            return (strBuild, strEvolveBld);
        }
    }
}
