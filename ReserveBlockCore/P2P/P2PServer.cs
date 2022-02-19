using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;


namespace ReserveBlockCore.P2P
{
    public class P2PServer : Hub
    {

        #region Receive Block
        public async Task ReceiveBlock(Block nextBlock, List<string> ipList)
        {
            Console.WriteLine("Found Block: " + nextBlock.Height.ToString());
            var result = await BlockValidatorService.ValidateBlock(nextBlock);
            if (result == false)
            {
                Console.WriteLine("Block was rejected from: " + nextBlock.Validator);
                //Add rejection notice for validator
            }
            else
            {
                //Resend block out if passed validation
                P2PClient.BroadcastBlock(nextBlock, ipList);
            }

        }

        #endregion

        #region Send list of Validators to peer
        public async Task<List<Validators>?> SendValidators()
        {
            var validators = Validators.Validator.GetAll();

            var validatorList = validators.FindAll().ToList();

            if (validatorList.Count() == 0)
                return null;

            //Only send 10 as that will be plenty.
            if (validatorList.Count() > 10)
                return validatorList.Take(10).ToList();

            return validatorList;
        }

        #endregion

        #region Get Validator Count
        public async Task<long?> SendValidatorCount()
        {
            var validators = Validators.Validator.GetAll();

            var validatorList = validators.FindAll().ToList();

            if (validatorList.Count() == 0)
                return null;

            return (long)validatorList.Count();
        }

        #endregion

        #region Connect Peers
        //Send hello status to connecting peers from p2p server
        public async Task ConnectPeers(string node, string message, string time)
        {
            long ticks = Convert.ToInt64(time);
            DateTime timeTicks = new DateTime(ticks);

            var feature = Context.Features.Get<IHttpConnectionFeature>();
            var peerIP = feature.RemoteIpAddress.MapToIPv4().ToString();

            if (message == "Hello")
            {
                var oNode = "Origin Node";
                var oMessage = "Connected to IP: " + peerIP;
                var endTime = DateTime.UtcNow;
                var totalTime = (endTime - timeTicks).TotalMilliseconds;
                await Clients.Caller.SendAsync("PeerConnected", oNode, oMessage, totalTime.ToString("0"), BlockchainData.ChainRef);
            }
        }

        #endregion

        #region Send Block Height
        public async Task<long> SendBlockHeight()
        {
            var blocks = BlockchainData.GetBlocks();

            if (blocks.FindAll().Count() != 0)
            {
                var blockHeight = BlockchainData.GetHeight();

                return blockHeight;
            }
            return -1;

        }

        #endregion

        #region Send Block
        //Send Block to client from p2p server
        public async Task<Block?> SendBlock(long currentBlock)
        {
            var peerIP = GetIP(Context);

            var message = "";
            var nextBlockHeight = currentBlock + 1;
            var nextBlock = BlockchainData.GetBlockByHeight(nextBlockHeight);

            if (nextBlock != null)
            {
                return nextBlock;
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region Send to Mempool
        public async Task<string> SendToMempool(Transaction txReceived, List<string> ipList)
        {
            var peerIP = GetIP(Context);

            var mempool = TransactionData.GetPool();
            if (mempool.Count() != 0)
            {
                var txFound = mempool.FindOne(x => x.Hash == txReceived.Hash);
                if (txFound == null)
                {
                    var result = TransactionValidatorService.VerifyTX(txReceived);
                    if (result == true)
                    {
                        mempool.Insert(txReceived);
                        P2PClient.SendTXMempool(txReceived, ipList);
                        return "ATMP";//added to mempool
                    }
                    else
                    {
                        return "TFVP"; //transaction failed verification process
                    }
                }
                else
                {
                    return "AIMP"; //already in mempool
                }
            }
            else
            {
                var result = TransactionValidatorService.VerifyTX(txReceived);
                if (result == true)
                {
                    mempool.Insert(txReceived);
                    return "ATMP";//added to mempool
                }
                else
                {
                    return "TFVP"; //transaction failed verification process
                }
            }
        }

        #endregion

        #region Send Validator
        public async Task<string> SendValidator(Validators validator)
        {
            var peerIP = GetIP(Context);
            validator.NodeIP = peerIP;

            var validatorList = Validators.Validator.GetAll();

            if (validatorList.Count() != 0)
            {
                var valFound = validatorList.FindOne(x => x.NodeIP == validator.NodeIP);
                if (valFound == null)
                {
                    //Need to do VALIDATOR VALIDATION
                    var result = ValidatorService.ValidateTheValidator(validator);
                    if (result == true)
                    {
                        validatorList.Insert(validator);
                        Validators.Validator.Initialize();
                        return "VATN";//added to validator list
                    }
                    else
                    {
                        return "FTAV"; //validator failed verification process
                    }
                }
                else
                {
                    return "AIVL"; //already in validator list
                }
            }
            else
            {
                var result = ValidatorService.ValidateTheValidator(validator);
                if (result == true)
                {
                    validatorList.Insert(validator);
                    Validators.Validator.Initialize();
                    return "VATN";//added to validator list
                }
                else
                {
                    return "FTAV"; //validator failed verification process
                }
            }

        }

        #endregion

        #region Share Peers
        public async Task SharePeers(string node)
        {
            var peers = P2PClient.ActivePeerList;
            var message = "";

            if (peers == null)
            {
                message = "NoPeers";
                await Clients.All.SendAsync("PeersShared", null, message);
            }
            else
            {
                message = "PeersFound";
                await Clients.All.SendAsync("PeersShared", peers, message);
            }
        }

        #endregion

        #region Seed node check
        public async Task<string> SeedNodeCheck()
        {
            //do check for validator. if yes return val otherwise return Hello.
            var validators = Validators.Validator.GetAll();
            var hasValidators = validators.FindAll().Where(x => x.NodeIP == "SELF").Count();

            if(hasValidators > 0)
                return "HelloVal";

            return "Hello";
        }
        #endregion

        #region Get IP

        private static string GetIP(HubCallerContext context)
        {
            var feature = context.Features.Get<IHttpConnectionFeature>();
            var peerIP = feature.RemoteIpAddress.MapToIPv4().ToString();

            return peerIP;
        }

        #endregion
    }
}
