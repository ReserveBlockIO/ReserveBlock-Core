using Newtonsoft.Json.Linq;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using System.Text;

namespace ReserveBlockCore.Services
{
    public static class SmartContractReaderService
    {
        public static async Task<(string, SmartContractMain)> ReadSmartContract(SmartContractMain scMain)
        {
            var scUID = scMain.SmartContractUID;
            var features = scMain.Features;
            var featuresList = scMain.Features;
            var signature = scMain.Signature;
            StringBuilder strRoyaltyBld = new StringBuilder();
            StringBuilder strEvolveBld = new StringBuilder();

            var appendChar = "\"|->\"";

            var scAsset = scMain.SmartContractAsset;
            StringBuilder strBuild = new StringBuilder();

            if (featuresList != null)
            {
                var Flist = new List<SmartContractFeatures>();
                if (featuresList.Count == 1)
                {
                    var feature = featuresList.First();
                    if (feature.FeatureName == FeatureName.Royalty)
                    {
                        var royalty = ((RoyaltyFeature)feature.FeatureFeatures);
                        feature.FeatureFeatures = royalty;

                        Flist.Add(feature); 
                        
                        //create royalty code block
                        strBuild.AppendLine("let RoyaltyType = \"" + ((int)royalty.RoyaltyType).ToString() + "\"");
                        strBuild.AppendLine("let RoyaltyAmount = \"" + royalty.RoyaltyAmount.ToString() + "\"");
                        strBuild.AppendLine("let RoyaltyPayToAddress = \"" + royalty.RoyaltyPayToAddress + "\"");

                        strRoyaltyBld.AppendLine("function GetRoyaltyData(royaltyType  : string, royaltyAmount : string, royaltyPayToAddress : string) : string");
                        strRoyaltyBld.AppendLine("{");
                        strRoyaltyBld.AppendLine("return (royaltyType + " + appendChar + " + royaltyAmount + " + appendChar + " + royaltyPayToAddress)");
                        strRoyaltyBld.AppendLine("}");
                    }
                }
                else
                {
                    int count = 1;
                    int featureCount = featuresList.Count();
                    featuresList.ForEach(x =>
                    {
                        if (x.FeatureName == FeatureName.Royalty)
                        {
                            var royalty = ((RoyaltyFeature)x.FeatureFeatures);
                            x.FeatureFeatures = royalty;

                            Flist.Add(x);
                            //create royalty code block
                            strBuild.AppendLine("let RoyaltyType = \"" + ((int)royalty.RoyaltyType).ToString() + "\"");
                            strBuild.AppendLine("let RoyaltyAmount = \"" + royalty.RoyaltyAmount.ToString() + "\"");
                            strBuild.AppendLine("let RoyaltyPayToAddress = \"" + royalty.RoyaltyPayToAddress + "\"");

                            strRoyaltyBld.AppendLine("function GetRoyaltyData(royaltyType  : string, royaltyAmount : string, royaltyPayToAddress : string) : string");
                            strRoyaltyBld.AppendLine("{");
                            strRoyaltyBld.AppendLine("return (royaltyType + " + appendChar + " + royaltyAmount + " + appendChar + " + royaltyPayToAddress");
                            strRoyaltyBld.AppendLine("}");
                        }

                    });
                }
                scMain.Features = Flist;

            }


            //NFT Main Data
            strBuild.AppendLine(("let Name = \"{#NFTName}\"").Replace("{#NFTName}", scMain.Name));
            strBuild.AppendLine(("let Description = \"{#Description}\"").Replace("{#Description}", scMain.Description));
            strBuild.AppendLine(("let Address = \"{#Address}\"").Replace("{#Address}", scMain.Address));
            strBuild.AppendLine(("let SmartContractUID = \"" + scUID + "\""));
            strBuild.AppendLine(("let Signature = \"" + signature + "\""));
            strBuild.AppendLine(("let Features = \"" + features + "\""));

            //NFT asset Data
            strBuild.AppendLine(("let Extension = \"" + scAsset.Extension + "\""));
            strBuild.AppendLine(("let FileSize = \"" + scAsset.FileSize.ToString() + "\""));
            strBuild.AppendLine(("let Location = \"" + scAsset.Location + "\""));
            strBuild.AppendLine(("let FileName = \"" + scAsset.Name + "\""));
            strBuild.AppendLine("function NftMain()");
            strBuild.AppendLine("{");
            strBuild.AppendLine("send(GetNFTData(Name, Description, Address))");
            strBuild.AppendLine("send(GetNFTAssetData(FileName, Location, FileSize, Extension))");
            if (featuresList != null)
            {
                if (featuresList.Exists(x => x.FeatureName == FeatureName.Royalty))
                {
                    strBuild.AppendLine("send(GetRoyaltyData(RoyaltyType, RoyaltyAmount, RoyaltyPayToAddress))");
                }
            }
            strBuild.AppendLine("}");
            strBuild.AppendLine("function GetNFTData(name : string, desc : string, addr : string) : string");
            strBuild.AppendLine("{");
            strBuild.AppendLine("return name + " + appendChar + " + desc + " + appendChar + " + addr");
            strBuild.AppendLine("}");
            strBuild.AppendLine("function GetNFTAssetData(fileName : string, loc : string, fileSize : string, ext : string) : string");
            strBuild.AppendLine("{");
            strBuild.AppendLine("return (fileName + " + appendChar + " + loc + " + appendChar + " + fileSize + " + appendChar + " + ext)");
            strBuild.AppendLine("}");

            if (featuresList != null)
            {
                if (featuresList.Exists(x => x.FeatureName == FeatureName.Royalty))
                {
                    strBuild.Append(strRoyaltyBld);
                }
            }

            strBuild.AppendLine("NftMain()");

            var scText = strBuild.ToString();

            return (scText, scMain);

        }
    }
}
