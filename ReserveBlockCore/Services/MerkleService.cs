using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ReserveBlockCore.Services
{
    internal class MerkleService
    {
        public static string CreateMerkleRoot(string[] trxsHash)
        {

            while (true)
            {
                if (trxsHash.Length == 0)
                    return string.Empty;

                if (trxsHash.Length == 1)
                    return trxsHash[0];

                List<string> HashList = new List<string>();

                int len = (trxsHash.Length % 2 != 0) ? trxsHash.Length - 1 : trxsHash.Length;

                for (int i = 0; i < len; i += 2)
                {
                    HashList.Add(DoubleHash(trxsHash[i], trxsHash[i + 1]));
                }

                if (len < trxsHash.Length)
                {
                    HashList.Add(DoubleHash(trxsHash[^1], trxsHash[^1]));
                }

                trxsHash = HashList.ToArray();
            }
        }

        static string DoubleHash(string leafA, string leafB)
        {
            byte[] leaf1_Byte = HashingService.HexToBytes(leafA);
            //Array.Reverse(leaf1Byte);

            byte[] leaf2_Byte = HashingService.HexToBytes(leafB);
            //Array.Reverse(leaf2Byte);

            var conHash = leaf1_Byte.Concat(leaf2_Byte).ToArray();
            SHA256 sha256 = SHA256.Create();
            byte[] sendHash = sha256.ComputeHash(sha256.ComputeHash(conHash));

            //Array.Reverse(sendHash);

            return HashingService.BytesToHex(sendHash).ToLower();
        }
    }
}
