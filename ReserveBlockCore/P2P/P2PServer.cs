using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
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
        private static Dictionary<string, string> PeerList = new Dictionary<string, string>();

        #region Broadcast methods
        public override async Task OnConnectedAsync()
        {
            var peerIP = GetIP(Context);
            var blockHeight = BlockchainData.GetHeight();
            PeerList.Add(Context.ConnectionId, peerIP);

            await SendMessage("Hello Peer", blockHeight.ToString());
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? ex)
        {
            string connectionId = Context.ConnectionId;
            var check = PeerList.ContainsKey(connectionId);

            if (check == true)
            {
                var peer = PeerList.FirstOrDefault(x => x.Key == connectionId);
                var ip = peer.Value;
                //await SendMessageAllPeers(ip);
                //do some logic
            }
        }

        public async Task SendMessage(string message, string data)
        {
            await Clients.Caller.SendAsync("GetMessage", message, data);
        }

        public async Task SendMessageAllPeers(string message, string data)
        {
            await Clients.All.SendAsync("GetMessage", message, data);
        }

        public async Task SendMessageAllValidators(string ip)
        {
            await Clients.All.SendAsync("GetMessage", "NewBlock");
        }

        #endregion

        #region Receive Block
        public async Task ReceiveBlock(Block nextBlock)
        {
            Console.WriteLine("Found Block: " + nextBlock.Height.ToString());

            var nextHeight = BlockchainData.GetHeight() + 1;
            var currentHeight = nextBlock.Height;

            if(nextHeight == currentHeight)
            {
                var result = await BlockValidatorService.ValidateBlock(nextBlock);
                if (result == true)
                {
                    string data = "";
                    data = JsonConvert.SerializeObject(nextBlock);
                    await SendMessageAllPeers("blk", data);
                }
                else
                {
                    Console.WriteLine("Block was rejected from: " + nextBlock.Validator);
                    //Add rejection notice for validator
                }
            }
            else
            {
                // means we need to download some blocks
                Program.BlocksDownloading = true;
                var setDownload = await BlockDownloadService.GetAllBlocks(currentHeight);
                Program.BlocksDownloading = setDownload;
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

        #region Ping Peers

        public async Task<string> PingPeers()
        {
            var peerIP = GetIP(Context);

            var peerDB = Peers.GetAll();

            var peer = peerDB.FindOne(x => x.PeerIP == peerIP);

            if(peer == null)
            {
                //this does a ping back on the peer to see if it can also be an outgoing node.
                var result = await P2PClient.PingBackPeer(peerIP);

                Peers nPeer = new Peers { 
                    FailCount = 0,
                    IsIncoming = true,
                    IsOutgoing = result,
                    PeerIP = peerIP,
                };

                peerDB.Insert(nPeer);
            }
            return "HelloPeer";
        }

        public async Task<string> PingBackPeer()
        {
            return "HelloBackPeer";
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
        public async Task<string> SendTxToMempool(Transaction txReceived)
        {
            var result = "";

            var data = JsonConvert.SerializeObject(txReceived);

            var mempool = TransactionData.GetPool();
            if (mempool.Count() != 0)
            {
                var txFound = mempool.FindOne(x => x.Hash == txReceived.Hash);
                if (txFound == null)
                {
                    var dblspndChk = await TransactionData.DoubleSpendCheck(txReceived);
                    if (dblspndChk == true)
                        return "TFVP";
                    var txResult = TransactionValidatorService.VerifyTX(txReceived); //sends tx to connected peers
                    if (txResult == true)
                    {
                        mempool.Insert(txReceived);
                        await SendMessageAllPeers("tx", data);
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
                var dblspndChk = await TransactionData.DoubleSpendCheck(txReceived);
                if (dblspndChk == true)
                    return "TFVP";

                var txResult = TransactionValidatorService.VerifyTX(txReceived);
                if (txResult == true)
                {
                    mempool.Insert(txReceived);
                    await SendMessageAllPeers("tx", data); //sends tx to connected peers
                    return "ATMP";//added to mempool
                }
                else
                {
                    return "TFVP"; //transaction failed verification process
                }
            }

            return "";
        }

        #endregion

        #region Get Masternodes
        public async Task<List<Validators>?> GetMasternodes(int valCount)
        {
            var validatorList = Validators.Validator.GetAll();
            var validatorListCount = validatorList.Count();

            if(validatorListCount == 0)
            {
                return null;
            }
            else
            {
                if(valCount == 0)
                {
                    return validatorList.FindAll().ToList();
                }
                else
                {
                    if(valCount < validatorListCount)
                    {
                        return validatorList.FindAll().ToList();
                    }
                }
            }

            return null;
        }

        #endregion

        #region Send Validator - Receives the new validator
        public async Task<string> SendValidator(Validators validator)
        {
            var peerIP = GetIP(Context);
            validator.NodeIP = peerIP;

            string data = "";

            var updateMasternodes = await P2PClient.GetMasternodes();

            var validatorList = Validators.Validator.GetAll();

            if (validatorList.Count() != 0)
            {
                var valFound = validatorList.FindOne(x => x.NodeIP == validator.NodeIP || x.Address == validator.Address);
                if (valFound == null)
                {
                    var result = ValidatorService.ValidateTheValidator(validator);
                    if (result == true)
                    {
                        var valPosFound = validatorList.FindOne(x => x.Position == validator.Position);
                        if(valPosFound != null)
                        {
                            validator.Position = validatorList.FindAll().Count() + 1; //adding just in case positions are off.
                        }
                        validatorList.Insert(validator);

                        data = JsonConvert.SerializeObject(validator);

                        await SendMessageAllPeers("val", data);
                        return "VATN";//added to validator list
                    }
                    else
                    {
                        return "FTAV"; //validator failed verification process
                    }
                }
                else
                {
                    //Update found record with new information
                    var result = ValidatorService.ValidateTheValidator(validator);
                    if (result == true)
                    {
                        var valPosFound = validatorList.FindOne(x => x.Position == validator.Position);
                        if (valPosFound != null)
                        {
                            validator.Position = validatorList.FindAll().Count() + 1; //adding just in case positions are off.
                        }

                        valFound.Amount = validator.Amount;
                        valFound.Signature = validator.Signature;
                        valFound.Address = validator.Address;
                        valFound.IsActive = validator.IsActive;
                        valFound.EligibleBlockStart = validator.EligibleBlockStart;
                        valFound.UniqueName = validator.UniqueName;
                        valFound.FailCount = validator.FailCount;
                        valFound.Position = validator.Position;

                        validatorList.Update(valFound);

                        data = JsonConvert.SerializeObject(valFound);
                        await SendMessageAllPeers("val", data);
                    }
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

        #region Ping Next Validator
        public async Task<bool> PingNextValidator()
        {
            return true;
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
