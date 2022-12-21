using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Services;

namespace ReserveBlockCore.Utilities
{
    public class BlockVersionUtility
    {
        public static int GetBlockVersion(long height)
        {
            //testnet
            if(Globals.IsTestNet)
            {
                if (height > Globals.BlockLock)
                    return 3;
                if (height < 15)
                    return 2;
            }
           


            if (height > Globals.BlockLock)
                return 3;
            else if (height > 294000)
                return 2;
            
            return 1;                        
        }

        public static async Task<bool> Version2Rules(Block block)
        {
            bool result = false;
            var leadAdjAddr = Globals.LeadAddress;

            if(block.AdjudicatorSignature != null)
            {
                var sigResult = SignatureService.VerifySignature(leadAdjAddr, block.Hash, block.AdjudicatorSignature);
                result = sigResult;
            }

            return result;            
        }

        public static async Task<bool> Version3Rules(Block block)
        {
            if (!string.IsNullOrWhiteSpace(block.AdjudicatorSignature))
            {
                var ValidCount = 0;
                var AddressSignatures = block.AdjudicatorSignature.Split('|');
                var Addresses = new HashSet<string>();
                
                if(Globals.IsTestNet)
                {                    
                    foreach (var AddressSignature in AddressSignatures)
                    {
                        var split = AddressSignature.Split(':');
                        var (Address, Signature) = (split[0], split[1]);
                        if (!Globals.Signers.ContainsKey(Address) && Address != "xBRS3SxqLQtEtmqZ1BUJiobjUzwufwaAnK" && Address != "xBRNST9oL8oW6JctcyumcafsnWCVXbzZnr")
                            return false;
                        if (!(SignatureService.VerifySignature(Address, block.Hash, Signature)))
                            return false;
                        ValidCount++;
                        Addresses.Add(Address);
                    }
                    if (ValidCount == Addresses.Count && ValidCount >= Signer.Majority())
                        return true;
                }

                foreach (var AddressSignature in AddressSignatures)
                {
                    var split = AddressSignature.Split(':');
                    var (Address, Signature) = (split[0], split[1]);
                    if (!Globals.Signers.ContainsKey(Address))
                        return false;
                    if(!(SignatureService.VerifySignature(Address, block.Hash, Signature)))
                        return false;
                    ValidCount++;
                    Addresses.Add(Address);
                }
                if (ValidCount == Addresses.Count && ValidCount >= Signer.Majority())
                    return true;
            }

            return false;
        }
    }
}
