using ReserveBlockCore.Models;
using System.Xml.Linq;

namespace ReserveBlockCore.Utilities
{
    public class TaskWinnerUtility
    {
        public static async Task<TaskAnswer?> TaskWinner(TaskQuestion taskQuestion, List<TaskAnswer>? failedTaskAnswerList = null)
        {
            var answer = Convert.ToInt32(taskQuestion.TaskAnswer);
            var answerList = new List<int>();
            var taskAnswerList = Globals.TaskAnswerDict.Values.ToArray();

            if (failedTaskAnswerList == null)
            {                
                foreach (var taskAnswer in taskAnswerList)
                {
                    int parsedAnswer = 0;
                    var valAnswer = int.TryParse(taskAnswer.Answer, out parsedAnswer);
                    if(parsedAnswer != 0)
                    {
                        answerList.Add(parsedAnswer);
                    }
                };

                int closest = answerList.Aggregate((x, y) => Math.Abs(x - answer) < Math.Abs(y - answer) ? x : y);

                var winner = taskAnswerList.Where(x => x.Answer == closest.ToString()).OrderBy(x => x.SubmitTime).FirstOrDefault();

                if (winner != null)
                {
                    return winner;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                List<TaskAnswer> validTaskAnswerList = taskAnswerList.Except(failedTaskAnswerList).ToList();

                if(validTaskAnswerList.Count() > 0)
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
        public static async Task<TaskNumberAnswer?> TaskWinner_New(TaskQuestion taskQuestion, List<TaskNumberAnswer> taskAnswerList, List<TaskNumberAnswer>? failedTaskAnswerList = null)
        {
            var answer = Convert.ToInt32(taskQuestion.TaskAnswer);
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
                    Globals.TaskSelectedNumbers[winner.Address] = winner;
                    return winner;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                List<TaskNumberAnswer> validTaskAnswerList = taskAnswerList.Except(failedTaskAnswerList).ToList();

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
                        Globals.TaskSelectedNumbers[winner.Address] = winner;
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

        private static void GetWinningNodes(List<TaskNumberAnswer> validTaskAnswerList, int answer, int numChoices)
        {
            var answerList = new List<int>();

            Globals.TaskSelectedNumbers.Clear();

            var chosenOnes = validTaskAnswerList.Where(x => x.Answer.ToInt32() != 0).OrderBy(x => Math.Abs(x.Answer.ToInt32() - answer))
                .ThenBy(x => x.SubmitTime).Take(numChoices).ToList();

            foreach(var chosen in chosenOnes)
                Globals.TaskSelectedNumbers[chosen.Address] = chosen;
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
