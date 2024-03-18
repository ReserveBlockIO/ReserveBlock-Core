using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.DST;
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
                if(!portCheck && !Globals.IsTestNet) 
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

                if (Globals.ValidatorNodes.ContainsKey(peerIP))
                {
                    var vNode = Globals.ValidatorNodes[peerIP];
                    if(vNode.IsConnected)
                    {
                        _ = EndOnConnect(peerIP, address + " attempted to connect as validator", address + " attempted to connect as validator");
                        return;
                    }
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
                        $"Connected, but you do not have the minimum balance of {ValidatorService.ValidatorRequiredAmount()} RBX. You are being disconnected.",
                        $"Connected, but you do not have the minimum balance of {ValidatorService.ValidatorRequiredAmount()} RBX: " + address);
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

            Globals.P2PPeerDict.TryRemove(peerIP, out _);
            Globals.ValidatorNodes.TryRemove(peerIP, out _);
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
                var result = await BlockValidatorService.ValidateBlock(nextBlock, false, false, true);
                if(result)
                {
                    Globals.NetworkBlockQueue.TryAdd(nextBlock.Height, nextBlock);

                    var blockJson = JsonConvert.SerializeObject(nextBlock);

                    if(!Globals.BlockQueueBroadcasted.TryGetValue(nextBlock.Height, out var lastBroadcast))
                    {
                        Globals.BlockQueueBroadcasted.TryAdd(nextBlock.Height, DateTime.UtcNow);
                        _ = Clients.All.SendAsync("GetValMessage", "6", blockJson);
                    }
                    else
                    {
                        if(DateTime.UtcNow.AddSeconds(30) > lastBroadcast)
                        {
                            Globals.BlockQueueBroadcasted[nextBlock.Height] = DateTime.UtcNow;
                            _ = Clients.All.SendAsync("GetValMessage", "6", blockJson);
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

        #region Send to Mempool
        public async Task<string> SendTxToMempoolVals(Transaction txReceived)
        {
            //This needs to have new modified network reach
            //No longer uses ADJs
            return "";
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

        #region Get Wallet Version
        public async Task<string> GetWalletVersionVal()
        {
            return await SignalRQueue(Context, Globals.CLIVersion.Length, async () => Globals.CLIVersion);
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
