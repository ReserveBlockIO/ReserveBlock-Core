using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Services
{
    public static class SignatureService
    {
        public static string CreateSignature(string message, PrivateKey PrivKey, string pubKey)
        {
            //1. Get signature with message and private key
            Signature signature = Ecdsa.sign(message, PrivKey);

            //2. Base64 the outputted signature
            var sigBase64 = signature.toBase64();

            //3. Base58 public key and remove '04' if it was appended.
            var pubKeyEncoded = Base58Utility.Base58Encode(HexByteUtility.HexToByte(pubKey.Remove(0, 2)));

            //4. Concat the base64 sig and the base58 public with a period '.'
            var sigScript = sigBase64 + "." + pubKeyEncoded;

            //5. validate new signature
            var sigScriptArray = sigScript.Split('.', 2);
            var pubKeyDecoded = HexByteUtility.ByteToHex(Base58Utility.Base58Decode(sigScriptArray[1]));
            var pubKeyByte = HexByteUtility.HexToByte(pubKeyDecoded);
            var publicKey = PublicKey.fromString(pubKeyByte);
            var verifyCheck = Ecdsa.verify(message, Signature.fromBase64(sigScriptArray[0]), publicKey);

            if (verifyCheck != true)
                return "ERROR";
            return sigScript;
        }

        public static bool VerifySignature(string address, string message, string sigScript)
        {
            var sigScriptArray = sigScript.Split('.', 2);
            var pubKeyDecoded = HexByteUtility.ByteToHex(Base58Utility.Base58Decode(sigScriptArray[1]));
            var pubKeyByte = HexByteUtility.HexToByte(pubKeyDecoded);
            var publicKey = PublicKey.fromString(pubKeyByte);

            var _PublicKey = "04" + ByteToHex(publicKey.toString());
            var _Address = AccountData.GetHumanAddress(_PublicKey);

            if(address != _Address)
            {
                return false;
            }

            return Ecdsa.verify(message, Signature.fromBase64(sigScriptArray[0]), publicKey);
        }
        private static string ByteToHex(byte[] pubkey)
        {
            return Convert.ToHexString(pubkey).ToLower();
        }
    }
}
