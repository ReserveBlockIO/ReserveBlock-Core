using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ReserveBlockCore.Services
{
    internal class HashingService
    {
        public static string GenerateHash(string data)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            byte[] hash = SHA256.Create().ComputeHash(bytes);
            return BytesToHex(hash);
        }
        public static string GenerateHashHex(string hex)
        {
            byte[] bytes = HexToBytes(hex);
            byte[] hash = SHA256.Create().ComputeHash(bytes);
            return BytesToHex(hash);
        }
        public static string BytesToHex(byte[] bytes)
        {
            return Convert.ToHexString(bytes).ToLower();
        }
        public static byte[] HexToBytes(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                .ToArray();
        }
    }
}
