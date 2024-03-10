using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using System.Text;

namespace ReserveBlockCore.Bitcoin.Services
{
    public class SignatureService
    {
        public static string CreateSignature(string privateKeyHex, string message)
        {
            try
            {
                byte[] privateKeyBytes = privateKeyHex.HexToByteArray();
                Key privateKey = new Key(privateKeyBytes);

                byte[] messageBytes = Encoding.UTF8.GetBytes(message);

                ECDSASignature signature = privateKey.Sign(new uint256(Hashes.DoubleSHA256(messageBytes)));

                var hexSig = signature.ToDER().ToStringHex();
                var hexPubKey = privateKey.PubKey.ToString();

                var fullSig = $"{hexSig}.{hexPubKey}";

                return fullSig;
            }
            catch { }

            return "F";
        }

        public static bool VerifySignature(string message, string signatureHex)
        {
            try
            {
                // Convert the original message to bytes
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);

                // Hash the message bytes using double SHA-256
                uint256 messageHash = new uint256(Hashes.DoubleSHA256(messageBytes));

                var signatureArray = signatureHex.Split(".");
                var signature = signatureArray[0];
                var publicKeyHex = signatureArray[1];

                // Convert the signature from its string representation to an ECDSASignature object
                byte[] derSig = Encoders.Hex.DecodeData(signature);
                ECDSASignature signatureObject = new ECDSASignature(derSig);

                // Obtain the public key corresponding to the private key that was used to sign the message
                PubKey publicKey = new PubKey(publicKeyHex);

                // Verify the signature using the public key and message hash
                bool isSignatureValid = publicKey.Verify(messageHash, signatureObject);

                // Output the result
                if (isSignatureValid)
                    return true;
            }
            catch { }

            return false;
        }
    }
}
