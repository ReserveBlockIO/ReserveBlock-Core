using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ReserveBlockCore.BIP32
{
    public class BIP32
    {
        readonly string curve = "ed25519 seed";
        readonly uint hardenedOffset = 0x80000000;
        readonly BigInteger curveN = BigInteger.Parse("00fffffffffffffffffffffffffffffffebaaedce6af48a03bbfd25e8cd0364141", NumberStyles.AllowHexSpecifier);

        public (byte[] Key, byte[] ChainCode) GetMasterKeyFromSeed(string seed)
        {
            using (HMACSHA512 hmacSha512 = new HMACSHA512(Encoding.UTF8.GetBytes(curve)))
            {
                var i = hmacSha512.ComputeHash(seed.HexToByteArray());

                var il = i.Slice(0, 32);
                var ir = i.Slice(32);

                return (Key: il, ChainCode: ir);
            }
        }
        private (byte[] Key, byte[] ChainCode) GetChildKeyDerivation(byte[] key, byte[] chainCode, uint index)
        {
            BigEndianBuffer buffer = new BigEndianBuffer();

            buffer.Write(new byte[] { 0 });
            buffer.Write(key);
            buffer.WriteUInt(index);

            using (HMACSHA512 hmacSha512 = new HMACSHA512(chainCode))
            {
                var i = hmacSha512.ComputeHash(buffer.ToArray());

                var il = i.Slice(0, 32);
                var ir = i.Slice(32);

                var leftSide = BigInteger.Parse("00" + il.ToStringHex(), NumberStyles.AllowHexSpecifier);
                var parent = BigInteger.Parse("00" + key.ToStringHex(), NumberStyles.AllowHexSpecifier);
                var childPrivateKeyLength = ((leftSide + parent) % curveN).ToString("x").Length;
                var childPrivateKeyHex = ((leftSide + parent) % curveN).ToString("x");
                var childPrivateKey = childPrivateKeyHex.HexToByteArray();

                if (childPrivateKeyLength > 64)
                {
                    var childPrivateKeyTrimmed = childPrivateKeyHex.Remove(0, 1);
                    childPrivateKey = childPrivateKeyTrimmed.HexToByteArray();
                }

                return (Key: childPrivateKey, ChainCode: ir);
            }
        }

        private bool IsValidPath(string path)
        {
            var regex = new Regex("^m(\\/[0-9]+')+$");

            if (!regex.IsMatch(path))
                return false;

            var valid = !(path.Split('/')
                .Slice(1)
                .Select(a => a.Replace("'", ""))
                .Any(a => !Int32.TryParse(a, out _)));

            return valid;
        }


        public (byte[] Key, byte[] ChainCode) DerivePath(string path, string seed)
        {
            if (!IsValidPath(path))
                throw new FormatException("Invalid derivation path");

            var masterKeyFromSeed = GetMasterKeyFromSeed(seed);

            var segments = path
                .Split('/')
                .Slice(1)
                .Select(a => a.Replace("'", ""))
                .Select(a => Convert.ToUInt32(a, 10));

            var results = segments
                .Aggregate(masterKeyFromSeed, (mks, next) => GetChildKeyDerivation(mks.Key, mks.ChainCode, next + hardenedOffset));

            return results;
        }
    }

    
}
