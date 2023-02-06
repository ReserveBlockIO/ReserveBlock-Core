using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.Services;

namespace ReserveBlockCore.Controllers
{
    [ActionFilterController]
    [Route("voapi/[controller]")]
    [Route("voapi/[controller]/{somePassword?}")]
    [ApiController]
    public class VOV1Controller : ControllerBase
    {
        /// <summary>
        /// Dumps out all vote topics
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetAllTopics")]
        public async Task<string> GetAllTopics()
        {
            var output = "";

            var topics = TopicTrei.GetTopics();
            if(topics != null)
            {
                var allTopics = topics.FindAll().ToList();
                if(allTopics.Count() > 0)
                {
                    output = JsonConvert.SerializeObject(allTopics);
                }
            }

            return output;
        }

        /// <summary>
        /// Dumps out all "active" topics
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetActiveTopics")]
        public async Task<string> GetActiveTopics()
        {
            var output = "";

            var topics = TopicTrei.GetActiveTopics();
            if (topics != null)
            {
                if (topics.Count() > 0)
                {
                    output = JsonConvert.SerializeObject(topics);
                }
            }

            return output;
        }

        /// <summary>
        /// Dumps out all "inactive" topics
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetInactiveTopics")]
        public async Task<string> GetInactiveTopics()
        {
            var output = "";

            var topics = TopicTrei.GetInactiveTopics();
            if (topics != null)
            {
                if (topics.Count() > 0)
                {
                    output = JsonConvert.SerializeObject(topics);
                }
            }

            return output;
        }

        /// <summary>
        /// Gets the details of topics
        /// </summary>
        /// <param name="topicUID"></param>
        /// <returns></returns>
        [HttpGet("GetTopicDetails/{topicUID}")]
        public async Task<string> GetTopicDetails(string topicUID)
        {
            var output = "";

            var topic = TopicTrei.GetSpecificTopic(topicUID);

            if(topic != null)
            {
                output = JsonConvert.SerializeObject(topic);
            }

            return output;
        }

        /// <summary>
        /// Gets the topics you have created from the active validator address
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetMyTopics")]
        public async Task<string> GetMyTopics()
        {
            var output = "";
            var address = Globals.ValidatorAddress;

            var topics = TopicTrei.GetSpecificTopicByAddress(address);
            if (topics != null)
            {
                if (topics.Count() > 0)
                {
                    output = JsonConvert.SerializeObject(topics);
                }
            }
            return output;
        }

        /// <summary>
        /// Gets votes you have casted with given validator address.
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetMyVotes")]
        public async Task<string> GetMyVotes()
        {
            var output = "";
            var address = Globals.ValidatorAddress;

            var myVotes = Vote.GetSpecificAddressVotes(address);
            if(myVotes != null)
            {
                if (myVotes.Count() > 0)
                {
                    output = JsonConvert.SerializeObject(myVotes);
                }
            }
            return output;
        }

        /// <summary>
        /// Gets the details of topics
        /// </summary>
        /// <param name="topicUID"></param>
        /// <returns></returns>
        [HttpGet("GetTopicVotes/{topicUID}")]
        public async Task<string> GetTopicVotes(string topicUID)
        {
            var output = "";

            var topicVotes = Vote.GetSpecificTopicVotes(topicUID);
            if (topicVotes != null)
            {
                if (topicVotes.Count() > 0)
                {
                    output = JsonConvert.SerializeObject(topicVotes);
                }
            }
            return output;
        }

        /// <summary>
        /// Audit a specific topic. This process can take a while.
        /// </summary>
        /// <param name="topicUID"></param>
        /// <returns></returns>
        [HttpGet("GetAuditTopic/{topicUID}")]
        public async Task<string> GetAuditTopic(string topicUID)
        {
            var output = "";

            _ = TopicTrei.AuditTopic(topicUID);

            output = $"Audit started for topic: {topicUID}";
            
            return output;
        }

        /// <summary>
        /// Cast your vote on a specific topic. (yes or no)
        /// </summary>
        /// <param name="topicUID"></param>
        /// <param name="voteType"></param>
        /// <returns></returns>
        [HttpGet("CastTopicVote/{topicUID}/{voteType}")]
        public async Task<string> CastTopicVote(string topicUID, VoteType voteType)
        {
            var output = "";

            var topic = TopicTrei.GetSpecificTopic(topicUID);

            if (topic != null)
            {
                Vote.VoteCreate voteC = new Vote.VoteCreate {
                    TopicUID = topicUID,
                    VoteType = voteType 
                };

                Vote vote = new Vote();

                vote.Build(voteC);

                var result = await Vote.CreateVoteTx(vote);

                if(result.Item1 != null)
                {
                    return result.Item1.Hash;
                }
                else
                {
                    return result.Item2;
                }
            }

            return output;
        }

        /// <summary>
        /// Search for a topic with a single word search
        /// </summary>
        /// <param name="search"></param>
        /// <returns></returns>
        [HttpGet("GetSearchTopics/{**search}")]
        public async Task<string> GetSearchTopics(string search)
        {
            var output = "";

            var topics = TopicTrei.GetTopics();
            if (topics != null)
            {
                if(!string.IsNullOrEmpty(search))
                {
                    search = search.ToLower();
                    var results = topics.Query()
                        .Where(x => x.TopicOwnerAddress.ToLower().Contains(search) ||
                            x.TopicUID.ToLower().Contains(search) ||
                            x.TopicName.ToLower().Contains(search))
                        .ToEnumerable();

                    if(results.Count() > 0)
                    {
                        output = JsonConvert.SerializeObject(results);
                    }
                }
                
            }

            return output;
        }

        /// <summary>
        /// Post a vote topic
        /// </summary>
        /// <param name="jsonData"></param>
        /// <returns></returns>
        [HttpPost("PostNewTopic")]
        public async Task<string> PostNewTopic([FromBody] object jsonData)
        {
            var output = "";

            if(jsonData != null)
            {
                var topicCreate = JsonConvert.DeserializeObject<TopicTrei.TopicCreate>(jsonData.ToString());

                var topic = new TopicTrei
                {
                    TopicName = topicCreate.TopicName,
                    TopicDescription = topicCreate.TopicDescription,
                };

                if(topicCreate.VoteTopicCategory == VoteTopicCategories.AdjVoteIn)
                {
                    var adjVoteReqs = JsonConvert.DeserializeObject<AdjVoteInReqs>(topic.TopicDescription);
                    if(adjVoteReqs != null)
                    {
                        var voteReqsResult = VoteValidatorService.ValidateAdjVoteIn(adjVoteReqs);
                        if(!voteReqsResult)
                        {
                            output = JsonConvert.SerializeObject(new { Success = false, Message = "You did not meet the required specs or information was not completed. This topic has been cancelled." });
                            return output;
                        }
                    }
                    else
                    {
                        output = JsonConvert.SerializeObject(new { Success = false, Message = "For this topic you must complete the Adj Vote in Requirements." });
                        return output;
                    }
                }

                topic.Build(topicCreate.VotingEndDays, topicCreate.VoteTopicCategory);

                var result = await TopicTrei.CreateTopicTx(topic);
                if(result.Item1 == null)
                {
                    output = JsonConvert.SerializeObject(new { Success = false, result.Item2 });
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Success = true, TxHash = result.Item1.Hash, Topic = topic });
                }
            }
            
            return output;
        }

    }
}
