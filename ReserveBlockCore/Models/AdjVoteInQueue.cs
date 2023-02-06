using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Utilities;
using System.Diagnostics;

namespace ReserveBlockCore.Models
{
    public class AdjVoteInQueue
    {
        public int Id { get; set; }
        public string IPAddress { get; set; }
        public string RBXAddress { get; set; }
        public string TopicUID { get; set; }
        public long DateVotingStarted { get; set; }
        public long DateVotingEnds { get; set; }
        public bool IsVoteInFavor { get; set; }
        public bool IsVoteAgainst { get; set; }
        public bool VoteIn { get; set; }
        public bool VoteOut { get; set; }

        public static LiteDB.ILiteCollection<AdjVoteInQueue> GetAdjVoteInQueue()
        {
            try
            {
                return DbContext.DB_Config.GetCollection<AdjVoteInQueue>(DbContext.RSRV_ADJ_BENCH_QUEUE);
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError(ex.ToString(), "AdjVoteInQueue.GetAdjVoteInQueueh()");
                return null;
            }
        }

        public static bool SaveToQueue(TopicTrei topic)
        {
            var adjVoteInQueueDb = GetAdjVoteInQueue();
            if (adjVoteInQueueDb != null)
            {
                try
                {
                    var adjVoteReqs = JsonConvert.DeserializeObject<AdjVoteInReqs>(topic.TopicDescription);
                    if (adjVoteReqs != null)
                    {
                        AdjVoteInQueue adjVIQ = new AdjVoteInQueue
                        {
                            DateVotingEnds = TimeUtil.GetTimeFromDateTime(topic.VotingEndDate),
                            DateVotingStarted = TimeUtil.GetTimeFromDateTime(topic.TopicCreateDate),
                            IPAddress = adjVoteReqs.IPAddress,
                            IsVoteAgainst = false,
                            IsVoteInFavor = false,
                            RBXAddress = adjVoteReqs.RBXAddress,
                            TopicUID = topic.TopicUID,
                            VoteIn = topic.VoteTopicCategory == VoteTopicCategories.AdjVoteIn ? true : false,
                            VoteOut = topic.VoteTopicCategory == VoteTopicCategories.AdjVoteOut ? true : false,
                        };
                        var rec = adjVoteInQueueDb.Query().Where(x => x.RBXAddress == adjVIQ.RBXAddress).FirstOrDefault();
                        if (rec == null)
                        {
                            adjVoteInQueueDb.InsertSafe(adjVIQ);
                            return true;
                        }
                    }
                }
                catch { }
            }

            return false;
        }


    }
}
