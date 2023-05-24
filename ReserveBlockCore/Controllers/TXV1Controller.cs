using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ReserveBlockCore.Controllers
{
    [ActionFilterController]
    [Route("txapi/[controller]")]
    [Route("txapi/[controller]/{somePassword?}")]
    [ApiController]
    public class TXV1Controller : ControllerBase
    {
        /// <summary>
        /// Returns the timestamp of the given wallet.
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetTimestamp")]
        public async Task<string> GetTimestamp()
        {
            //use Id to get specific commands
            var output = "FAIL"; // this will only display if command not recognized.

            var timestamp = TimeUtil.GetTime();

            output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Timestamp Acquired.", Timestamp = timestamp });

            return output;
        }

        /// <summary>
        /// Returns a list of successful transactions that are local to wallet
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetSuccessfulLocalTX")]
        public async Task<string> GetSuccessfulLocalTX()
        {
            //use Id to get specific commands
            var output = "[]"; // this will only display if command not recognized.

            var txList = TransactionData.GetSuccessfulLocalTransactions();

            if(txList.Count() > 0)
            {
                output = JsonConvert.SerializeObject(txList);
            }
            
            return output;
        }

        /// <summary>
        /// Returns a list of reserved transactions that are local to wallet
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetReserveLocalTX")]
        public async Task<string> GetReserveLocalTX()
        {
            //use Id to get specific commands
            var output = "[]"; // this will only display if command not recognized.

            var txList = TransactionData.GetReserveLocalTransactions();

            if (txList.Count() > 0)
            {
                output = JsonConvert.SerializeObject(txList);
            }

            return output;
        }

        /// <summary>
        /// Returns a list of mined reward transactions that are local to wallet
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetMinedLocalTX")]
        public async Task<string> GetMinedLocalTX()
        {
            //use Id to get specific commands
            var output = "[]"; // this will only display if command not recognized.

            var txList = TransactionData.GetLocalMinedTransactions();

            if (txList.Count() > 0)
            {
                output = JsonConvert.SerializeObject(txList);
            }

            return output;
        }

        /// <summary>
        /// Returns a list of all transactions that are local to wallet
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetAllLocalTX")]
        public async Task<string> GetAllLocalTX()
        {
            //use Id to get specific commands
            var output = "[]"; // this will only display if command not recognized.

            var txList = TransactionData.GetAllLocalTransactions(true);

            if (txList.Count() > 0)
            {
                output = JsonConvert.SerializeObject(txList);
            }

            return output;
        }

        /// <summary>
        /// Returns a list of pending transactions that are local to wallet
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetPendingLocalTX")]
        public async Task<string> GetPendingLocalTX()
        {
            //use Id to get specific commands
            var output = "[]"; // this will only display if command not recognized.

            var txList = TransactionData.GetLocalPendingTransactions();

            if (txList.Count() > 0)
            {
                output = JsonConvert.SerializeObject(txList);
            }

            return output;
        }

        /// <summary>
        /// Returns a list of failed transactions that are local to wallet
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetFailedLocalTX")]
        public async Task<string> GetFailedLocalTX()
        {
            //use Id to get specific commands
            var output = "[]"; // this will only display if command not recognized.

            var txList = TransactionData.GetLocalFailedTransactions();

            if (txList.Count() > 0)
            {
                output = JsonConvert.SerializeObject(txList);
            }

            return output;
        }

        /// <summary>
        /// Returns a single transaction with hash as the search
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        [HttpGet("GetLocalTxByHash/{hash}")]
        public async Task<string> GetLocalTxByHash(string hash)
        {
            //use Id to get specific commands
            var output = "[]"; // this will only display if command not recognized.

            var txList = TransactionData.GetTxByHash(hash);

            if (txList != null)
            {
                output = JsonConvert.SerializeObject(txList);
            }

            return output;
        }

        /// <summary>
        /// Returns a single transaction with height as the search
        /// </summary>
        /// <param name="height"></param>
        /// <returns></returns>
        [HttpGet("GetLocalTxByBlock/{height}")]
        public async Task<string> GetLocalTxByBlock(long height)
        {
            //use Id to get specific commands
            var output = "[]"; // this will only display if command not recognized.

            var txList = TransactionData.GetTxByBlock(height);

            if (txList.Count() > 0)
            {
                output = JsonConvert.SerializeObject(txList);
            }

            return output;
        }

        /// <summary>
        /// Returns a all transactions before a block height as the search
        /// </summary>
        /// <param name="height"></param>
        /// <returns></returns>
        [HttpGet("GetLocalTxBeforeBlock/{height}")]
        public async Task<string> GetLocalTxBeforeBlock(long height)
        {
            //use Id to get specific commands
            var output = "[]"; // this will only display if command not recognized.

            var txList = TransactionData.GetLocalTransactionsBeforeBlock(height);

            if (txList.Count() > 0)
            {
                output = JsonConvert.SerializeObject(txList);
            }

            return output;
        }

        /// <summary>
        /// Returns a all transactions after a block height as the search
        /// </summary>
        /// <param name="height"></param>
        /// <returns></returns>
        [HttpGet("GetLocalTxAfterBlock/{height}")]
        public async Task<string> GetLocalTxAfterBlock(long height)
        {
            //use Id to get specific commands
            var output = "[]"; // this will only display if command not recognized.

            var txList = TransactionData.GetLocalTransactionsSinceBlock(height);

            if (txList.Count() > 0)
            {
                output = JsonConvert.SerializeObject(txList);
            }

            return output;
        }

        /// <summary>
        /// Returns a all transactions before a timestamp height as the search
        /// </summary>
        /// <param name="timestamp"></param>
        /// <returns></returns>
        [HttpGet("GetLocalTxBeforeTimestamp/{timestamp}")]
        public async Task<string> GetLocalTxBeforeTimestamp(long timestamp)
        {
            //use Id to get specific commands
            var output = "[]"; // this will only display if command not recognized.

            var txList = TransactionData.GetLocalTransactionsBeforeDate(timestamp);

            if (txList.Count() > 0)
            {
                output = JsonConvert.SerializeObject(txList);
            }

            return output;
        }

        /// <summary>
        /// Returns a all transactions after a timestamp height as the search
        /// </summary>
        /// <param name="timestamp"></param>
        /// <returns></returns>
        [HttpGet("GetLocalTxAfterTimestamp/{timestamp}")]
        public async Task<string> GetLocalTxAfterTimestamp(long timestamp)
        {
            //use Id to get specific commands
            var output = "[]"; // this will only display if command not recognized.

            var txList = TransactionData.GetLocalTransactionsSinceDate(timestamp);

            if (txList.Count() > 0)
            {
                output = JsonConvert.SerializeObject(txList);
            }

            return output;
        }

        /// <summary>
        /// Returns a all transactions for a specific address
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        [HttpGet("GetLocalTxByAddress/{address}")]
        public async Task<string> GetLocalTxByAddress(string address)
        {
            //use Id to get specific commands
            var output = "[]"; // this will only display if command not recognized.

            var txList = TransactionData.GetAllLocalTransactionsByAddress(address);

            if (txList.Count() > 0)
            {
                output = JsonConvert.SerializeObject(txList);
            }

            return output;
        }

        /// <summary>
        /// Returns a all transactions for a specific address
        /// </summary>
        /// <param name="address"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        [HttpGet("GetLocalTxByAddressLimit/{address}/{limit?}")]
        public async Task<string> GetLocalTxByAddressLimit(string address, int limit = 50)
        {
            //use Id to get specific commands
            var output = "[]"; // this will only display if command not recognized.

            var txList = TransactionData.GetAccountTransactionsLimit(address, limit);

            if (txList.Count() > 0)
            {
                output = JsonConvert.SerializeObject(txList);
            }

            return output;
        }

        /// <summary>
        /// Returns a all transactions for by page and limit per page
        /// </summary>
        /// <param name="address"></param>
        /// <param name="page"></param>
        /// <param name="amountPerPage"></param>
        /// <returns></returns>
        [HttpGet("GetLocalTxPaginated/{page}/{amountPerPage}/{address?}")]
        public async Task<string> GetLocalTxByAddressPaginated(int page, int amountPerPage, string? address = null)
        {
            //use Id to get specific commands
            var output = "[]"; // this will only display if command not recognized.

            var txList = TransactionData.GetTransactionsPaginated(page, amountPerPage, address);

            if (txList.Count() > 0)
            {
                output = JsonConvert.SerializeObject(txList);
            }

            return output;
        }

        /// <summary>
        /// Returns all transactions for ADNR
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetLocalADNRTX")]
        public async Task<string> GetLocalADNRTX()
        {
            //use Id to get specific commands
            var output = "[]"; // this will only display if command not recognized.

            var txList = TransactionData.GetLocalAdnrTransactions();

            if (txList.Count() > 0)
            {
                output = JsonConvert.SerializeObject(txList);
            }

            return output;
        }

        /// <summary>
        /// Returns and Address Nonce
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        [HttpGet("GetAddressNonce/{address}")]
        public async Task<string> GetAddressNonce(string address)
        {
            //use Id to get specific commands
            var output = "FAIL"; // this will only display if command not recognized.

            var nextNonce = AccountStateTrei.GetNextNonce(address);

            output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Next Nonce Found.", Nonce = nextNonce });

            return output;
        }

        /// <summary>
        /// Returns transaction from entire chain.
        /// Warning this uses parallelism so only use if you system can handle this.
        /// </summary>
        /// <param name="txHash"></param>
        /// <returns></returns>
        [HttpGet("GetNetworkTXByHash/{txHash}")]
        public async Task<string> GetNetworkTXByHash(string txHash)
        {
            var output = "";
            var coreCount = Environment.ProcessorCount;
            if (coreCount >= 4 || Globals.RunUnsafeCode)
            {
                if (!string.IsNullOrEmpty(txHash))
                {
                    try
                    {
                        txHash = txHash.Replace(" ", "");//removes any whitespace before or after in case left in.
                        var blocks = BlockchainData.GetBlocks();
                        var height = Convert.ToInt32(Globals.LastBlock.Height);
                        bool resultFound = false;

                        var integerList = Enumerable.Range(0, height + 1);
                        Parallel.ForEach(integerList, new ParallelOptions { MaxDegreeOfParallelism = coreCount == 4 ? 2 : 4 }, (blockHeight, loopState) =>
                        {
                            var block = blocks.Query().Where(x => x.Height == blockHeight).FirstOrDefault();
                            if (block != null)
                            {
                                var txs = block.Transactions.ToList();
                                var result = txs.Where(x => x.Hash == txHash).FirstOrDefault();
                                if (result != null)
                                {
                                    resultFound = true;
                                    output = JsonConvert.SerializeObject(new { Success = true, Message = result });
                                    loopState.Break();
                                }
                            }
                        });

                        if (!resultFound)
                            output = JsonConvert.SerializeObject(new { Success = false, Message = "No transaction found with that hash." });
                    }
                    catch (Exception ex)
                    {
                        output = JsonConvert.SerializeObject(new { Success = false, Message = $"Error Performing Query: {ex.ToString()}" });
                    }

                }
            }
            else
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = "The current system does not have enough physical/logical cores to safely run a query of this magnitude. You must enable 'RunUnsafeCode' in config file or add 'unsafe' to your start up parameters." });
            }

            return output;
        }

        /// <summary>
        /// Creates a minting tranasctions
        /// </summary>
        /// <param name="jsonData"></param>
        /// <returns></returns>
        [HttpPost("GetNFTMintData")]
        public async Task<string> GetNFTMintData([FromBody] object jsonData)
        {
            var output = "";

            var scMain = JsonConvert.DeserializeObject<SmartContractMain>(jsonData.ToString());

            try
            {
                if(scMain == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = "SC Main was null" });

                var result = await SmartContractWriterService.WriteSmartContract(scMain);

                var txData = "";

                if (result.Item1 != null)
                {
                    var md5List = await MD5Utility.GetMD5FromSmartContract(scMain);
                    var bytes = Encoding.Unicode.GetBytes(result.Item1);
                    var scBase64 = bytes.ToCompress().ToBase64();
                    var newSCInfo = new[]
                    {
                            new { Function = "Mint()", ContractUID = scMain.SmartContractUID, Data = scBase64, MD5List = md5List}
                    };

                    txData = JsonConvert.SerializeObject(newSCInfo);
                    var txJToken = JToken.Parse(txData.ToString());
                    //Type type = typeof(string);
                    //var dataTest = txJToken["Data"] != null ? txJToken["Data"].ToString(Formatting.None) : null;//sometest["Data"].ToObject<string>();
                    //txJToken["Data"] = dataTest;
                    output = txData;
                }
            }
            catch(Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = ex.ToString()});
            }

            return output;
        }

        /// <summary>
        /// Creates a beacon upload request transactions
        /// </summary>
        /// <param name="scUID"></param>
        /// <param name="toAddress"></param>
        /// <param name="signature"></param>
        /// <returns></returns>
        [HttpGet("CreateBeaconUploadRequest/{scUID}/{toAddress}/{**signature}")]
        public async Task<string> CreateBeaconUploadRequest(string scUID, string toAddress, string signature)
        {
            var output = "MethodStarted";
            toAddress = toAddress.ToAddressNormalize();
            var scStateTrei = SmartContractStateTrei.GetSmartContractState(scUID);
            if (scStateTrei != null)
            {
                var sc = SmartContractMain.GenerateSmartContractInMemory(scStateTrei.ContractData);
                if (sc != null)
                {
                    if (sc.IsPublished == true)
                    {
                        //Get beacons here!
                        if (!Globals.Beacon.Values.Where(x => x.IsConnected).Any())
                        {
                            var beaconConnectionResult = await BeaconUtility.EstablishBeaconConnection(true, false);
                            if (!beaconConnectionResult)
                            {
                                output = JsonConvert.SerializeObject(new { Success = false, Message = "You are not connected to any beacons."});
                                NFTLogUtility.Log("Error - You failed to connect to any beacons.", "TXV1Controller.CreateBeaconUploadRequest()");
                                return output;
                            }
                        }
                        else
                        {
                            var connectedBeacon = Globals.Beacon.Values.Where(x => x.IsConnected).FirstOrDefault();
                            if (connectedBeacon == null)
                            {
                                output = JsonConvert.SerializeObject(new { Success = false, Message = "You have lost connection to beacons. Please attempt to resend." });
                                NFTLogUtility.Log("Error - You have lost connection to beacons. Please attempt to resend.", "TXV1Controller.CreateBeaconUploadRequest()");
                                return output;
                            }

                            var assetList = await NFTAssetFileUtility.GetAssetListFromSmartContract(sc);
                            var md5List = scStateTrei.MD5List;

                            if (assetList == null)
                                return JsonConvert.SerializeObject(new { Success = false, Message = "Asset List was Null" }); ;

                            var result = await P2PClient.BeaconUploadRequest(connectedBeacon, assetList, sc.SmartContractUID, toAddress, md5List, signature).WaitAsync(new TimeSpan(0, 0, 10));
                            if (result == true)
                            {
                                var aqResult = AssetQueue.CreateAssetQueueItem(sc.SmartContractUID, toAddress, connectedBeacon.Beacons.BeaconLocator, md5List, assetList,
                                    AssetQueue.TransferType.Upload);
                                if (aqResult)
                                {
                                    //DO TRANSFER HERE
                                    _ = Task.Run(() => BeaconUtility.SendAssets_New(sc.SmartContractUID, assetList, connectedBeacon));

                                    var success = JsonConvert.SerializeObject(new { Success = true, Message = "NFT Transfer has been started.", Locator = connectedBeacon.Beacons.BeaconLocator });
                                    return success;
                                }
                                else
                                {
                                    return JsonConvert.SerializeObject(new { Success = false, Message = "Creating asset queue has failed." });
                                }
                            }
                            else
                            {
                                return JsonConvert.SerializeObject(new { Success = false, Message = "Beacon Upload Request has Failed." });
                            }
                        }
                    }
                }
            }

            return JsonConvert.SerializeObject(new { Success = false, Message = "Process Failed" });
        }

        /// <summary>
        /// Creates a NFT transfer transaction
        /// </summary>
        /// <param name="scUID"></param>
        /// <param name="toAddress"></param>
        /// <param name="locator"></param>
        /// <returns></returns>
        [HttpGet("GetNFTTransferData/{scUID}/{toAddress}/{**locator}")]
        public async Task<string> GetNFTTransferData(string scUID, string toAddress, string locator)
        {
            var output = "";
            var scStateTrei = SmartContractStateTrei.GetSmartContractState(scUID);
            toAddress = toAddress.ToAddressNormalize();

            if (scStateTrei == null)
                return JsonConvert.SerializeObject(new { Success = false, Message = "State trei record cannot be null." });

            var sc = SmartContractMain.GenerateSmartContractInMemory(scStateTrei.ContractData);
            try
            {
                var txData = "";
                var newSCInfo = new[]
                {
                    new
                    {
                        Function = "Transfer()",
                        ContractUID = sc.SmartContractUID,
                        ToAddress = toAddress,
                        Data = scStateTrei.ContractData,
                        Locators = locator, //either beacons, or self kept (NA).
                        MD5List = scStateTrei.MD5List
                    }
                };

                txData = JsonConvert.SerializeObject(newSCInfo);
                var txJToken = JToken.Parse(txData.ToString());
                output = txData;
            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = ex.ToString() });
            }

            return output;
        }

        /// <summary>
        /// Creates a NFT evolve transaction data
        /// </summary>
        /// <param name="scUID"></param>
        /// <param name="toAddress"></param>
        /// <param name="evoState"></param>
        /// <returns></returns>
        [HttpGet("GetNFTEvolveData/{scUID}/{toAddress}/{evoState}")]
        public async Task<string> GetNFTEvolveData(string scUID, string toAddress, int evoState)
        {
            var output = "";
            try
            {
                toAddress = toAddress.ToAddressNormalize();

                var smartContractStateTrei = SmartContractStateTrei.GetSmartContractState(scUID);
                if (smartContractStateTrei == null)
                    return JsonConvert.SerializeObject(new { Success = false, Message = "Smart Contract State was Null." });

                var minterAddress = smartContractStateTrei.MinterAddress;
                var evolve = await EvolvingFeature.GetNewSpecificState(smartContractStateTrei.ContractData, evoState);

                var evolveResult = evolve.Item1;
                if (evolveResult != true)
                    return JsonConvert.SerializeObject(new { Success = false, Message = "Failed to process new evolutionary state." });

                var evolveData = evolve.Item2;
                var bytes = Encoding.Unicode.GetBytes(evolveData);
                var scBase64 = SmartContractUtility.Compress(bytes).ToBase64();

                var newSCInfo = new[]
                {
                    new { Function = "ChangeEvolveStateSpecific()", ContractUID = scUID, FromAddress = minterAddress, ToAddress = toAddress, NewEvoState = evoState, Data = scBase64}
                };

                var txData = JsonConvert.SerializeObject(newSCInfo);

                return txData;

            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = ex.ToString() });
            }

            return output;
        }

        /// <summary>
        /// Creates a NFT Burn data transactions
        /// </summary>
        /// <param name="scUID"></param>
        /// <param name="fromAddress"></param>
        /// <returns></returns>
        [HttpGet("GetNFTBurnData/{scUID}/{fromAddress}/")]
        public async Task<string> GetNFTBurnData(string scUID, string fromAddress)
        {
            var output = "";
            var scStateTrei = SmartContractStateTrei.GetSmartContractState(scUID);

            if(scStateTrei == null)
            {
                output = JsonConvert.SerializeObject(new {Success = false, Message = "Smart contract does not exist." });
                return output;
            }
            
            try
            {
                var txData = "";
                var newSCInfo = new[]
                {
                        new { Function = "Burn()", 
                            ContractUID = scUID, 
                            FromAddress = fromAddress}
                };

                txData = JsonConvert.SerializeObject(newSCInfo);
                var txJToken = JToken.Parse(txData.ToString());
                output = JsonConvert.SerializeObject(new {Success = true, Message = txData });
                
            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Success = false, Message = ex.ToString() });
            }

            return output;
        }

        /// <summary>
        /// Produces the estimated fee for a transaction
        /// </summary>
        /// <param name="jsonData"></param>
        /// <returns></returns>
        [HttpPost("GetRawTxFee")]
        public async Task<string> GetRawTxFee([FromBody] object jsonData)
        {
            var output = "";
            try
            {
                var txJToken = JToken.Parse(jsonData.ToString());
                Type type = typeof(string);
                var dataTest = txJToken["Data"] != null ? txJToken["Data"].ToString(Formatting.None) : null;//sometest["Data"].ToObject<string>();
                txJToken["Data"] = dataTest;
                var tx = JsonConvert.DeserializeObject<Transaction>(txJToken.ToString());

                var nTx = new Transaction
                {
                    Timestamp = tx.Timestamp,
                    FromAddress = tx.FromAddress,
                    ToAddress = tx.ToAddress.ToAddressNormalize(),
                    Amount = tx.Amount + 0.0M,
                    Fee = 0,
                    Nonce = AccountStateTrei.GetNextNonce(tx.FromAddress),
                    TransactionType = tx.TransactionType,
                    Data = tx.Data
                };

                //Calculate fee for tx.
                nTx.Fee = FeeCalcService.CalculateTXFee(nTx);

                output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"TX Fee Calculated", Fee = nTx.Fee });
            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Failed to calcuate Fee. Error: {ex.ToString()}" });
            }

            return output;
        }

        /// <summary>
        /// Produces the TX Hash
        /// </summary>
        /// <param name="jsonData"></param>
        /// <returns></returns>
        [HttpPost("GetTxHash")]
        public async Task<string> GetTxHash([FromBody] object jsonData)
        {
            var output = "";
            try
            {
                var txJToken = JToken.Parse(jsonData.ToString());
                Type type = typeof(string);
                var dataTest = txJToken["Data"] != null ? txJToken["Data"].ToString(Formatting.None) : null;//sometest["Data"].ToObject<string>();
                txJToken["Data"] = dataTest;
                var tx = JsonConvert.DeserializeObject<Transaction>(txJToken.ToString());

                if(tx != null)
                {
                    tx.ToAddress = tx.ToAddress.ToAddressNormalize();
                    tx.Amount = tx.Amount.ToNormalizeDecimal();

                    tx.Build();

                    output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Hash Calculated.", Hash = tx.Hash });
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Could not deserialize raw TX." });
                }
            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Failed to create Hash. Error: {ex.ToString()}" });
            }

            return output;
        }

        //Step 5.
        //You create the signature now in your application.

        /// <summary>
        /// Validate a signature
        /// </summary>
        /// <param name="message"></param>
        /// <param name="address"></param>
        /// <param name="sigScript"></param>
        /// <returns></returns>
        [HttpGet("ValidateSignature/{message}/{address}/{**sigScript}")]
        public async Task<string> ValidateSignature(string message, string address, string sigScript)
        {
            string output;

            try
            {
                var result = SignatureService.VerifySignature(address, message, sigScript);

                if (result == true)
                {
                    output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Signature Verified." });
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Signature Not Verified." });
                }
            }
            catch(Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Signature Not Verified. Unknown Error: {ex.ToString()}" });
            }
            
            return output;
        }

        //If validation was true
        //Step 7.
        /// <summary>
        /// Verify a raw transaction
        /// </summary>
        /// <returns></returns>
        [HttpPost("VerifyRawTransaction")]
        public async Task<string> VerifyRawTransaction([FromBody] object jsonData)
        {
            var output = "";
            try
            {
                var txJToken = JToken.Parse(jsonData.ToString());
                Type type = typeof(string);
                var dataTest = txJToken["Data"] != null ? txJToken["Data"].ToString(Formatting.None) : null;//sometest["Data"].ToObject<string>();
                txJToken["Data"] = dataTest;
                var transaction = JsonConvert.DeserializeObject<Transaction>(txJToken.ToString());

                if (transaction != null)
                {
                    transaction.ToAddress = transaction.ToAddress.ToAddressNormalize();
                    transaction.Amount = transaction.Amount.ToNormalizeDecimal();

                    var result = await TransactionValidatorService.VerifyTX(transaction);
                    if (result.Item1 == true)
                    {

                        output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Transaction has been verified.", Hash = transaction.Hash });
                    }
                    else
                    {
                        output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Transaction was not verified. Error: {result.Item2}" });
                    }
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Failed to deserialize transaction. Please try again." });
                }

            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Error - {ex.ToString()}. Please Try Again." });
            }

            return output;
        }

        //Step 8.
        /// <summary>
        /// Sends a raw TX over the network
        /// </summary>
        /// <returns></returns>
        [HttpPost("SendRawTransaction")]
        public async Task<string> SendRawTransaction([FromBody] object jsonData)
        {
            var output = "";
            try
            {
                var txJToken = JToken.Parse(jsonData.ToString());
                Type type = typeof(string);
                var dataTest = txJToken["Data"] != null ? txJToken["Data"].ToString(Formatting.None) : null;//sometest["Data"].ToObject<string>();
                txJToken["Data"] = dataTest;
                var transaction = JsonConvert.DeserializeObject<Transaction>(txJToken.ToString());
                if (transaction != null)
                {
                    transaction.ToAddress = transaction.ToAddress.ToAddressNormalize();
                    transaction.Amount = transaction.Amount.ToNormalizeDecimal();

                    var result = await TransactionValidatorService.VerifyTX(transaction);
                    if (result.Item1 == true)
                    {
                        if (transaction.TransactionRating == null)
                        {
                            var rating = await TransactionRatingService.GetTransactionRating(transaction);
                            transaction.TransactionRating = rating;
                        }

                        TransactionData.AddToPool(transaction);
                        await P2PClient.SendTXMempool(transaction);//send out to mempool

                        output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Transaction has been broadcasted.", Hash = transaction.Hash });
                    }
                    else
                    {
                        output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Transaction was not verified." });
                    }
                }
                else
                {

                    output = JsonConvert.SerializeObject(new { Result = "Fail", Message = "Failed to deserialize transaction. Please try again." });
                }

            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Error - {ex.ToString()}. Please Try Again." });
            }

            return output;
        }

        /// <summary>
        /// Test raw transaction is received and return the input
        /// </summary>
        /// <returns></returns>
        [HttpPost("TestRawTransaction")]
        public string TestRawTransaction([FromBody] object jsonData)
        {
            var output = jsonData.ToString();
            try
            {
                var tx = JsonConvert.DeserializeObject<Transaction>(jsonData.ToString());

                var json = JsonConvert.SerializeObject(tx);

                output = json;
            }
            catch (Exception ex)
            {
                output = $"Error - {ex.ToString()}. Please Try Again.";
            }

            return output;
        }

        /// <summary>
        /// Create an ADNR and associate it to address
        /// </summary>
        /// <param name="address"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        [HttpGet("CreateAdnr/{address}/{name}")]
        public async Task<string> CreateAdnr(string address, string name)
        {
            string output = "";

            try
            {
                var wallet = AccountData.GetSingleAccount(address);
                if(wallet != null)
                {
                    var addressFrom = wallet.Address;
                    var adnr = Adnr.GetAdnr();
                    if(adnr != null)
                    {
                        var adnrAddressCheck = adnr.FindOne(x => x.Address == addressFrom);
                        if (adnrAddressCheck != null)
                        {
                            output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"This address already has a DNR associated with it: {adnrAddressCheck.Name}" });
                            return output;
                        }

                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            name = name.ToLower();

                            var limit = Globals.ADNRLimit;

                            if(name.Length > limit)
                            {
                                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = "A DNR may only be a max of 65 characters" });
                                return output;
                            }

                            var nameCharCheck = Regex.IsMatch(name, @"^[a-zA-Z0-9]+$");
                            if (!nameCharCheck)
                            {
                                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = "A DNR may only contain letters and numbers." });
                                return output;
                            }
                            else
                            {
                                var nameRBX = name.ToLower() + ".rbx";
                                var nameCheck = adnr.FindOne(x => x.Name == nameRBX);
                                if (nameCheck == null)
                                {
                                    var result = await Adnr.CreateAdnrTx(address, name);
                                    if (result.Item1 != null)
                                    {
                                        output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Transaction has been broadcasted.", Hash = result.Item1.Hash });
                                    }
                                    else
                                    {
                                        output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Transaction failed to broadcast. Error: {result.Item2}" });
                                    }
                                }
                                else
                                {
                                    output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"This name already has a DNR associated with it: {nameCheck.Name}" });
                                    return output;
                                }
                            }

                        }
                        else
                        {
                            output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Name was empty." });
                        }
                    }
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Account with address: {address} was not found." });
                    return output;
                }
            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Unknown Error: {ex.ToString()}" });
            }

            return output;
        }

        /// <summary>
        /// Transfer ADNR from one address to another
        /// </summary>
        /// <param name="fromAddress"></param>
        /// <param name="toAddress"></param>
        /// <returns></returns>
        [HttpGet("TransferAdnr/{fromAddress}/{toAddress}")]
        public async Task<string> TransferAdnr(string fromAddress, string toAddress)
        {
            string output = "";

            try
            {
                var wallet = AccountData.GetSingleAccount(fromAddress);
                if (wallet != null)
                {
                    var addressFrom = wallet.Address;
                    var adnr = Adnr.GetAdnr();
                    if (adnr != null)
                    {
                        var adnrCheck = adnr.FindOne(x => x.Address == addressFrom);
                        if (adnrCheck == null)
                        {
                            output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"This address does not have a DNR associated with it." });
                            return output;
                        }
                        if (!string.IsNullOrWhiteSpace(toAddress))
                        {
                            var addrVerify = AddressValidateUtility.ValidateAddress(toAddress);
                            if (addrVerify == true)
                            {
                                var toAddrAdnr = adnr.FindOne(x => x.Address == toAddress);
                                if(toAddrAdnr == null)
                                {
                                    var result = await Adnr.TransferAdnrTx(fromAddress, toAddress);
                                    if (result.Item1 != null)
                                    {
                                        output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Transaction has been broadcasted.", Hash = result.Item1.Hash });
                                    }
                                    else
                                    {
                                        output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Transaction failed to broadcast. Error: {result.Item2}" });
                                    }
                                }
                                else
                                {
                                    output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"To Address already has adnr associated to it." });
                                }
                            }
                            else
                            {
                                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"To Address is not a valid RBX address." });
                            }

                        }
                        else
                        {
                            output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Name was empty." });
                        }
                    }
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Account with address: {fromAddress} was not found." });
                    return output;
                }
            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Unknown Error: {ex.ToString()}" });
            }

            return output;
        }

        /// <summary>
        /// Permanently remove ADNR from address.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        [HttpGet("DeleteAdnr/{address}")]
        public async Task<string> DeleteAdnr(string address)
        {
            string output = "";

            try
            {
                var wallet = AccountData.GetSingleAccount(address);
                if (wallet != null)
                {
                    var addressFrom = wallet.Address;
                    var adnr = Adnr.GetAdnr();
                    if (adnr != null)
                    {
                        var adnrCheck = adnr.FindOne(x => x.Address == addressFrom);
                        if (adnrCheck == null)
                        {
                            output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"This address already has a DNR associated with it: {adnrCheck.Name}" });
                            return output;
                        }

                        var result = await Adnr.DeleteAdnrTx(address);
                        if (result.Item1 != null)
                        {
                            output = JsonConvert.SerializeObject(new { Result = "Success", Message = $"Transaction has been broadcasted.", Hash = result.Item1.Hash });
                        }
                        else
                        {
                            output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Transaction failed to broadcast. Error: {result.Item2}" });
                        }
                    }
                }
                else
                {
                    output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Account with address: {address} was not found." });
                    return output;
                }
            }
            catch (Exception ex)
            {
                output = JsonConvert.SerializeObject(new { Result = "Fail", Message = $"Unknown Error: {ex.ToString()}" });
            }

            return output;
        }

        /// <summary>
        /// Shows the fortis pool work broadcast list of txs
        /// </summary>
        /// <returns></returns>
        [HttpGet("GetFortisBroadcastTx")]
        public async Task<string> GetFortisBroadcastTx()
        {
            var output = "";

            var txlist = Globals.BroadcastedTrxDict.Values.ToList();

            if (txlist.Count > 0)
            {
                output = JsonConvert.SerializeObject(txlist);
            }

            return output;
        }


        /// <summary>
        /// Send a transaction. Specify from, to, and amount
        /// </summary>
        /// <param name="faddr"></param>
        /// <param name="taddr"></param>
        /// <param name="amt"></param>
        /// <returns></returns>
        [HttpGet("SendTransaction/{faddr}/{taddr}/{amt}")]
        public async Task<string> SendTransaction(string faddr, string taddr, string amt)
        {
            var output = "FAIL";
            var fromAddress = faddr;
            var toAddress = taddr;
            var strAmount = amt;

            var addrCheck = AddressValidateUtility.ValidateAddress(toAddress);

            if (addrCheck == false)
            {
                output = "This is not a valid RBX address to send to. Please verify again.";
                return output;
            }

            decimal amount = new decimal();

            try
            {
                amount = decimal.Parse(strAmount);
            }
            catch
            {
                return output;
            }

            if (Globals.IsWalletEncrypted == true)
            {
                if (Globals.EncryptPassword.Length > 0)
                {
                    var result = await WalletService.SendTXOut(fromAddress, toAddress, amount);

                    output = result;
                }
                else
                {
                    output = "FAIL. Please type in wallet encryption password first.";
                }
            }
            else
            {
                var result = await WalletService.SendTXOut(fromAddress, toAddress, amount);
                output = result;
            }

            return output;
        }
    }
}
