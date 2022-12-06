using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System.Globalization;
using System.Numerics;

namespace ReserveBlockCore.Models
{
    public class Vote
    {

        #region Variables
        public int Id { get; set; }
        public string TopicUID { get; set; }
        public string Address { get; set; }
        public VoteType VoteType { get; set; }
        public string TransactionHash { get; set; }
        public long BlockHeight { get; set; }

        public class VoteCreate
        {
            public string TopicUID { get; set; }
            public VoteType VoteType { get; set; }
        }

        #endregion

        #region Get Votes DB
        public static LiteDB.ILiteCollection<Vote>? GetVotes()
        {
            try
            {
                var votes = DbContext.DB_Vote.GetCollection<Vote>(DbContext.RSRV_VOTE);
                return votes;
            }
            catch (Exception ex)
            {
                DbContext.Rollback();
                ErrorLogUtility.LogError(ex.ToString(), "Vote.GetVotes()");
                return null;
            }

        }

        #endregion

        #region Get Specific Topic Votes
        public static List<Vote>? GetSpecificTopicVotes(string tUID)
        {
            var votes = GetVotes();
            if (votes != null)
            {
                var voteList = votes.Query().Where(x => x.TopicUID == tUID).ToList();
                if (voteList.Count() > 0)
                {
                    return voteList;
                }
            }
            return null;
        }
        #endregion

        #region Get Specific Address Votes
        public static List<Vote>? GetSpecificAddressVotes(string address)
        {
            var votes = GetVotes();
            if (votes != null)
            {
                var voteList = votes.Query().Where(x => x.Address == address).ToList();
                if (voteList.Count() > 0)
                {
                    return voteList;
                }
            }
            return null;
        }
        #endregion

        #region Get Specific Address Vote For Topic - Returns Vote
        public static Vote? GetSpecificAddressVoteOnTopic(string address, string topicUID)
        {
            var votes = GetVotes();
            if (votes != null)
            {
                var vote = votes.Query().Where(x => x.Address == address && x.TopicUID == topicUID).FirstOrDefault();
                if (vote != null)
                {
                    return vote;
                }
            }
            return null;
        }
        #endregion

        #region Get Specific Address Vote For Topic - Returns bool
        public static bool CheckSpecificAddressVoteOnTopic(string address, string topicUID)
        {
            var votes = GetVotes();
            if (votes != null)
            {
                var vote = votes.Query().Where(x => x.Address == address && x.TopicUID == topicUID).FirstOrDefault();
                if (vote != null)
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region Save Vote
        public static bool SaveVote(Vote vote)
        {
            var votes = GetVotes();
            if (votes == null)
            {
                ErrorLogUtility.LogError("GetTopics() returned a null value.", "Vote.SaveTopic()");
                return false;
            }
            else
            {
                var voteRecData = votes.FindOne(x => x.TopicUID == vote.TopicUID && x.Address == vote.Address);
                if (voteRecData != null)
                {
                    return false;
                }
                else
                {
                    votes.InsertSafe(vote);
                    return true;
                }
            }

        }
        #endregion

        #region Build
        public bool Build(VoteCreate vote)
        {
            try
            {
                TopicUID = vote.TopicUID;
                VoteType = vote.VoteType;
                Address = Globals.ValidatorAddress;
                return true;
            }
            catch { }

            return false;
        }
        #endregion

        #region Create Topic Transaction
        public static async Task<(Transaction?, string)> CreateVoteTx(Vote vote)
        {
            Transaction? voteTx = null;
            var address = vote.Address;

            var account = AccountData.GetSingleAccount(address);
            if (account == null)
            {
                ErrorLogUtility.LogError($"Address is not found for : {address}", "Vote.CreateVoteTx()");
                return (null, $"Address is not found for : {address}");
            }

            var txData = "";
            var timestamp = TimeUtil.GetTime();

            BigInteger b1 = BigInteger.Parse(account.PrivateKey, NumberStyles.AllowHexSpecifier);//converts hex private key into big int.
            PrivateKey privateKey = new PrivateKey("secp256k1", b1);

            txData = JsonConvert.SerializeObject(new { Function = "TopicVote()", Vote = vote });

            voteTx = new Transaction
            {
                Timestamp = timestamp,
                FromAddress = address,
                ToAddress = "Vote_Base",
                Amount = 0.0M,
                Fee = 0,
                Nonce = AccountStateTrei.GetNextNonce(address),
                TransactionType = TransactionType.VOTE,
                Data = txData
            };

            voteTx.Fee = FeeCalcService.CalculateTXFee(voteTx);

            voteTx.Build();

            var txHash = voteTx.Hash;
            var sig = SignatureService.CreateSignature(txHash, privateKey, account.PublicKey);
            if (sig == "ERROR")
            {
                ErrorLogUtility.LogError($"Signing TX failed for Topic Request on address {address}.", "Vote.CreateVoteTx()");
                return (null, $"Signing TX failed for Topic Request on address {address}.");
            }

            voteTx.Signature = sig;

            try
            {
                if (voteTx.TransactionRating == null)
                {
                    var rating = await TransactionRatingService.GetTransactionRating(voteTx);
                    voteTx.TransactionRating = rating;
                }

                var result = await TransactionValidatorService.VerifyTXDetailed(voteTx);//MODIFY THIS FOR VOTE RULES!
                if (result.Item1 == true)
                {
                    voteTx.TransactionStatus = TransactionStatus.Pending;
                    //TransactionData.AddToPool(topicTx);
                    TransactionData.AddTxToWallet(voteTx);
                    AccountData.UpdateLocalBalance(voteTx.FromAddress, (voteTx.Fee + voteTx.Amount));
                    //await P2PClient.SendTXToAdjudicator(topicTx);//send out to mempool
                    return (voteTx, "Success");
                }
                else
                {
                    ErrorLogUtility.LogError($"Transaction Failed Verify and was not Sent to Mempool. Error: {result.Item2}", "Vote.CreateVoteTx()");
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

    #region Enums
    public enum VoteType
    {
        No,
        Yes
    }

    #endregion
}
