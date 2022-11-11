using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Services;

namespace ReserveBlockCore.Utilities
{
    public class BlockVersionUtility
    {
        public static int GetBlockVersion(long height)
        {
            if (height > Globals.BlockLock)
                return 3;
            else if (height > 294000)
                return 2;
            
            return 1;                        
        }

        public static async Task<bool> Version2Rules(Block block)
        {
            bool result = false;
            var leadAdj = Globals.LeadAdjudicator;
            var leadAdjAddr = leadAdj.Address;

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
                foreach (var AddressSignature in AddressSignatures)
                {
                    var split = AddressSignature.Split(':');
                    var (Address, Signature) = (split[0], split[1]);
                    if (!Globals.AdjudicatorAddresses.ContainsKey(Address))
                        return false;
                    if(!(SignatureService.VerifySignature(Address, block.Hash, Signature)))
                        return false;
                    ValidCount++;
                }
                if (ValidCount == Globals.AdjudicatorAddresses.Count / 2 + 1)
                    return true;
            }

            return false;
        }
    }
}
