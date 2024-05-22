using ImageMagick;
using NBitcoin;
using Newtonsoft.Json;
using ReserveBlockCore.Models;
using System;
using System.Security.Principal;

namespace ReserveBlockCore.Services
{
    public class ArbiterService
    {
        public static async Task<(string, string)> GetTokenizationDetails(string accountAddress)
        {
            var myList = Globals.Arbiters;
            var rnd = new Random();
            myList = myList.OrderBy(x => rnd.Next()).ToList();

            List<PubKey> publicKeys = new List<PubKey>();
            List<ArbiterProof> proofs = new List<ArbiterProof>();

            List<Models.Arbiter> randomArbs = myList.Take(Globals.TotalArbiterParties).ToList();

            foreach (var arbiter in randomArbs)
            {
                using (var client = Globals.HttpClientFactory.CreateClient())
                {
                    try
                    {
                        string url = $"http://{arbiter.IPAddress}:{Globals.ArbiterPort}/depositaddress/{accountAddress}";

                        var response = await client.GetAsync(url);
                        if (response == null)
                            return ("FAIL", "Response was null.");

                        if (response.StatusCode != System.Net.HttpStatusCode.OK)
                            return ("FAIL", $"Bad Status Code: {response.StatusCode}");

                        var responseContent = await response.Content.ReadAsStringAsync();
                        if (responseContent == null)
                            return ("FAIL", "Resposne Content was null.");
                                
                        var arbResponse = JsonConvert.DeserializeObject<ArbiterResponse>(responseContent);
                        if (arbResponse == null)
                            return ("FAIL", "Arbiter response was null");

                        var sigValid = SignatureService.VerifySignature(arbiter.SigningAddress, arbResponse.PublicKey, arbResponse.Signature);

                        if(!sigValid)
                            return ("FAIL", "Invalid Signature Proof from Arbiter.");

                        publicKeys.Add(new PubKey(arbResponse.PublicKey));
                        proofs.Add(new ArbiterProof { PublicKey = arbResponse.PublicKey, Signature = arbResponse.Signature, SigningAddress = arbiter.SigningAddress });
                    }
                    catch { }
                }
            }

            if(publicKeys.Count() != Globals.TotalArbiterParties)
                return ("FAIL", "Failed to total number of arbiters needed.");

            try
            {
                var depositAddress = CreateDepositAddress(publicKeys);
                var proofJson = JsonConvert.SerializeObject(proofs);

                return (depositAddress, proofJson);
            }
            catch { }

            return ("FAIL", "Unknown Error.");
        }

        public static async Task GetArbiterSigningAddress()
        {
            await Task.Delay(10000);
            var arbList = Globals.Arbiters.ToList();
                
            foreach (var arbiter in arbList)
            {
                using (var client = Globals.HttpClientFactory.CreateClient())
                {
                    try
                    {
                        string url = $"http://{arbiter.IPAddress}:{Globals.ArbiterPort}/getsigneraddress";

                        var response = await client.GetAsync(url).WaitAsync(new TimeSpan(0,0,3));
                        if (response == null)
                            return;

                        if (response.StatusCode != System.Net.HttpStatusCode.OK)
                            return;

                        var responseContent = await response.Content.ReadAsStringAsync();
                        if (responseContent == null)
                            return;

                        var arbResponse = JsonConvert.DeserializeObject<ArbiterSigningResponse>(responseContent);
                        if (arbResponse == null)
                            return;

                        Globals.Arbiters.Remove(arbiter);
                        arbiter.SigningAddress = arbResponse.Address;
                        Globals.Arbiters.Add(arbiter);
                    }
                    catch (Exception ex)
                    {

                    }
                }
            }

            var check = Globals.Arbiters;
        }

        private static string CreateDepositAddress(List<PubKey> shares)
        {
            PubKey[] aggregatedPubKey = AggregatePublicKeys(shares);
            Script multiSigScript = CreateMultiSigScript(Globals.TotalArbiterThreshold, aggregatedPubKey);
            BitcoinAddress multiSigAddress = GetMultiSigAddress(multiSigScript);

            return multiSigAddress.ToString();
        }

        private static PubKey[] AggregatePublicKeys(List<PubKey> publicKeys)
        {
            Script multiSigScript = PayToMultiSigTemplate.Instance.GenerateScriptPubKey(publicKeys.Count, publicKeys.ToArray());
            return multiSigScript.GetSigOpCount(true) == publicKeys.Count ? multiSigScript.GetDestinationPublicKeys() : null;
        }
        private static Script CreateMultiSigScript(int threshold, PubKey[] publicKeys)
        {
            return PayToMultiSigTemplate.Instance.GenerateScriptPubKey(threshold, publicKeys);
        }

        private static BitcoinAddress GetMultiSigAddress(Script multiSigScript)
        {
            // Convert the multisig script to a script hash
            var scriptHash = multiSigScript.Hash;

            // Derive the corresponding Bitcoin address from the script hash
            return scriptHash.GetAddress(Globals.BTCNetwork);
        }

        private class ArbiterResponse
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public string PublicKey { get; set; }
            public string Signature { get; set; }
        }
        private class ArbiterSigningResponse
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public string Address { get; set; }
        }
        public class ArbiterProof
        {
            public string Signature { get; set; }
            public string SigningAddress { get; set; }
            public string PublicKey { get; set; }
        }
    }
}
