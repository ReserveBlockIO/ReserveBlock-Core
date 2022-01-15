using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ReserveBlockCore.Utilities
{
    internal class DecryptFileUtil
    {
        public const int iterations = 1042; // Recommendation is >= 1000.
        public static Stream DecryptFile(Stream source, string password)
        {
            var ArrayLength = source.ReadByte();
            if (ArrayLength == -1) throw new Exception("Salt length not found");
            byte[] SaltBytes = new byte[ArrayLength];
            var readBytes = source.Read(SaltBytes, 0, ArrayLength);
            if (readBytes != ArrayLength) throw new Exception("No support for multiple reads");

            ArrayLength = source.ReadByte();
            if (ArrayLength == -1) throw new Exception("Salt length not found");
            byte[] IVBytes = new byte[ArrayLength];
            readBytes = source.Read(IVBytes, 0, ArrayLength);
            if (readBytes != ArrayLength) throw new Exception("No support for multiple reads");

            AesManaged aes = new AesManaged();
            aes.BlockSize = aes.LegalBlockSizes[0].MaxSize;
            aes.KeySize = aes.LegalKeySizes[0].MaxSize;
            aes.IV = IVBytes;

            Rfc2898DeriveBytes key = new Rfc2898DeriveBytes(password, SaltBytes, iterations);
            aes.Key = key.GetBytes(aes.KeySize / 8);

            aes.Mode = CipherMode.CBC;
            ICryptoTransform transform = aes.CreateDecryptor(aes.Key, aes.IV);

            var cryptoStream = new CryptoStream(source, transform, CryptoStreamMode.Read);
            return cryptoStream;
        }
    }
}
