using ReserveBlockCore.Extensions;
using ReserveBlockCore.Models;
using System.Security.Cryptography;
using System.Text;

namespace ReserveBlockCore.Utilities
{
    public class ProofUtility
    {
        public static async Task<List<Proof>> GenerateProofs(string address, string publicKey, long blockHeight)
        {
            List<Proof> proofs = new List<Proof>();
            var finalHeight = blockHeight + 144;
            for(long h = blockHeight; h <= finalHeight; h++)
            {
                var proof = await CreateProof(address, publicKey, h);
                if(proof.Item1 != 0 && !string.IsNullOrEmpty(proof.Item2))
                {
                    Proof _proof = new Proof { 
                        Address = address,
                        BlockHeight = h,
                        ProofHash = proof.Item2,
                        PublicKey = publicKey,
                        VRFNumber = proof.Item1
                    };

                    proofs.Add(_proof);
                }
            }

            return proofs;
        }

        public static async Task<(uint, string)> CreateProof(string address, string publicKey, long blockHeight)
        {
            
            uint vrfNum = 0;
            var proof = "";
            // Random seed
            string seed = publicKey + blockHeight.ToString();
            
            // Convert the combined input to bytes (using UTF-8 encoding)
            byte[] combinedBytes = Encoding.UTF8.GetBytes(seed);

            // Calculate a hash using SHA256
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(combinedBytes);

                //Produces non-negative by shifting and masking 
                int randomBytesAsInt = BitConverter.ToInt32(hashBytes, 0);
                uint nonNegativeRandomNumber = (uint)(randomBytesAsInt & 0x7FFFFFFF);

                vrfNum = nonNegativeRandomNumber;
                proof = ProofUtility.CalculateSHA256Hash(seed + vrfNum.ToString());
            }

            return (vrfNum, proof);
        }

        public static async Task<bool> VerifyProofAsync(string publicKey, long blockHeight, string proofHash)
        {
            try
            {
                uint vrfNum = 0;
                var proof = "";
                // Random seed
                string seed = publicKey + blockHeight.ToString();

                // Convert the combined input to bytes (using UTF-8 encoding)
                byte[] combinedBytes = Encoding.UTF8.GetBytes(seed);

                // Calculate a hash using SHA256
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] hashBytes = sha256.ComputeHash(combinedBytes);

                    //Produces non-negative by shifting and masking 
                    int randomBytesAsInt = BitConverter.ToInt32(hashBytes, 0);
                    uint nonNegativeRandomNumber = (uint)(randomBytesAsInt & 0x7FFFFFFF);

                    vrfNum = nonNegativeRandomNumber;
                    proof = ProofUtility.CalculateSHA256Hash(seed + vrfNum.ToString());

                    if (proof == proofHash)
                        return true;
                }

                return false;
            }
            catch { return false; }
            
        }

        public static bool VerifyProofSync(string publicKey, long blockHeight, string proofHash)
        {
            try
            {
                uint vrfNum = 0;
                var proof = "";
                // Random seed
                string seed = publicKey + blockHeight.ToString();

                // Convert the combined input to bytes (using UTF-8 encoding)
                byte[] combinedBytes = Encoding.UTF8.GetBytes(seed);

                // Calculate a hash using SHA256
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] hashBytes = sha256.ComputeHash(combinedBytes);

                    //Produces non-negative by shifting and masking 
                    int randomBytesAsInt = BitConverter.ToInt32(hashBytes, 0);
                    uint nonNegativeRandomNumber = (uint)(randomBytesAsInt & 0x7FFFFFFF);

                    vrfNum = nonNegativeRandomNumber;
                    proof = ProofUtility.CalculateSHA256Hash(seed + vrfNum.ToString());

                    if (proof == proofHash)
                        return true;
                }

                return false;
            }
            catch { return false; }

        }

        public static async Task SortProofs(List<Proof> proofs)
        {
            try
            {
                var checkListForOneVal = proofs.GroupBy(p => p.Address).Count();

                //More than one val found in proof list. This should not happen unless cheating occurs. 
                if (checkListForOneVal > 1)
                    return;

                var badProofFound = proofs.Where(x => !x.VerifyProof()).Count() > 0 ? true : false;

                //If any proofs are bad the entire list is ignored.
                if (badProofFound)
                    return;

                var finalProof = new Proof();
                foreach (var proof in proofs)
                {
                    var currentWinningProof = Globals.WinningProofs.Where(x => x.Key == proof.BlockHeight).FirstOrDefault();
                    if (currentWinningProof.Value != null)
                    {
                        if (proof.VerifyProof())
                        {
                            //Closer to zero wins.
                            if (currentWinningProof.Value.VRFNumber > proof.VRFNumber)
                            {
                                var backupProofs = Globals.BackupProofs[proof.BlockHeight];
                                if (backupProofs != null)
                                {
                                    backupProofs.Add(currentWinningProof.Value);
                                    Globals.BackupProofs[proof.BlockHeight] = backupProofs.OrderBy(x => x.VRFNumber).ToList();
                                }
                                //Update winning proof with new proof if the value is greater.
                                Globals.WinningProofs[proof.BlockHeight] = proof;
                            }
                        }
                        else
                        {
                            //stop checking due to proof failure. This should never happen unless a rigged proof is entered. 
                            return;
                        }
                    }
                    else
                    {
                        //No proof found, so add first one found.
                        if (proof.VerifyProof())
                            Globals.WinningProofs.TryAdd(proof.BlockHeight, proof);
                    }

                    finalProof = proof;
                }


                //Updates the network val with latest list to ensure spamming doesn't happen.
                var networkVal = Globals.NetworkValidators.TryGet(finalProof.Address);
                if (networkVal != null)
                {
                    networkVal.LastBlockProof = finalProof.BlockHeight;
                    Globals.NetworkValidators[finalProof.Address] = networkVal;
                }
            }
            catch { return; }
        }

        public static string CalculateSHA256Hash(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hashBytes);
            }
        }
    }
}
