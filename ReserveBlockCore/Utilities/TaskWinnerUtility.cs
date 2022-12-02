using ReserveBlockCore.Models;
using ReserveBlockCore.P2P;
using System.Xml.Linq;

namespace ReserveBlockCore.Utilities
{
    public class TaskWinnerUtility
    {
        public static async Task<TaskNumberAnswerV2?> TaskWinner_New(IList<TaskNumberAnswerV2> taskAnswerList, List<TaskNumberAnswerV2>? failedTaskAnswerList = null)
        {            
            var answer = ConsensusServer.GetState().Answer;
            var answerList = new List<int>();
            var answerCount = 0;
            if (failedTaskAnswerList == null)
            {
                foreach (var taskAnswer in taskAnswerList)
                {
                    int parsedAnswer = 0;
                    var valAnswer = int.TryParse(taskAnswer.Answer, out parsedAnswer);
                    if (parsedAnswer != 0)
                    {
                        answerList.Add(parsedAnswer);
                    }
                };

                int closest = answerList.Aggregate((x, y) => Math.Abs(x - answer) < Math.Abs(y - answer) ? x : y);

                var winner = taskAnswerList.Where(x => x.Answer == closest.ToString()).OrderBy(x => x.SubmitTime).FirstOrDefault();

                if (winner != null)
                {
                    taskAnswerList.Remove(winner);
                    answerList.Remove(closest);
                    answerCount = taskAnswerList.Count() >= 30 ? 30 : taskAnswerList.Count();

                    GetWinningNodes(taskAnswerList, answer, answerCount);
                    Globals.TaskSelectedNumbersV2[winner.Address] = winner;
                    return winner;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                List<TaskNumberAnswerV2> validTaskAnswerList = taskAnswerList.Except(failedTaskAnswerList).ToList();

                if (validTaskAnswerList.Count() > 0)
                {
                    foreach (var taskAnswer in validTaskAnswerList)
                    {
                        int parsedAnswer = 0;
                        var valAnswer = int.TryParse(taskAnswer.Answer, out parsedAnswer);
                        if (parsedAnswer != 0)
                        {
                            answerList.Add(parsedAnswer);
                        }
                    };

                    int closest = answerList.Aggregate((x, y) => Math.Abs(x - answer) < Math.Abs(y - answer) ? x : y);

                    var winner = validTaskAnswerList.Where(x => x.Answer == closest.ToString()).OrderBy(x => x.SubmitTime).FirstOrDefault();

                    if (winner != null)
                    {
                        validTaskAnswerList.Remove(winner);
                        answerList.Remove(closest);
                        answerCount = taskAnswerList.Count() >= 30 ? 30 : taskAnswerList.Count();

                        GetWinningNodes(validTaskAnswerList, answer, answerCount);
                        Globals.TaskSelectedNumbersV2[winner.Address] = winner;
                        return winner;
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }
        }

        private static void GetWinningNodes(IList<TaskNumberAnswerV2> validTaskAnswerList, int answer, int numChoices)
        {
            var answerList = new List<int>();

            Globals.TaskSelectedNumbersV2.Clear();

            var chosenOnes = validTaskAnswerList.Where(x => x.Answer.ToInt32() != 0).OrderBy(x => Math.Abs(x.Answer.ToInt32() - answer))
                .ThenBy(x => x.SubmitTime).Take(numChoices).ToList();

            foreach(var chosen in chosenOnes)
                Globals.TaskSelectedNumbersV2[chosen.Address] = chosen;
        }

        public static string GetVerifySecret()
        {
            string secret = "";

            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var stringChars = new char[16];
            var random = new Random();

            for (int i = 0; i < stringChars.Length; i++)
            {
                stringChars[i] = chars[random.Next(chars.Length)];
            }

            var finalString = new string(stringChars);

            secret = finalString;

            stringChars = new char[16];

            return secret;
        }
    }
}
