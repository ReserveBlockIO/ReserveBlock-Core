using LiteDB;
using ReserveBlockCore.Data;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ReserveBlockCore.Models.SmartContracts
{
    public class SmartContractMain
    {
        public string Name { get; set; } //User Defined
        public string Description { get; set; } //User Defined
        public string Address { get; set; } //User Defined
        public SmartContractAsset SmartContractAsset { get; set; } 
        public bool IsPublic { get; set; } //System Set
        public Guid SmartContractUID { get; set; }//System Set
        public string Signature { get; set; }//System Set
        public List<SmartContractFeatures>? Features { get; set; }

        public class SmartContractData
        {
            public static ILiteCollection<SmartContractMain> GetSCs()
            {
                var scs = DbContext.DB_Assets.GetCollection<SmartContractMain>(DbContext.RSRV_ASSETS);
                return scs;
            }

            public static SmartContractMain? GetSmartContract(Guid smartContractUID)
            {
                var scs = GetSCs();
                if(scs != null)
                {
                    var sc = scs.FindOne(x => x.SmartContractUID == smartContractUID);
                    if(sc != null)
                    {
                        return sc;
                    }
                }

                return null;
            }

            public static void SaveSmartContract(SmartContractMain scMain, string scText)
            {
                var scs = GetSCs();

                scs.Insert(scMain);

                SaveSCLocally(scMain, scText);
            }
            public static async void SaveSCLocally(SmartContractMain scMain, string scText)
            {
                try
                {
                    var databaseLocation = Program.IsTestNet != true ? "SmartContracts" : "SmartContractsTestNet";
                    var text = scText;
                    string path = "";
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                        path = homeDirectory + Path.DirectorySeparatorChar + "rbx" + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                    }
                    else
                    {
                        if (Debugger.IsAttached)
                        {
                            path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                        }
                        else
                        {
                            path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + Path.DirectorySeparatorChar + "RBX" + Path.DirectorySeparatorChar + databaseLocation + Path.DirectorySeparatorChar;
                        }
                    }
                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }

                    await File.AppendAllTextAsync(path + scMain.SmartContractUID +".trlm", text);
                }
                catch (Exception ex)
                {

                }
            }
        }

    }
}
