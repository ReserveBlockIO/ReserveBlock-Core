using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.DST;
using ReserveBlockCore.Nodes;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;

namespace ReserveBlockCore.P2P
{
    public class P2PValidatorServer : P2PServer
    {
        #region On Connected 
        public override async Task OnConnectedAsync()
        {
            string peerIP = null;
            try
            {
                peerIP = GetIP(Context);

                if (Globals.BannedIPs.ContainsKey(peerIP))
                {
                    Context.Abort();
                    return;
                }
                var httpContext = Context.GetHttpContext();

                if (httpContext == null)
                {
                    _ = EndOnConnect(peerIP, "httpContext is null", "httpContext is null");
                    return;
                }

                var portCheck = PortUtility.IsPortOpen(peerIP, Globals.ValPort);
                if(!portCheck) 
                {
                    _ = EndOnConnect(peerIP, $"Port: {Globals.ValPort} was not detected as open.", $"Port: {Globals.ValPort} was not detected as open for IP: {peerIP}.");
                    return;
                }

                var address = httpContext.Request.Headers["address"].ToString();
                var time = httpContext.Request.Headers["time"].ToString();
                var uName = httpContext.Request.Headers["uName"].ToString();
                var publicKey = httpContext.Request.Headers["publicKey"].ToString();
                var signature = httpContext.Request.Headers["signature"].ToString();
                var walletVersion = httpContext.Request.Headers["walver"].ToString();

                Globals.P2PValDict.TryAdd(peerIP, Context);

                if (Globals.P2PValDict.TryGetValue(peerIP, out var context) && context.ConnectionId != Context.ConnectionId)
                {
                    context.Abort();
                }

                var SignedMessage = address;
                var Now = TimeUtil.GetTime();
                SignedMessage = address + ":" + time + ":" + publicKey;
                if (TimeUtil.GetTime() - long.Parse(time) > 300)
                {
                    _ = EndOnConnect(peerIP, "Signature Bad time.", "Signature Bad time.");
                    return;
                }

                var walletVersionVerify = WalletVersionUtility.Verify(walletVersion);

                if (string.IsNullOrWhiteSpace(address) || 
                    string.IsNullOrWhiteSpace(publicKey) || 
                    string.IsNullOrWhiteSpace(signature))
                {
                    _ = EndOnConnect(peerIP,
                        "Connection Attempted, but missing field(s). Address, and Public Key required. You are being disconnected.",
                        "Connected, but missing field(s). Address, and Public Key required: " + address);
                    return;
                }
                var stateAddress = StateData.GetSpecificAccountStateTrei(address);
                if (stateAddress == null)
                {
                    _ = EndOnConnect(peerIP,
                        "Connection Attempted, But failed to find the address in trie. You are being disconnected.",
                        "Connection Attempted, but missing field Address: " + address + " IP: " + peerIP);
                    return;
                }

                if (stateAddress.Balance < ValidatorService.ValidatorRequiredAmount())
                {
                    _ = EndOnConnect(peerIP,
                        $"Connected, but you do not have the minimum balance of {ValidatorService.ValidatorRequiredAmount()} VFX. You are being disconnected.",
                        $"Connected, but you do not have the minimum balance of {ValidatorService.ValidatorRequiredAmount()} VFX: " + address);
                    return;
                }

                var verifySig = SignatureService.VerifySignature(address, SignedMessage, signature);
                if (!verifySig)
                {
                    _ = EndOnConnect(peerIP,
                        "Connected, but your address signature failed to verify. You are being disconnected.",
                        "Connected, but your address signature failed to verify with Val: " + address);
                    return;
                }

                var netVal = new NetworkValidator { 
                    Address = address,
                    IPAddress = peerIP,
                    LastBlockProof = 0,
                    PublicKey = publicKey,
                    Signature = signature,
                    SignatureMessage = SignedMessage,
                    UniqueName = uName,
                    Context = Context
                };

                Globals.NetworkValidators.TryAdd(address, netVal);

                //Have to null context out as json cannot serialize.
                netVal.Context = null;
                var netValSerialize = JsonConvert.SerializeObject(netVal);

                _ = Peers.UpdatePeerAsVal(peerIP);
                _ = Clients.Caller.SendAsync("GetValMessage", "1", peerIP, new CancellationTokenSource(2000).Token);
                _ = Clients.All.SendAsync("GetValMessage", "2", netValSerialize, new CancellationTokenSource(6000).Token);

            }
            catch (Exception ex)
            {
                Context?.Abort();
                ErrorLogUtility.LogError($"Unhandled exception has happend. Error : {ex.ToString()}", "P2PValidatorServer.OnConnectedAsync()");
            }

        }
        public override async Task OnDisconnectedAsync(Exception? ex)
        {
            var peerIP = GetIP(Context);
            //var netVal = Globals.NetworkValidators.Where(x => x.Value.IPAddress == peerIP).FirstOrDefault();

            Globals.P2PValDict.TryRemove(peerIP, out _);
            Context?.Abort();

            await base.OnDisconnectedAsync(ex);
        }
        private async Task SendValMessageSingle(string message, string data)
        {
            await Clients.Caller.SendAsync("GetValMessage", message, data, new CancellationTokenSource(1000).Token);
        }
        #endregion

        #region Get Network Validator List - TODO: ADD PROTECTION
        public async Task SendNetworkValidatorList(string data)
        {
            try
            {
                var peerIP = GetIP(Context);

                if(!string.IsNullOrEmpty(data))
                {
                    var networkValList = JsonConvert.DeserializeObject<List<NetworkValidator>>(data);
                    if(networkValList?.Count > 0)
                    {
                        foreach(var networkValidator in networkValList)
                        {
                            if(Globals.NetworkValidators.TryGetValue(networkValidator.Address, out var networkValidatorVal))
                            {
                                var verifySig = SignatureService.VerifySignature(
                                    networkValidator.Address, 
                                    networkValidator.SignatureMessage, 
                                    networkValidator.Signature);

                                //if(networkValidatorVal.PublicKey != networkValidator.PublicKey)

                                if(verifySig && networkValidator.Signature.Contains(networkValidator.PublicKey))
                                    Globals.NetworkValidators[networkValidator.Address] = networkValidator;

                            }
                            else
                            {
                                Globals.NetworkValidators.TryAdd(networkValidator.Address, networkValidator);
                            }
                        }
                    }
                }
            }
            catch { }
        }
        #endregion

        #region Send Block Height
        public async Task<long> SendBlockHeightForVals()
        {
            return Globals.LastBlock.Height;
        }

        #endregion

        #region Receive Block - Receives Block and then Broadcast out.
        public async Task<bool> ReceiveBlockVal(Block nextBlock)
        {
            try
            {
                return await SignalRQueue(Context, (int)nextBlock.Size, async () =>
                {
                    if (nextBlock.ChainRefId == BlockchainData.ChainRef)
                    {
                        var IP = GetIP(Context);
                        var nextHeight = Globals.LastBlock.Height + 1;
                        var currentHeight = nextBlock.Height;

                        if (currentHeight >= nextHeight && BlockDownloadService.BlockDict.TryAdd(currentHeight, (nextBlock, IP)))
                        {
                            await Task.Delay(2000);

                            if(Globals.LastBlock.Height < nextBlock.Height)
                                await BlockValidatorService.ValidateBlocks();

                            if (nextHeight == currentHeight)
                            {
                                string data = "";
                                data = JsonConvert.SerializeObject(nextBlock);
                                await Clients.All.SendAsync("GetMessage", "blk", data);
                            }

                            if (nextHeight < currentHeight)
                                await BlockDownloadService.GetAllBlocks();

                            return true;
                        }
                    }

                    return false;
                });
            }
            catch { }

            return false;
        }

        #endregion

        #region Receives a Queued block from client

        public async Task<bool> ReceiveQueueBlockVal(Block nextBlock)
        {
            try
            {
                var lastBlock = Globals.LastBlock;
                if(lastBlock.Height < nextBlock.Height)
                {
                    var result = await BlockValidatorService.ValidateBlock(nextBlock, false, false, true);
                    if (result)
                    {
                        var blockAdded = Globals.NetworkBlockQueue.TryAdd(nextBlock.Height, nextBlock);

                        if(blockAdded)
                        {
                            var blockJson = JsonConvert.SerializeObject(nextBlock);

                            if (!Globals.BlockQueueBroadcasted.TryGetValue(nextBlock.Height, out var lastBroadcast))
                            {
                                Globals.BlockQueueBroadcasted.TryAdd(nextBlock.Height, DateTime.UtcNow);
                                _ = Clients.All.SendAsync("GetValMessage", "6", blockJson);
                            }
                            else
                            {
                                if (DateTime.UtcNow.AddSeconds(30) > lastBroadcast)
                                {
                                    Globals.BlockQueueBroadcasted[nextBlock.Height] = DateTime.UtcNow;
                                    _ = Clients.All.SendAsync("GetValMessage", "6", blockJson);
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        #endregion

        #region Send Queued Block - returns specific block
        //Send Block to client from p2p server
        public async Task<Block?> SendQueuedBlock(long currentBlock)
        {
            try
            {
                if(Globals.NetworkBlockQueue.TryGetValue(currentBlock, out var block))
                {
                    return block;
                }
            }
            catch { }

            return null;

        }

        #endregion

        #region Send Block - returns specific block
        //Send Block to client from p2p server
        public async Task<Block?> SendBlockVal(long currentBlock)
        {
            try
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
            catch { }

            return null;

        }

        #endregion

        #region Send to Mempool Vals
        public async Task<string> SendTxToMempoolVals(Transaction txReceived)
        {
            try
            {
                return await SignalRQueue(Context, (txReceived.Data?.Length ?? 0) + 1024, async () =>
                {
                    var result = "";

                    var data = JsonConvert.SerializeObject(txReceived);

                    var mempool = TransactionData.GetPool();

                    if (mempool.Exists(x => x.Hash == txReceived.Hash))
                        return "ATMP";

                    if (mempool.Count() != 0)
                    {
                        var txFound = mempool.FindOne(x => x.Hash == txReceived.Hash);
                        if (txFound == null)
                        {
                            var isTxStale = await TransactionData.IsTxTimestampStale(txReceived);
                            if (!isTxStale)
                            {
                                var txResult = await TransactionValidatorService.VerifyTX(txReceived); //sends tx to connected peers
                                if (txResult.Item1 == false)
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
                                var dblspndChk = await TransactionData.DoubleSpendReplayCheck(txReceived);
                                var isCraftedIntoBlock = await TransactionData.HasTxBeenCraftedIntoBlock(txReceived);
                                var rating = await TransactionRatingService.GetTransactionRating(txReceived);
                                txReceived.TransactionRating = rating;

                                if (txResult.Item1 == true && dblspndChk == false && isCraftedIntoBlock == false && rating != TransactionRating.F)
                                {
                                    mempool.InsertSafe(txReceived);
                                    _ = ValidatorProcessor.Broadcast("7777", data, "SendTxToMempoolVals");

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
                                if (isCraftedIntoBlock)
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
                            if (!txResult.Item1)
                            {
                                try
                                {
                                    mempool.DeleteManySafe(x => x.Hash == txReceived.Hash);// tx has been crafted into block. Remove.
                                }
                                catch { }

                                return "TFVP";
                            }
                            var dblspndChk = await TransactionData.DoubleSpendReplayCheck(txReceived);
                            var isCraftedIntoBlock = await TransactionData.HasTxBeenCraftedIntoBlock(txReceived);
                            var rating = await TransactionRatingService.GetTransactionRating(txReceived);
                            txReceived.TransactionRating = rating;

                            if (txResult.Item1 == true && dblspndChk == false && isCraftedIntoBlock == false && rating != TransactionRating.F)
                            {
                                mempool.InsertSafe(txReceived);
                                if (!string.IsNullOrEmpty(Globals.ValidatorAddress))
                                {
                                    _ = ValidatorProcessor.Broadcast("7777", data, "SendTxToMempoolVals");
                                } //sends tx to connected peers
                                return "ATMP";//added to mempool
                            }
                            else
                            {
                                try
                                {
                                    mempool.DeleteManySafe(x => x.Hash == txReceived.Hash);// tx has been crafted into block. Remove.
                                }
                                catch { }

                                return "TFVP"; //transaction failed verification process
                            }
                        }

                    }

                    return "";
                });
            }
            catch { }

            return "TFVP";
        }

        #endregion

        #region Get Validator Status
        public async Task<bool> GetValidatorStatusVal()
        {
            return await SignalRQueue(Context, bool.FalseString.Length, async () => !string.IsNullOrEmpty(Globals.ValidatorAddress));
        }

        #endregion

        #region Send Proof List (Receive it)

        public async Task<bool> SendProofList(string proofJson)
        {
            var proofList = JsonConvert.DeserializeObject<List<Proof>>(proofJson);

            if (proofList?.Count() == 0) return false;

            if (proofList == null) return false;

            await ProofUtility.SortProofs(proofList);

            return true;
        }

        #endregion

        #region Send Winning Proof List (Receive it)

        public async Task<bool> SendWinningProofList(string proofJson)
        {
            var proofList = JsonConvert.DeserializeObject<List<Proof>>(proofJson);

            if (proofList?.Count() == 0) return false;

            if (proofList == null) return false;

            await ProofUtility.SortProofs(proofList);

            return true;
        }

        #endregion

        #region Get Winning Proof List (Send it)

        public async Task<string> GetWinningProofList()
        {
            string result = "0";
            if(Globals.WinningProofs.Count() != 0)
            {
                var list = Globals.WinningProofs.Select(x => x.Value).ToList();
                if(list != null)
                    result = JsonConvert.SerializeObject(list);
            }

            return result;
        }

        #endregion

        #region Get Finalized Winners (Send it)

        public async Task<string> GetFinalizedWinnersList()
        {
            string result = "0";
            if (Globals.WinningProofs.Count() != 0)
            {
                var list = Globals.FinalizedWinner.Select(x => x.Value).ToList();
                if (list != null)
                    result = JsonConvert.SerializeObject(list);
            }

            return result;
        }

        #endregion

        #region Send locked Winner
        //Send locked winner to client from p2p server
        public async Task<string> SendLockedWinner(long height)
        {
            try
            {
                var peerIP = GetIP(Context);

                if(Globals.FinalizedWinner.TryGetValue(height, out var winner))
                {
                    return winner;
                }
                else
                {
                    return "0";
                }
            }
            catch { }

            return "0";
        }

        #endregion

        #region Get Wallet Version
        public async Task<string> GetWalletVersionVal()
        {
            return await SignalRQueue(Context, Globals.CLIVersion.Length, async () => Globals.CLIVersion);
        }

        #endregion

        #region Get Connected Val Count

        public static async Task<int> GetConnectedValCount()
        {
            try
            {
                var peerCount = Globals.P2PValDict.Count;
                return peerCount;
            }
            catch { }

            return -1;
        }

        #endregion

        #region End on Connect

        private async Task EndOnConnect(string ipAddress, string adjMessage, string logMessage)
        {
            await SendValMessageSingle("9999", adjMessage);
            if (Globals.OptionalLogging == true)
            {
                LogUtility.Log(logMessage, "Validator Connection");
                LogUtility.Log($"IP: {ipAddress} ", "Validator Connection");
            }

            Context?.Abort();
        }

        #endregion

        #region Get IP
        private static string GetIP(HubCallerContext context)
        {
            try
            {
                var peerIP = "NA";
                var feature = context.Features.Get<IHttpConnectionFeature>();
                if (feature != null)
                {
                    if (feature.RemoteIpAddress != null)
                    {
                        peerIP = feature.RemoteIpAddress.MapToIPv4().ToString();
                    }
                }

                return peerIP;
            }
            catch (Exception ex)
            {
                ErrorLogUtility.LogError($"Unknown Error: {ex.ToString()}", "ConsensusServer.GetIP()");
            }

            return "0.0.0.0";
        }

        #endregion
    }
}
