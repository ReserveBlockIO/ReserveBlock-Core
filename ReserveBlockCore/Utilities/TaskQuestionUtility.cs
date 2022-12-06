using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
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
                        ConsensusServer.UpdateState(0, (int)ConsensusStatus.Processing, GenerateRandomNumber(Globals.LastBlock.Height + 1), false);
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
