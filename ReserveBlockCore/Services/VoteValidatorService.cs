using ReserveBlockCore.Models;

namespace ReserveBlockCore.Services
{
    public class VoteValidatorService
    {
        public static bool ValidateAdjVoteIn(AdjVoteInReqs adjVoteReq)
        {
            var result = false;

            switch(adjVoteReq.MachineHDDSpecifier)
            {
                case HDSizeSpecifier.GB:
                    if(adjVoteReq.MachineHDDSize < 200)
                    {
                        return result;
                    }
                    break;
                case HDSizeSpecifier.TB:
                    if(adjVoteReq.MachineHDDSize < 1)
                    {
                        return result;
                    }
                    //
                    break;
                case HDSizeSpecifier.PB:
                    if (adjVoteReq.MachineHDDSize < 1)
                    {
                        return result;
                    }
                    break;
            }

            if(adjVoteReq.InternetSpeedDown >= 100 && 
                adjVoteReq.InternetSpeedUp >= 100 &&
                adjVoteReq.MachineCPUCores >= 6 &&
                adjVoteReq.MachineCPUThreads >= 10 &&
                adjVoteReq.MachineRam >= 16 &&
                (adjVoteReq.Bandwith > 32 || adjVoteReq.Bandwith == 0) &&
                adjVoteReq.TechnicalBackground.Length > 100 &&
                adjVoteReq.ReasonForAdjJoin.Length > 100) { result = true; }

            return result;
        }
    }
}
