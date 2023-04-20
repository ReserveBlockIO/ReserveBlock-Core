using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.Models;
using ReserveBlockCore.Utilities;
using System.Globalization;
using System.Numerics;

namespace ReserveBlockCore.Services
{
    public static class SignatureService
    {
        public static string CreateSignature(string message, PrivateKey PrivKey, string pubKey)
        {
            try
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
                
                //This is a patch for sigs with 0000 start point.
                if (pubKeyDecoded.Length / 2 == 63)
                {
                    pubKeyDecoded = "00" + pubKeyDecoded;
                }                
                
                var pubKeyByte = HexByteUtility.HexToByte(pubKeyDecoded);
                var publicKey = PublicKey.fromString(pubKeyByte);
                var verifyCheck = Ecdsa.verify(message, Signature.fromBase64(sigScriptArray[0]), publicKey);

                if (verifyCheck != true)
                    return "ERROR";
                return sigScript;
            }
            catch(Exception ex)
            {
                ErrorLogUtility.LogError($"Error with sig signing. Message: {ex.ToString()}", "SignatureService.CreateSignature");
                return "ERROR";
            }
        }

        public static bool VerifySignature(string address, string message, string sigScript)
        {
            try
            {
                if (sigScript == null)
                    return false;

                var sigScriptArray = sigScript.Split('.', 2);
                var pubKeyDecoded = HexByteUtility.ByteToHex(Base58Utility.Base58Decode(sigScriptArray[1]));

                //This is a patch for sigs with 0000 start point. remove lock after update has been achieved.
                if (pubKeyDecoded.Length / 2 == 63)
                {
                    pubKeyDecoded = "00" + pubKeyDecoded;
                }

                var pubKeyByte = HexByteUtility.HexToByte(pubKeyDecoded);
                var publicKey = PublicKey.fromString(pubKeyByte);

                var _PublicKey = "04" + ByteToHex(publicKey.toString());
                var _Address = address.StartsWith("xRBX") ? ReserveAccount.GetHumanAddress(_PublicKey) : AccountData.GetHumanAddress(_PublicKey);

                if (address != _Address)
                {
                    return false;
                }

                return Ecdsa.verify(message, Signature.fromBase64(sigScriptArray[0]), publicKey);
            }
            catch(Exception ex)
            {
                ErrorLogUtility.LogError($"Error with sig verify. Message: {ex.ToString()}", "SignatureService.VerifySignature");
                return false;
            }
            
        }
        private static string ByteToHex(byte[] pubkey)
        {
            return Convert.ToHexString(pubkey).ToLower();
        }

        public static string ValidatorSignature(string message)
        {
            var validatorAccount = AccountData.GetSingleAccount(Globals.ValidatorAddress);

            BigInteger b1 = BigInteger.Parse(validatorAccount.GetKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
            PrivateKey privateKey = new PrivateKey("secp256k1", b1);

            return SignatureService.CreateSignature(message, privateKey, validatorAccount.PublicKey);
        }

        public static string AdjudicatorSignature(string message)
        {
            var account = Globals.AdjudicateAccount;            
            return SignatureService.CreateSignature(message, Globals.AdjudicatePrivateKey, account.PublicKey);
        }
    }
}
