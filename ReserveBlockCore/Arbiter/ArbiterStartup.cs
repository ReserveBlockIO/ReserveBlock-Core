using ImageMagick;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using NBitcoin;
using NBitcoin.Protocol;
using Newtonsoft.Json;
using ReserveBlockCore.Bitcoin.Models;
using ReserveBlockCore.Bitcoin.Services;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.SmartContracts;
using ReserveBlockCore.SecretSharing.Cryptography;
using ReserveBlockCore.SecretSharing.Math;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System;
using System.Numerics;
using System.Text.Json;
using static ReserveBlockCore.Models.Integrations;

namespace ReserveBlockCore.Arbiter
{
    public class ArbiterStartup
    {
        public IConfiguration Configuration { get; }

        public ArbiterStartup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.Configure<FormOptions>(x =>
            {
                x.ValueLengthLimit = 4 * 1024 * 1024; // 4 MB
                x.MultipartBodyLengthLimit = 4 * 1024 * 1024; // 4 MB
            });
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    // Handle the GET request
                    var ipAddress = context.Connection.RemoteIpAddress?.MapToIPv4().ToString();
                    context.Response.StatusCode = StatusCodes.Status200OK;
                    await context.Response.WriteAsync($"Hello {ipAddress}, this is the server's response!");
                });

                endpoints.MapGet("/getsigneraddress", async context =>
                {
                    // Handle the GET request
                    if (Globals.ArbiterSigningAddress == null)
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        var response = JsonConvert.SerializeObject(new { Success = false, Message = $"No Signing Address" }, Formatting.Indented);
                        await context.Response.WriteAsync(response);
                        return;
                    }
                    else
                    {
                        var response = JsonConvert.SerializeObject(new { Success = true, Message = $"Address Found", Address = $"{Globals.ArbiterSigningAddress.Address}" }, Formatting.Indented);
                        context.Response.StatusCode = StatusCodes.Status200OK;
                        await context.Response.WriteAsync(response);
                        return;
                    }
                });

                endpoints.MapGet("/depositaddress/{address}/{**scUID}", async context =>
                {

                     var address = context.Request.RouteValues["address"] as string;
                     var scUID = context.Request.RouteValues["scUID"] as string;

                     if (string.IsNullOrEmpty(address))
                     {
                         context.Response.StatusCode = StatusCodes.Status400BadRequest;
                         var response = JsonConvert.SerializeObject(new { Success = false, Message = $"No Address" }, Formatting.Indented);
                         await context.Response.WriteAsync(response);
                         return;
                     }

                     var publicKey = BitcoinAccount.CreatePublicKeyForArbiter(Globals.ArbiterSigningAddress.GetKey, scUID);

                     var message = publicKey + scUID;
                    
                     var signature = Services.SignatureService.CreateSignature(message, Globals.ArbiterSigningAddress.GetPrivKey, Globals.ArbiterSigningAddress.PublicKey);

                     if(signature == "F")
                    
                     {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        var response = JsonConvert.SerializeObject(new { Success = false, Message = $"Signature Failed." }, Formatting.Indented);
                        await context.Response.WriteAsync(response);
                        return;
                    
                     }

                     context.Response.StatusCode = StatusCodes.Status200OK;
                     var requestorResponseJson = JsonConvert.SerializeObject(new { Success = true, Message = $"PubKey created", PublicKey = publicKey, Signature = signature }, Formatting.Indented);
                     await context.Response.WriteAsync(requestorResponseJson);
                     return;
                    
                });

                endpoints.MapPost("/getsignedmultisig",  async context =>
                {
                    // Handle the GET request
                    try
                    {
                        using (var reader = new StreamReader(context.Request.Body))
                        {
                            var body = await reader.ReadToEndAsync();
                            var postData = JsonConvert.DeserializeObject<PostData.MultiSigSigningPostData>(body);
                            if (postData != null)
                            {
                                var result = new
                                {
                                    Transaction = postData.TransactionData,
                                    ScriptCoinList = postData.ScriptCoinListData,
                                    postData.SCUID,
                                    postData.VFXAddress,
                                    postData.Timestamp,
                                    postData.Signature,
                                    postData.Amount,
                                    postData.UniqueId
                                };

                                var timeCheck = TimeUtil.GetTime(-45);

                                if(timeCheck > result.Timestamp)
                                {
                                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                                    context.Response.ContentType = "application/json";
                                    var response = JsonConvert.SerializeObject(new { Success = false, Message = $"Timestamp too old." }, Formatting.Indented);
                                    await context.Response.WriteAsync(response);
                                    return;
                                }

                                var sigCheck = Services.SignatureService.VerifySignature(result.VFXAddress, $"{result.VFXAddress}.{result.Timestamp}.{result.UniqueId}", postData.Signature);

                                if(!sigCheck)
                                {
                                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                                    context.Response.ContentType = "application/json";
                                    var response = JsonConvert.SerializeObject(new { Success = false, Message = $"Invalid Signature from message: {result.VFXAddress}.{result.Timestamp}" }, Formatting.Indented);
                                    await context.Response.WriteAsync(response);
                                    return;
                                }

                                var coinsToSpend = result.ScriptCoinList;

                                if (coinsToSpend == null && coinsToSpend?.Count() > 0)
                                {
                                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                                    context.Response.ContentType = "application/json";
                                    var response = JsonConvert.SerializeObject(new { Success = false, Message = $"No Coins to Spend" }, Formatting.Indented);
                                    await context.Response.WriteAsync(response);
                                    return;
                                }

                                TransactionBuilder builder = Globals.BTCNetwork.CreateTransactionBuilder();
                                var privateKey = BitcoinAccount.CreatePrivateKeyForArbiter(Globals.ArbiterSigningAddress.GetKey, result.SCUID);

                                var unsignedTransaction = NBitcoin.Transaction.Parse(result.Transaction, Globals.BTCNetwork);

                                List<ScriptCoin> coinList = new List<ScriptCoin>();

                                foreach(var input in result.ScriptCoinList)
                                {
                                    Script redeemScript = Script.FromHex(input.RedeemScript);
                                    Script scriptPubKey = Script.FromHex(input.ScriptPubKey);
                                    OutPoint outPoint = new OutPoint(uint256.Parse(input.TxHash), input.Vout);
                                    Coin coin = new Coin(outPoint, new TxOut(Money.Coins(input.Money), redeemScript));
                                    ScriptCoin coinToSpend = new ScriptCoin(coin, scriptPubKey);

                                    coinList.Add(coinToSpend);
                                }

                                NBitcoin.Transaction keySigned = builder.AddCoins(coinList).AddKeys(privateKey) .SignTransaction(unsignedTransaction);

                                var scState = SmartContractStateTrei.GetSmartContractState(result.SCUID);

                                if(scState == null)
                                {
                                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                                    context.Response.ContentType = "application/json";
                                    var response = JsonConvert.SerializeObject(new { Success = false, Message = $"Failed to find vBTC token at state level." }, Formatting.Indented);
                                    await context.Response.WriteAsync(response);
                                    return;
                                }

                                var totalOwned = 0.0M;

                                if(scState.OwnerAddress != result.VFXAddress)
                                {
                                    if(scState.SCStateTreiTokenizationTXes != null)
                                    {
                                        var vBTCList = scState.SCStateTreiTokenizationTXes.Where(x => x.ToAddress == result.VFXAddress && x.FromAddress == result.VFXAddress).ToList();
                                        if(vBTCList.Count() > 0)
                                        {
                                            //Also get the state level stuff we are saving now.
                                            totalOwned = vBTCList.Sum(x => x.Amount);
                                            if(totalOwned < postData.Amount)
                                            {
                                                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                                                context.Response.ContentType = "application/json";
                                                var response = JsonConvert.SerializeObject(new { Success = false, Message = $"Insufficient Balance" }, Formatting.Indented);
                                                await context.Response.WriteAsync(response);
                                                return;
                                            }
                                        }
                                        else
                                        {
                                            context.Response.StatusCode = StatusCodes.Status400BadRequest;
                                            context.Response.ContentType = "application/json";
                                            var response = JsonConvert.SerializeObject(new { Success = false, Message = $"Account not found as owner of vBTC." }, Formatting.Indented);
                                            await context.Response.WriteAsync(response);
                                            return;
                                        }
                                    }
                                    else
                                    {
                                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                                        context.Response.ContentType = "application/json";
                                        var response = JsonConvert.SerializeObject(new { Success = false, Message = $"Account not found as owner of vBTC." }, Formatting.Indented);
                                        await context.Response.WriteAsync(response);
                                        return;
                                    }
                                }
                                else
                                {
                                    //TODO: Do owner balance check here!
                                }

                                var scMain = SmartContractMain.GenerateSmartContractInMemory(scState.ContractData);

                                if(scMain == null )
                                {
                                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                                    context.Response.ContentType = "application/json";
                                    var response = JsonConvert.SerializeObject(new { Success = false, Message = $"Failed to make SC Main at state level." }, Formatting.Indented);
                                    await context.Response.WriteAsync(response);
                                    return;
                                }

                                if (scMain.Features == null)
                                {
                                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                                    context.Response.ContentType = "application/json";
                                    var response = JsonConvert.SerializeObject(new { Success = false, Message = $"NO SC Features Found." }, Formatting.Indented);
                                    await context.Response.WriteAsync(response);
                                    return;
                                }

                                var tknzFeature = scMain.Features.Where(x => x.FeatureName == FeatureName.Tokenization).Select(x => x.FeatureFeatures).FirstOrDefault();

                                if (tknzFeature == null)
                                {
                                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                                    context.Response.ContentType = "application/json";
                                    var response = JsonConvert.SerializeObject(new { Success = false, Message = $"No Token Feature Found." }, Formatting.Indented);
                                    await context.Response.WriteAsync(response);
                                    return;
                                }

                                var tknz = (TokenizationFeature)tknzFeature;

                                if(tknz == null)
                                {
                                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                                    context.Response.ContentType = "application/json";
                                    var response = JsonConvert.SerializeObject(new { Success = false, Message = $"Failed to cast token feature." }, Formatting.Indented);
                                    await context.Response.WriteAsync(response);
                                    return;
                                }

                                var depositAddress = tknz.DepositAddress;

                                bool changeAddressCorrect = false;
                                foreach (var output in keySigned.Outputs)
                                {
                                    var addr = output.ScriptPubKey.GetDestinationAddress(Globals.BTCNetwork);
                                    if(addr != null)
                                    {
                                        if (addr.ToString() == depositAddress)
                                            changeAddressCorrect = true;
                                    }
                                }

                                if(!changeAddressCorrect)
                                {
                                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                                    context.Response.ContentType = "application/json";
                                    var response = JsonConvert.SerializeObject(new { Success = false, Message = $"Change address must match deposit address." }, Formatting.Indented);
                                    await context.Response.WriteAsync(response);
                                    return;
                                }

                                var keySignedHex = keySigned.ToHex();

                                var nTokenizedWithdrawal = new TokenizedWithdrawals
                                {
                                    Amount = result.Amount,
                                    IsCompleted = false,
                                    OriginalUniqueId = result.UniqueId,
                                    OriginalRequestTime = result.Timestamp,
                                    OriginalSignature = result.Signature,
                                    RequestorAddress = result.VFXAddress,
                                    SmartContractUID = result.SCUID,
                                    TransactionHash = "0",
                                    WithdrawalRequestType = WithdrawalRequestType.Arbiter,
                                    Timestamp = TimeUtil.GetTime(),
                                    ArbiterUniqueId = RandomStringUtility.GetRandomStringOnlyLetters(16)
                                };

                                var responseData = new ResponseData.MultiSigSigningResponse {
                                    Success = true,
                                    Message = "Transaction Signed",
                                    SignedTransaction = keySignedHex,
                                    TokenizedWithdrawals = nTokenizedWithdrawal,
                                };

                                var account = AccountData.GetSingleAccount(Globals.ValidatorAddress);

                                if(account == null )
                                {
                                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                                    context.Response.ContentType = "application/json";
                                    var response = JsonConvert.SerializeObject(new { Success = false, Message = $"Error with Validator/Arb Account." }, Formatting.Indented);
                                    await context.Response.WriteAsync(response);
                                    return;
                                }

                                var wtx = await TokenizationService.CreateTokenizedWithdrawal(nTokenizedWithdrawal, Globals.ValidatorAddress, result.VFXAddress, account, result.SCUID, true);

                                if (wtx.Item1 == null)
                                {
                                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                                    context.Response.ContentType = "application/json";
                                    var response = JsonConvert.SerializeObject(new { Success = false, Message = $"Failed to create withdrawal transaction. Reason: {wtx.Item2}" }, Formatting.Indented);
                                    await context.Response.WriteAsync(response);
                                    return;
                                }

                                var scTx = wtx.Item1;

                                var txresult = await TransactionValidatorService.VerifyTX(scTx);

                                if (txresult.Item1 == true)
                                {
                                    scTx.TransactionStatus = TransactionStatus.Pending;

                                    if (account != null)
                                    {
                                        await WalletService.SendTransaction(scTx, account);
                                    }

                                    context.Response.StatusCode = StatusCodes.Status200OK;
                                    context.Response.ContentType = "application/json";
                                    var requestorResponseJson = JsonConvert.SerializeObject(responseData, Formatting.Indented);
                                    await context.Response.WriteAsync(requestorResponseJson);
                                    return;
                                }
                                else
                                {
                                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                                    context.Response.ContentType = "application/json";
                                    var response = JsonConvert.SerializeObject(new { Success = false, Message = $"Failed to create withdrawal transaction." }, Formatting.Indented);
                                    await context.Response.WriteAsync(response);
                                    return;
                                }

                            }
                            else
                            {
                                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                                context.Response.ContentType = "application/json";
                                var response = JsonConvert.SerializeObject(new { Success = false, Message = $"Failed to deserialize json" }, Formatting.Indented);
                                await context.Response.WriteAsync(response);
                                return;
                            }
                        }
                    }
                    catch(Exception ex)
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        context.Response.ContentType = "application/json";
                        var response = JsonConvert.SerializeObject(new { Success = false, Message = $"Error: {ex}" }, Formatting.Indented);
                        await context.Response.WriteAsync(response);
                        return;
                    }
                    


                    

                });

            });
        }
    }
}
