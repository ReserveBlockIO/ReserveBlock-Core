using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System.Globalization;
using System.Net;
using System.Numerics;

namespace ReserveBlockCore.Models
{
    public class TopicTrei
    {
        #region Variables
        public int Id { get; set; }
        public string TopicUID { get; set; }
        public string TopicName { get; set; }
        public string TopicDescription { get; set; }
        public string TopicOwnerAddress { get; set; }
        public string TopicOwnerSignature { get; set; }
        public string? AdjudicatorAddress { get; set; }
        public long BlockHeight { get; set; }
        public int ValidatorCount { get; set; }
        public string? AdjudicatorSignature { get; set; } //must receive endorsement from adj
        public DateTime TopicCreateDate { get; set; }
        public DateTime VotingEndDate { get; set; }
        public TopicVoterType VoterType { get; set; }
        public VoteTopicCategories VoteTopicCategory { get; set; }
        public int VoteYes { get; set; }
        public int VoteNo { get; set; }
        
        public decimal TotalVotes { get { return VoteYes + VoteNo;  } }
        public decimal PercentVotesYes { get { return TotalVotes != 0M ? ((VoteYes / TotalVotes) * 100M) : 0M; } }
        public decimal PercentVotesNo { get { return TotalVotes != 0M ? ((VoteNo / TotalVotes) * 100M) : 0M; } }
        public decimal PercentInFavor { get { return ((VoteYes / ValidatorCount) * 100M); } }
        public decimal PercentAgainst { get { return ((VoteNo / ValidatorCount) * 100M); } }

        #endregion

        #region Get Topics DB
        public static LiteDB.ILiteCollection<TopicTrei>? GetTopics()
        {
            try
            {
                var topics = DbContext.DB_TopicTrei.GetCollection<TopicTrei>(DbContext.RSRV_TOPIC_TREI);
                return topics;
            }
            catch (Exception ex)
            {
                DbContext.Rollback();
                ErrorLogUtility.LogError(ex.ToString(), "TopicTrei.GetTopics()");
                return null;
            }

        }

        #endregion

        #region Get Specific Topic
        public static TopicTrei? GetSpecificTopic(string tUID)
        {
            var topics = GetTopics();
            if (topics != null)
            {
                var topic = topics.FindOne(x => x.TopicUID == tUID);
                if (topic != null)
                {
                    return topic;
                }
            }
            return null;
        }
        #endregion

        #region Get Specific Topic by Address
        public static List<TopicTrei>? GetSpecificTopicByAddress(string address, bool isActiveOnly = false)
        {
            var topics = GetTopics();
            if (topics != null)
            {
                var currentDate = DateTime.UtcNow;
                if (isActiveOnly)
                {
                    var topicList = topics.Find(x => x.TopicOwnerAddress == address && x.VotingEndDate >= currentDate).ToList();
                    if (topicList.Count() > 0)
                    {
                        return topicList;
                    }
                }
                else
                {
                    var topicList = topics.Find(x => x.TopicOwnerAddress == address).ToList();
                    if (topicList.Count() > 0)
                    {
                        return topicList;
                    }
                }
                
            }
            return null;
        }
        #endregion

        #region Get Active Topics
        public static List<TopicTrei>? GetActiveTopics()
        {
            var topics = GetTopics();
            var currentDate = DateTime.UtcNow;
            if (topics != null)
            {
                var topicList = topics.Find(x => x.VotingEndDate >= currentDate).ToList();
                if (topicList.Count() > 0)
                {
                    return topicList;
                }
            }
            return null;
        }
        #endregion

        #region Get Inactive Topics
        public static List<TopicTrei>? GetInactiveTopics()
        {
            var topics = GetTopics();
            var currentDate = DateTime.UtcNow;
            if (topics != null)
            {
                var topicList = topics.Find(x => x.VotingEndDate < currentDate).ToList();
                if (topicList.Count() > 0)
                {
                    return topicList;
                }
            }
            return null;
        }
        #endregion

        #region Save Topic
        public static bool SaveAdnr(TopicTrei topic)
        {
            var topics = GetTopics();
            if (topics == null)
            {
                ErrorLogUtility.LogError("GetTopics() returned a null value.", "TopicTrei.SaveAdnr()");
                return false;
            }
            else
            {
                var topicRecData = topics.FindOne(x => x.TopicUID == topic.TopicUID);
                if (topicRecData != null)
                {
                    return false;
                }
                else
                {
                    topics.InsertSafe(topic);
                    return true;
                }
            }

        }
        #endregion

        #region Get Topic UID
        public string GetTopicUID()
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

        #endregion

        #region Build
        public bool Build(VotingDays voteDays, VoteTopicCategories voteTopicCat)
        {
            var result = false;

            var startDate = DateTimeOffset.UtcNow.AddDays(-14).ToUnixTimeSeconds();
            var adjCount = BlockchainData.GetBlocks().Query().Where(x => x.Timestamp >= startDate).Select(x => x.Validator).ToList().Distinct().Count();
            var daysToEnd = ((int)voteDays);

            TopicUID = GetTopicUID();
            VoteTopicCategory = voteTopicCat;
            TopicCreateDate = DateTime.UtcNow;
            VotingEndDate = DateTime.UtcNow.AddDays(daysToEnd);
            ValidatorCount = adjCount;
            BlockHeight = Globals.LastBlock.Height;

            if (TopicName.Length > 128)
                return result;

            if (TopicDescription.Length > 1600)
                return result;

            if(!string.IsNullOrEmpty(Globals.ValidatorAddress))
            {
                VoterType = TopicVoterType.Validator;
            }
            else if(Globals.AdjudicateAccount != null)
            {
                VoterType = TopicVoterType.Adjudicator;
            }
            else
            {
                return result;
            }

            var account = AccountData.GetSingleAccount(TopicOwnerAddress);
            if (account == null)
            {
                ErrorLogUtility.LogError($"Address is not found for : {TopicOwnerAddress}", "TopicTrei.Build()");
                return result;
            }

            var accPrivateKey = GetPrivateKeyUtility.GetPrivateKey(account.PrivateKey, account.Address);
            BigInteger b1 = BigInteger.Parse(accPrivateKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
            PrivateKey privateKey = new PrivateKey("secp256k1", b1);

            var sig = SignatureService.CreateSignature(TopicUID, privateKey, account.PublicKey);
            if (sig == "ERROR")
            {
                ErrorLogUtility.LogError($"Signing for Topic Build failed on address {TopicOwnerAddress}.", "TopicTrei.Build()");
                return result;
            }
            else
            {
                TopicOwnerSignature = sig;
                result = true;//Build success
            }

            return result; 
        }

        #endregion

        #region Create Topic Transaction
        public static async Task<(Transaction?, string)> CreateTopicTx(TopicTrei topic)
        {
            Transaction? topicTx = null;
            var address = topic.TopicOwnerAddress;

            var account = AccountData.GetSingleAccount(address);
            if (account == null)
            {
                ErrorLogUtility.LogError($"Address is not found for : {address}", "TopicTrei.CreateTopicTx()");
                return (null, $"Address is not found for : {address}");
            }

            var txData = "";
            var timestamp = TimeUtil.GetTime();

            var accPrivateKey = GetPrivateKeyUtility.GetPrivateKey(account.PrivateKey, account.Address);

            BigInteger b1 = BigInteger.Parse(accPrivateKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
            PrivateKey privateKey = new PrivateKey("secp256k1", b1);

            txData = JsonConvert.SerializeObject(new { Function = "TopicAdd()", Topic = topic });

            topicTx = new Transaction
            {
                Timestamp = timestamp,
                FromAddress = address,
                ToAddress = "Topic_Base",
                Amount = 1.0M,
                Fee = 0,
                Nonce = AccountStateTrei.GetNextNonce(address),
                TransactionType = TransactionType.VOTE_TOPIC,
                Data = txData
            };

            topicTx.Fee = FeeCalcService.CalculateTXFee(topicTx);

            topicTx.Build();

            var txHash = topicTx.Hash;
            var sig = SignatureService.CreateSignature(txHash, privateKey, account.PublicKey);
            if (sig == "ERROR")
            {
                ErrorLogUtility.LogError($"Signing TX failed for Topic Request on address {address}.", "TopicTrei.CreateTopicTx()");
                return (null, $"Signing TX failed for Topic Request on address {address}.");
            }

            topicTx.Signature = sig;

            try
            {
                if (topicTx.TransactionRating == null)
                {
                    var rating = await TransactionRatingService.GetTransactionRating(topicTx);
                    topicTx.TransactionRating = rating;
                }

                var result = await TransactionValidatorService.VerifyTXDetailed(topicTx);
                if (result.Item1 == true)
                {
                    topicTx.TransactionStatus = TransactionStatus.Pending;
                    TransactionData.AddToPool(topicTx);
                    TransactionData.AddTxToWallet(topicTx);
                    AccountData.UpdateLocalBalance(topicTx.FromAddress, (topicTx.Fee + topicTx.Amount));
                    await P2PClient.SendTXToAdjudicator(topicTx);//send out to mempool
                    return (topicTx, "Success");
                }
                else
                {
                    ErrorLogUtility.LogError($"Transaction Failed Verify and was not Sent to Mempool. Error: {result.Item2}", "TopicTrei.CreateTopicTx()");
                    return (null, $"Transaction Failed Verify and was not Sent to Mempool. Error: {result.Item2}");
                }
            }
            catch (Exception ex)
            {
                DbContext.Rollback();
                Console.WriteLine("Error: {0}", ex.ToString());
            }

            return (null, "Error. Please see message above.");
        }

        #endregion
    }

    #region Topic/Vote Enums
    public enum TopicVoterType
    {
        Adjudicator,
        Validator
    }
    public enum VoteType
    {
        No,
        Yes
    }
    public enum VotingDays
    {
        Thirty = 30,
        Sixty = 60,
        Ninety = 90,
        OneHundredEighty = 180
    }
    public enum VoteTopicCategories
    {
        General,
        CodeChange,
        AddDeveloper,
        RemoveDeveloper,
        NetworkChange,
        AdjVoteIn,
        AdjVoteOut,
        ValidatorChange,
        BlockModify,
        TransactionModify,
        BalanceCorrection,
        HackOrExploitCorrection,
        Other = 999,
    }
    #endregion

}
