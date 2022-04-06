using ReserveBlockCore.Models;

namespace ReserveBlockCore.Utilities
{
    public class TaskWinnerUtility
    {
        public static async Task<TaskAnswer?> TaskWinner(TaskQuestion taskQuestion, List<TaskAnswer> taskAnswerList, List<TaskAnswer>? failedTaskAnswerList = null)
        {
            var answer = Convert.ToInt32(taskQuestion.TaskAnswer);
            var answerList = new List<int>();
            if (failedTaskAnswerList == null)
            {
                foreach(var taskAnswer in taskAnswerList)
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
    }
}
