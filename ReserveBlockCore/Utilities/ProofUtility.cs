using ReserveBlockCore.Data;
using ReserveBlockCore.Extensions;
using ReserveBlockCore.Models;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace ReserveBlockCore.Utilities
{
    public class ProofUtility
    {
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public static async Task<List<Proof>> GenerateProofs(string address, string publicKey, long blockHeight, bool firstProof)
        {
            List<Proof> proofs = new List<Proof>();
            var blockHeightStart = blockHeight + 1;
            var finalHeight = blockHeightStart + 1;

            if (firstProof)
            {
                finalHeight = blockHeightStart + 3;//if first proof of the day then push it out.
            }
            for (long h = blockHeightStart; h <= finalHeight; h++)
            {
                var proof = await CreateProof(address, publicKey, h);
                if(proof.Item1 != 0 && !string.IsNullOrEmpty(proof.Item2))
                {
                    Proof _proof = new Proof {
                        Address = address,
                        BlockHeight = h,
                        ProofHash = proof.Item2,
                        PublicKey = publicKey,
                        VRFNumber = proof.Item1,
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
            //if (Globals.BlockHashes.Count >= 35)
            //{
            //    var height = blockHeight - 7;
            //    seed = seed + Globals.BlockHashes[height].ToString();
            //}
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
                //if (Globals.BlockHashes.Count >= 35)
                //{
                //    var height = blockHeight - 7;
                //    seed = seed + Globals.BlockHashes[height].ToString();
                //}

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
                //if (Globals.BlockHashes.Count >= 35)
                //{
                //    var height = blockHeight - 7;
                //    seed = seed + Globals.BlockHashes[height].ToString();
                //}
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
                            //Skip proof since winner was already found.
                            if (Globals.FinalizedWinner.ContainsKey(proof.BlockHeight))
                                continue;

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
                            else
                            {
                                Globals.BackupProofs.TryGetValue(proof.BlockHeight, out var backupProofs);
                                if (backupProofs != null)
                                {
                                    var hasProof = backupProofs.Exists(x => x.Address == proof.Address && x.BlockHeight == proof.BlockHeight);
                                    if (!hasProof)
                                    {
                                        backupProofs.Add(currentWinningProof.Value);
                                        Globals.BackupProofs[proof.BlockHeight] = backupProofs.OrderBy(x => x.VRFNumber).ToList();
                                    }
                                }
                                else
                                {
                                    Globals.BackupProofs.TryAdd(proof.BlockHeight, new List<Proof> { proof });
                                }
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

        public static async Task AbandonProof(long height, string supposeValidatorAddress)
        {
            await _semaphore.WaitAsync();
            try
            {
                int maxRetries = 10;
                int counter = 0;
                while (!Globals.FinalizedWinner.TryRemove(height, out _) && counter < maxRetries)
                {
                    counter++;
                    await Task.Delay(100);
                }

                counter = 0;
                var blockDiff = (TimeUtil.GetTime() - Globals.LastBlockAddedTimestamp);
                if (blockDiff >= 120)
                {
                    Globals.FailedValidators.Clear();
                    Globals.FailedValidators = new ConcurrentDictionary<string, long>();
                }
                    
                Globals.FailedValidators.TryAdd(supposeValidatorAddress, height + 1);

                var proofList = Globals.WinningProofs;
                foreach (var proof in proofList)
                {
                    if (proof.Value.Address == supposeValidatorAddress)
                    {
                        while (!Globals.WinningProofs.TryRemove(proof.Key, out _) && counter < maxRetries)
                        {
                            counter++;
                            await Task.Delay(100);
                        }
                        counter = 0;
                        Globals.BackupProofs.TryGetValue(proof.Key, out var backupProofList);
                        if (backupProofList != null)
                        {
                            var newProof = backupProofList.Where(x => x.Address != supposeValidatorAddress).OrderBy(x => x.VRFNumber).FirstOrDefault();
                            if (newProof != null)
                            {
                                while (!Globals.WinningProofs.TryAdd(proof.Key, newProof) && counter < maxRetries)
                                {
                                    counter++;
                                    await Task.Delay(100);
                                }
                                counter = 0;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {

            }
            finally
            {
                _semaphore.Release();
            }
            ValidatorLogUtility.Log($"Validator {supposeValidatorAddress} failed to produce block for height: {height} ", "ProofUtility.AbandonProof()");
        }

        public static async Task CleanupProofs()
        {
            var blockHeight = Globals.LastBlock.Height;

            var keysToRemove = Globals.WinningProofs.Where(x => x.Key < blockHeight).ToList();

            var backupKeysToRemove = Globals.BackupProofs.Where(x => x.Key < blockHeight).ToList();

            foreach (var key in keysToRemove)
            {
                try
                {
                    Globals.WinningProofs.TryRemove(key.Key, out _);
                }
                catch { }
            }

            foreach (var key in backupKeysToRemove)
            {
                try
                {
                    Globals.BackupProofs.TryRemove(key.Key, out _);
                }
                catch { }
            }

            if(Globals.FailedValidators.Count() > 0)
            {
                try
                {
                    var failedValsToRemove = Globals.FailedValidators.Where(x => x.Value < blockHeight).ToList();
                    if (failedValsToRemove?.Count() > 0)
                    {
                        foreach (var val in failedValsToRemove)
                        {
                            Globals.FailedValidators.TryRemove(val.Key, out _);
                        }
                    }
                }
                catch { }
            }
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
