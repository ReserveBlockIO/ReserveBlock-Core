using ReserveBlockCore.Data;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System.Globalization;
using System.Numerics;

namespace ReserveBlockCore.Models.SmartContracts
{
    public class TokenVoteTopic
    {
        public string SmartContractUID { get; set; }//user defined
        public string TopicUID { get; set; } //system defined
        public string TopicName { get; set; } //user defined - max length of 128 chars
        public string TopicDescription { get; set; }  //user defined - max length of 1600 chars
        public long MinimumVoteRequirement { get; set; }
        public long BlockHeight { get; set; }  //system defined
        public long TokenHolderCount { get; set; }  //system defined
        public long TopicCreateDate { get; set; }  //system defined
        public long VotingEndDate { get; set; } //user defined
        public int VoteYes { get; set; }  //system defined
        public int VoteNo { get; set; }  //system defined
        public decimal TotalVotes { get { return VoteYes + VoteNo; } }  //system defined
        public decimal PercentVotesYes { get { try { return TotalVotes != 0M ? Math.Round(((Convert.ToDecimal(VoteYes) / TotalVotes) * 100M), 2) : 0M; } catch { return 0M; } } }  //system defined
        public decimal PercentVotesNo { get { try { return TotalVotes != 0M ? Math.Round(((Convert.ToDecimal(VoteNo) / TotalVotes) * 100M), 2) : 0M; } catch { return 0M; } } }   //system defined
        public decimal PercentInFavor { get { try { return Math.Round(((Convert.ToDecimal(VoteYes) / Convert.ToDecimal(TokenHolderCount)) * 100M), 2); } catch { return 0M; } } }   //system defined 
        public decimal PercentAgainst { get { try { return Math.Round(((Convert.ToDecimal(VoteNo) / Convert.ToDecimal(TokenHolderCount)) * 100M), 2); } catch { return 0M; } } }   //system defined

        public class TopicCreate
        {
            public string TopicName { get; set; }
            public string TopicDescription { get; set; }
            public string SmartContractUID { get; set; }
            public long MinimumVoteRequirement { get; set; }
            public string FromAddress { get; set; }
            public VotingDays VotingEndDays { get; set; }
        }

        public string GetTokenTopicUID()
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var stringChars = new char[8];
            var random = new Random();

            for (int i = 0; i < stringChars.Length; i++)
            {
                stringChars[i] = chars[random.Next(chars.Length)];
            }

            var finalString = (new string(stringChars)) + TimeUtil.GetTime().ToString();

            return finalString;
        }

        public bool Build(VotingDays voteDays)
        {
            var result = false;
            var adjList = new List<string>();
            var startDate = DateTimeOffset.UtcNow.AddDays(-14).ToUnixTimeSeconds();
            var tokenHolderCount = GetTotalTokenHolderCount(SmartContractUID);

            var daysToEnd = ((int)voteDays);

            TopicUID = GetTokenTopicUID();
            TopicCreateDate = TimeUtil.GetTime();
            VotingEndDate = TimeUtil.GetTimeFromDateTime(DateTime.UtcNow.AddDays(daysToEnd));
            TokenHolderCount = tokenHolderCount;
            BlockHeight = Globals.LastBlock.Height;

            if (TopicName.Length > 128)
                return result;

            if (TopicDescription.Length > 1600)
                return result;


            return true;
        }

        private long GetTotalTokenHolderCount(string scUID)
        {
            var acState = StateData.GetAccountStateTrei();
            long count = 0;

            if(acState != null)
            {
                var tokenAccountHolders = acState.Query().Where(x => x.TokenAccounts != null).ToEnumerable();
                foreach(var account in tokenAccountHolders)
                {
                    if(account.TokenAccounts.Exists(x => x.SmartContractUID == scUID))
                    {
                        count += 1;
                    }
                }
            }

            return count;
        }
    }
}
