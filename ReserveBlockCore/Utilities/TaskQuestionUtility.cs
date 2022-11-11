using ReserveBlockCore.Models;

namespace ReserveBlockCore.Utilities
{
    public class TaskQuestionUtility
    {
        public static async Task<TaskQuestion> CreateTaskQuestion(string type, long height)
        {
            TaskQuestion taskQuestion = new TaskQuestion();

            if(!string.IsNullOrWhiteSpace(type))
            {
                switch (type)
                {
                    case "rndNum":
                        taskQuestion.TaskAnswer = GenerateRandomNumber(height).ToString();
                        taskQuestion.BlockHeight = Globals.LastBlock.Height + 1;
                        break;
                    case "pickCol":
                        break;
                    default:
                        break;
                }
                
                taskQuestion.TaskType = type;
                taskQuestion.BlockHeight = Globals.LastBlock.Height + 1;
            }

            return taskQuestion;
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
