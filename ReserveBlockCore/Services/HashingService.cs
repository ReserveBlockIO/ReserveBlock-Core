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
		private static string CalculateHash(string inputString)
		{
			SHA256 sha256 = SHA256Managed.Create();
			byte[] bytes = Encoding.UTF8.GetBytes(inputString);
			byte[] hash = sha256.ComputeHash(bytes);
			return GetStringFromHash(hash);
		}

		private static string GetStringFromHash(byte[] hash)
		{
			StringBuilder result = new StringBuilder();
			for (int i = 0; i < hash.Length; i++)
			{
				result.Append(hash[i].ToString("X2"));
			}
			return result.ToString();
		}
	}
}
