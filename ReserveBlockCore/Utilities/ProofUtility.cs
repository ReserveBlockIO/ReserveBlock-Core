using ReserveBlockCore.Extensions;
using ReserveBlockCore.Models;
using System.Security.Cryptography;
using System.Text;

namespace ReserveBlockCore.Utilities
{
    public class ProofUtility
    {
        public static async Task<List<Proof>> GenerateProofs(string address, string publicKey, long blockHeight, bool firstProof)
        {
            List<Proof> proofs = new List<Proof>();
            var blockHeightStart = blockHeight + 1;
            if(firstProof)
            {
                blockHeightStart = blockHeight + 144;//if first proof of the day then push it out.
            }
            var finalHeight = blockHeightStart + 144;
            for(long h = blockHeightStart; h <= finalHeight; h++)
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

            Globals.LastProofBlockheight = finalHeight;

            return proofs;
        }

        public static async Task<(uint, string)> CreateProof(string address, string publicKey, long blockHeight)
        {
            
            uint vrfNum = 0;
            var proof = "";
            // Random seed
            string seed = publicKey + blockHeight.ToString();
            if (Globals.BlockHashes.Count >= 35)
            {
                var height = blockHeight - 7;
                seed = seed + Globals.BlockHashes[height].ToString();
            }
            //add previous block hash here!
            
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
                if (Globals.BlockHashes.Count >= 35)
                {
                    var height = blockHeight - 7;
                    seed = seed + Globals.BlockHashes[height].ToString();
                }

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

        public static bool VerifyProof(string publicKey, long blockHeight, string proofHash)
        {
            try
            {
                uint vrfNum = 0;
                var proof = "";
                // Random seed
                string seed = publicKey + blockHeight.ToString();
                if (Globals.BlockHashes.Count >= 35)
                {
                    var height = blockHeight - 7;
                    seed = seed + Globals.BlockHashes[height].ToString();
                }
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

        public static async Task SortProofs(List<Proof> proofs, bool isWinnerList = false)
        {
            try
            {
                var checkListForOneVal = proofs.GroupBy(p => p.Address).Count();

                //More than one val found in proof list. This should not happen unless cheating occurs. 
                if (checkListForOneVal > 1 && !isWinnerList)
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
                                Globals.BackupProofs.TryGetValue(proof.BlockHeight, out var backupProofs);
                                if (backupProofs != null)
                                {
                                    var hasProof = backupProofs.Exists(x => x.Address == currentWinningProof.Value.Address && x.BlockHeight == currentWinningProof.Value.BlockHeight);
                                    if(!hasProof)
                                    {
                                        backupProofs.Add(currentWinningProof.Value);
                                        Globals.BackupProofs[proof.BlockHeight] = backupProofs.OrderBy(x => x.VRFNumber).ToList();
                                    }
                                }
                                else
                                {
                                    Globals.BackupProofs.TryAdd(currentWinningProof.Value.BlockHeight, new List<Proof> { currentWinningProof.Value });
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
