using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;
using System.Security.Cryptography;
using System.Text;

namespace ReserveBlockCore.Utilities
{
    public class TaskQuestionUtility
    {
        public static void CreateTaskQuestion(string type)
        {            
            if(!string.IsNullOrWhiteSpace(type))
            {
                switch (type)
                {
                    case "rndNum":
                        var state = ConsensusServer.GetState();
                        var nextHeight = Globals.LastBlock.Height + 1;
                        if (nextHeight != state.Height || state.IsUsed)
                        {
                            var Answer = GenerateRandomNumber(nextHeight);
                            var MyDecryptedAnswer = nextHeight + ":" + Answer;
                            var MyEncryptedAnswer = SignatureService.AdjudicatorSignature(MyDecryptedAnswer);
                            ConsensusServer.UpdateState(nextHeight, 0, (int)ConsensusStatus.Processing, Answer, MyEncryptedAnswer, false);
                        }
                        else
                            ConsensusServer.UpdateState(methodCode: 0, status: (int)ConsensusStatus.Processing);
                        break;
                    case "pickCol":
                        break;
                    default:
                        break;
                }
            }
        }

        public static int GenerateRandomNumber(long height)
        {
            int randomNumber = 0;
            Random rnd = new Random();
            if (height >= Globals.BlockLock)
                randomNumber = rnd.Next();
            else
                randomNumber = rnd.Next(1, 1000000);

            return randomNumber;
        }

    }
}
