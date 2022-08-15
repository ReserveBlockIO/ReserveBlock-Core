using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System;
using System.Collections.Concurrent;
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
        #region Broadcast methods
        public override async Task OnConnectedAsync()
        {            
            var peerIP = GetIP(Context);
            if(PeerList.TryGetValue(peerIP, out var context) && context.ConnectionId != Context.ConnectionId)
                context.Abort();

            PeerList[peerIP] = Context;

            //Save Peer here
            var peers = Peers.GetAll();
            var peerExist = peers.Find(x => x.PeerIP == peerIP).FirstOrDefault();
            if (peerExist == null)
            {
                Peers nPeer = new Peers
                {
                    FailCount = 0,
                    IsIncoming = true,
                    IsOutgoing = false,
                    PeerIP = peerIP
                };

                peers.InsertSafe(nPeer);
            }
                                    
            await Clients.Caller.SendAsync("GetMessage", "IP", peerIP);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? ex)
        {
            var peerIP = GetIP(Context);
            Globals.PeerList.TryRemove(peerIP, out var test);
        }

        #endregion

        #region GetConnectedPeerCount
        public static int GetConnectedPeerCount()
        {
            return Globals.PeerList.Count;
        }

        #endregion

        #region Receive Block
        public async Task ReceiveBlock(Block nextBlock)
        {                        
            if (Globals.BlocksDownloading == 0)
            {
                if (nextBlock.ChainRefId == BlockchainData.ChainRef)
                {
                    var IP = GetIP(Context);                 
                    var nextHeight = Globals.LastBlock.Height + 1;
                    var currentHeight = nextBlock.Height;

                    var isNewBlock = currentHeight >= nextHeight && !BlockDownloadService.BlockDict.ContainsKey(currentHeight);

                    if (isNewBlock)
                    {
                        BlockDownloadService.BlockDict[currentHeight] = (nextBlock, IP);
                        await BlockValidatorService.ValidateBlocks();
                    }

                    if (nextHeight == currentHeight && isNewBlock)
                    {                        
                        string data = "";
                        data = JsonConvert.SerializeObject(nextBlock);
                        await Clients.All.SendAsync("GetMessage", "blk", data);                        
                    }

                    if (nextHeight < currentHeight && isNewBlock)                    
                        await BlockDownloadService.GetAllBlocks();                                        
                }
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

        #region Ping Peers
        public async Task<string> PingPeers()
        {
            var peerIP = GetIP(Context);

            var peerDB = Peers.GetAll();

            var peer = peerDB.FindOne(x => x.PeerIP == peerIP);

            if (peer == null)
            {
                //this does a ping back on the peer to see if it can also be an outgoing node.
                var result = await P2PClient.PingBackPeer(peerIP);

                Peers nPeer = new Peers {
                    FailCount = 0,
                    IsIncoming = true,
                    IsOutgoing = result,
                    PeerIP = peerIP
                };

                peerDB.InsertSafe(nPeer);
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
            return Globals.LastBlock.Height;
        }

        #endregion

        #region Send Beacon Locator Info
        public async Task<string> SendBeaconInfo()
        {
            var result = "";

            var beaconInfo = BeaconInfo.GetBeaconInfo();

            if (beaconInfo == null)
                return "NA";

            result = beaconInfo.BeaconLocator;

            return result;
        }

        #endregion

        #region  ReceiveDownloadRequest
        public async Task<bool> ReceiveDownloadRequest(BeaconData.BeaconDownloadData bdd)
        {
            bool result = false;
            var peerIP = GetIP(Context);

            try
            {
                if (bdd != null)
                {
                    var scState = SmartContractStateTrei.GetSmartContractState(bdd.SmartContractUID);
                    if (scState == null)
                    {
                        return result; //fail
                    }

                    var sigCheck = SignatureService.VerifySignature(scState.OwnerAddress, bdd.SmartContractUID, bdd.Signature);
                    if (sigCheck == false)
                    {
                        return result; //fail
                    }

                    var beaconDatas = BeaconData.GetBeacon();
                    var beaconData = BeaconData.GetBeaconData();
                    foreach (var fileName in bdd.Assets)
                    {
                        if (beaconData != null)
                        {
                            var bdCheck = beaconData.Where(x => x.SmartContractUID == bdd.SmartContractUID && x.AssetName == fileName && x.NextAssetOwnerAddress == scState.OwnerAddress).FirstOrDefault();
                            if (bdCheck != null)
                            {
                                if (beaconDatas != null)
                                {
                                    bdCheck.DownloadIPAddress = peerIP;
                                    beaconDatas.UpdateSafe(bdCheck);
                                }
                                else
                                {
                                    return result;//fail
                                }
                            }
                            else
                            {
                                return result; //fail
                            }
                        }
                        else
                        {
                            return result; //fail
                        }

                        result = true;
                    }

                }
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError($"Error Creating BeaconData. Error Msg: {ex.Message}", "P2PServer.ReceiveUploadRequest()");
            }

            return result;
        }

        #endregion

        #region ReceiveUploadRequest
        public async Task<bool> ReceiveUploadRequest(BeaconData.BeaconSendData bsd)
        {
            bool result = false;
            var peerIP = GetIP(Context);
            try
            {
                if (bsd != null)
                {
                    var scState = SmartContractStateTrei.GetSmartContractState(bsd.SmartContractUID);
                    if (scState == null)
                    {
                        return result;
                    }

                    var sigCheck = SignatureService.VerifySignature(scState.OwnerAddress, bsd.SmartContractUID, bsd.Signature);
                    if (sigCheck == false)
                    {
                        return result;
                    }

                    var beaconData = BeaconData.GetBeaconData();
                    foreach (var fileName in bsd.Assets)
                    {
                        if (beaconData == null)
                        {
                            var bd = new BeaconData
                            {
                                AssetExpireDate = 0,
                                AssetReceiveDate = 0,
                                AssetName = fileName,
                                IPAdress = peerIP,
                                NextAssetOwnerAddress = bsd.NextAssetOwnerAddress,
                                SmartContractUID = bsd.SmartContractUID
                            };

                            BeaconData.SaveBeaconData(bd);
                        }
                        else
                        {
                            var bdCheck = beaconData.Where(x => x.SmartContractUID == bsd.SmartContractUID && x.AssetName == fileName).FirstOrDefault();
                            if (bdCheck == null)
                            {
                                var bd = new BeaconData
                                {
                                    AssetExpireDate = 0,
                                    AssetReceiveDate = 0,
                                    AssetName = fileName,
                                    IPAdress = peerIP,
                                    NextAssetOwnerAddress = bsd.NextAssetOwnerAddress,
                                    SmartContractUID = bsd.SmartContractUID
                                };

                                BeaconData.SaveBeaconData(bd);
                            }
                        }
                    }

                    result = true;

                }
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError($"Error Receive Upload Request. Error Msg: {ex.Message}", "P2PServer.ReceiveUploadRequest()");
            }

            return result;
        }

        #endregion

        #region Send Adjudicator
        public async Task<Adjudicators?> SendLeadAdjudicator()
        {
            var leadAdj = Globals.LeadAdjudicator;
            if (leadAdj == null)
            {
                leadAdj = Adjudicators.AdjudicatorData.GetLeadAdjudicator();
            }

            return leadAdj;
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
                    var isTxStale = await TransactionData.IsTxTimestampStale(txReceived);
                    if (!isTxStale)
                    {
                        var txResult = await TransactionValidatorService.VerifyTX(txReceived); //sends tx to connected peers
                        if (txResult == false)
                        {
                            try
                            {
                                mempool.DeleteManySafe(x => x.Hash == txReceived.Hash);// tx has been crafted into block. Remove.
                            }
                            catch (Exception ex)
                            {
                                //delete failed
                            }
                            return "TFVP";
                        }
                        var dblspndChk = await TransactionData.DoubleSpendCheck(txReceived);
                        var isCraftedIntoBlock = await TransactionData.HasTxBeenCraftedIntoBlock(txReceived);

                        if (txResult == true && dblspndChk == false && isCraftedIntoBlock == false)
                        {
                            mempool.InsertSafe(txReceived);
                            await P2PClient.SendTXToAdjudicator(txReceived);
                            return "ATMP";//added to mempool
                        }
                        else
                        {
                            try
                            {
                                mempool.DeleteManySafe(x => x.Hash == txReceived.Hash);// tx has been crafted into block. Remove.
                            }
                            catch (Exception ex)
                            {
                                //delete failed
                            }
                            return "TFVP"; //transaction failed verification process
                        }
                    }


                }
                else
                {

                    var isTxStale = await TransactionData.IsTxTimestampStale(txReceived);
                    if (!isTxStale)
                    {
                        var isCraftedIntoBlock = await TransactionData.HasTxBeenCraftedIntoBlock(txReceived);
                        if (!isCraftedIntoBlock)
                        {
                        }
                        else
                        {
                            try
                            {
                                mempool.DeleteManySafe(x => x.Hash == txReceived.Hash);// tx has been crafted into block. Remove.
                            }
                            catch (Exception ex)
                            {
                                //delete failed
                            }
                        }

                        return "AIMP"; //already in mempool
                    }
                    else
                    {
                        try
                        {
                            mempool.DeleteManySafe(x => x.Hash == txReceived.Hash);// tx has been crafted into block. Remove.
                        }
                        catch (Exception ex)
                        {
                            //delete failed
                        }
                    }

                }
            }
            else
            {
                var isTxStale = await TransactionData.IsTxTimestampStale(txReceived);
                if (!isTxStale)
                {
                    var txResult = await TransactionValidatorService.VerifyTX(txReceived);
                    if (txResult == false)
                    {
                        try
                        {
                            mempool.DeleteManySafe(x => x.Hash == txReceived.Hash);// tx has been crafted into block. Remove.
                        }
                        catch (Exception ex)
                        {
                            //delete failed
                        }
                        return "TFVP";
                    }
                    var dblspndChk = await TransactionData.DoubleSpendCheck(txReceived);
                    var isCraftedIntoBlock = await TransactionData.HasTxBeenCraftedIntoBlock(txReceived);
                    if (txResult == true && dblspndChk == false && isCraftedIntoBlock == false)
                    {
                        mempool.InsertSafe(txReceived);
                        await P2PClient.SendTXToAdjudicator(txReceived); //sends tx to connected peers
                        return "ATMP";//added to mempool
                    }
                    else
                    {
                        try
                        {
                            mempool.DeleteManySafe(x => x.Hash == txReceived.Hash);// tx has been crafted into block. Remove.
                        }
                        catch (Exception ex)
                        {
                            //delete failed
                        }
                        return "TFVP"; //transaction failed verification process
                    }
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

            if (validatorListCount == 0)
            {
                return null;
            }
            else
            {

                return validatorList.FindAll().ToList();
            }
        }

        #endregion

        #region Ping Next Validator
        public async Task<bool> PingNextValidator()
        {
            return true;
        }

        #endregion

        #region Call Crafter
        public async Task<bool> CallCrafter()
        {
            return true;
        }

        #endregion

        #region Send Banned Addresses
        public async Task<List<Validators>?> GetBannedMasternodes()
        {
            var validatorList = Validators.Validator.GetAll();
            var validatorListCount = validatorList.Count();

            if (validatorListCount == 0)
            {
                return null;
            }
            else
            {
                var bannedNodes = validatorList.FindAll().Where(x => x.FailCount >= 10).ToList();
                if (bannedNodes.Count() > 0)
                {
                    return bannedNodes;
                }
            }

            return null;
        }
        #endregion 

        #region Check Masternode
        public async Task<bool> MasternodeOnline()
        {
            return true;
        }

        #endregion

        #region Seed node check
        public async Task<string> SeedNodeCheck()
        {
            //do check for validator. if yes return val otherwise return Hello.
            var validators = Validators.Validator.GetAll();
            var hasValidators = validators.FindAll().Where(x => x.NodeIP == "SELF").Count(); //revise this to use local account and IsValidating

            if (hasValidators > 0)
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
