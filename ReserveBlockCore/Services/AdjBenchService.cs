using ReserveBlockCore.Models;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.Services
{
    public class AdjBenchService
    {
        public static async Task RunBenchService()
        {

            if (Globals.AdjudicateAccount == null && Globals.ValidatorAddress == "")
                return;

            while(true)
            {
                var delay = Task.Delay(new TimeSpan(12, 0, 0));

                ProcessVoteInQueue();
                //ProcessVoteOutQueue();

                await delay;
            }
        }

        private static void ProcessVoteInQueue()
        {
            var currentTime = TimeUtil.GetTime();
            var adjVoteInQueue = AdjVoteInQueue.GetAdjVoteInQueue();
            if (adjVoteInQueue != null)
            {
                var queueList = adjVoteInQueue.Query().Where(x => !x.IsVoteInFavor && !x.IsVoteAgainst && x.VoteIn).ToList();
                if (queueList.Count() > 0)
                {
                    foreach (var queue in queueList)
                    {
                        var topicUID = queue.TopicUID;
                        var topic = TopicTrei.GetSpecificTopic(topicUID);
                        if (topic != null)
                        {
                            if (topic.PercentInFavor >= 51.0M)
                            {
                                queue.IsVoteInFavor = true;
                                adjVoteInQueue.UpdateSafe(queue);
                                CreateAdjBenchItem(queue);
                            }
                            else if (topic.PercentAgainst >= 51.0M)
                            {
                                queue.IsVoteAgainst = true;
                                adjVoteInQueue.UpdateSafe(queue);
                            }
                            else
                            {
                                if (topic.VotingEndDate < DateTime.UtcNow)
                                {
                                    if (topic.PercentInFavor > 51.0M)
                                    {
                                        queue.IsVoteInFavor = true;
                                        adjVoteInQueue.UpdateSafe(queue);
                                        CreateAdjBenchItem(queue);
                                    }
                                    else if (topic.PercentAgainst > 51.0M)
                                    {
                                        queue.IsVoteAgainst = true;
                                        adjVoteInQueue.UpdateSafe(queue);
                                    }
                                    else
                                    {
                                        //failed to reach majority = automatic fail.
                                        queue.IsVoteAgainst = true;
                                        adjVoteInQueue.UpdateSafe(queue);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private static bool CreateAdjBenchItem(AdjVoteInQueue adjQ)
        {
            AdjBench adjB = new AdjBench
            {
                IPAddress = adjQ.IPAddress,
                RBXAddress = adjQ.RBXAddress,
                TimeEntered = TimeUtil.GetTime(),
                TopicUID = adjQ.TopicUID,
                PulledFromBench = false,
                TimeEligibleForConsensus = TimeUtil.GetTime(86400)
            };

            var saveResult = AdjBench.SaveToBench(adjB);

            return saveResult;
            
        }
    }
}
