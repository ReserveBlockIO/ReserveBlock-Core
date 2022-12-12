using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;

namespace ReserveBlockCore.Controllers
{
    [ActionFilterController]
    [Route("voapi/[controller]")]
    [Route("voapi/[controller]/{somePassword?}")]
    [ApiController]
    public class VOV1Controller : ControllerBase
    {
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

        [HttpGet("GetVotedOnTopics")]
        public async Task<string> GetVotedOnTopics()
        {
            var output = "";

            return output;
        }

        [HttpGet("GetNonVotedOnTopics")]
        public async Task<string> GetNonVotedOnTopics()
        {
            var output = "";

            return output;
        }

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

        [HttpGet("GetAuditTopic/{topicUID}")]
        public async Task<string> GetAuditTopic(string topicUID)
        {
            var output = "";

            TopicTrei.AuditTopic(topicUID);

            output = $"Audit started for topic: {topicUID}";
            
            return output;
        }

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
                        .ToList();

                    if(results.Count() > 0)
                    {
                        output = JsonConvert.SerializeObject(results);
                    }
                }
                
            }

            return output;
        }

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
