using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;

namespace ReserveBlockCore.Models
{
    public class Signer
    {
        public string Id { get; set; }
        public string Address { get; set; }
        public long StartHeight { get; set; }
        public long? EndHeight { get; set; }
        public static LiteDB.ILiteCollection<Signer> GetSigners()
        {
            try
            {
                return DbContext.DB_Config.GetCollection<Signer>(DbContext.RSRV_SIGNER);
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError(ex.ToString(), "AccountData.GetSigners()");
                return null;
            }
        }

        public static ConcurrentDictionary<(string Address, long StartHeight), long?> Signers;      
        public static void UpdateSigningAddresses()
        {
            var Height = Globals.LastBlock.Height;
            var NewSigners = Signers.Where(x => x.Key.StartHeight <= Height && (x.Value == null || x.Value >= Height))
               .Select(x => x.Key.Address)
               .ToHashSet();
            foreach (var signer in NewSigners)
                Globals.Signers[signer] = true;
            foreach (var singer in Globals.Signers.Keys)
                if (!NewSigners.Contains(singer))
                    Globals.Signers.TryRemove(singer, out _);

            lock (Globals.SignerCacheLock)
            {
                Globals.SignerCache = JsonConvert.SerializeObject(Signer.Signers.Select(x => new
                {
                    x.Key.StartHeight,
                    x.Key.Address,
                    EndHeight = x.Value
                }));

                Globals.IpAddressCache = JsonConvert.SerializeObject(Globals.AdjBench.Select(x => new
                {
                    x.Value.RBXAddress,
                    x.Value.IPAddress                    
                }));
            }

            if (Globals.AdjudicateAccount == null)
            {
                var Accounts = AccountData.GetAccounts().FindAll().ToArray();
                Globals.AdjudicateAccount = Accounts.Where(x => Globals.Signers.ContainsKey(x.Address)).FirstOrDefault();
                if (Globals.AdjudicateAccount != null)
                {
                    BigInteger b1 = BigInteger.Parse(Globals.AdjudicateAccount.GetKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
                    Globals.AdjudicatePrivateKey = new PrivateKey("secp256k1", b1);
                    _ = Task.Run(StartupService.ConnectToConsensusNodes);
                }
            }
        }        
        public static int NumSigners()
        {
            return Globals.Signers.Count;
        }
        public static int Majority()
        {
            return NumSigners() / 2 + 1;
        }
    }
}
