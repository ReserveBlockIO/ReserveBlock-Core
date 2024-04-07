using ImageMagick;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using ReserveBlockCore.Bitcoin.Models;
using ReserveBlockCore.Bitcoin.Services;
using ReserveBlockCore.SecretSharing.Cryptography;
using ReserveBlockCore.SecretSharing.Math;
using ReserveBlockCore.Utilities;
using System.Numerics;

namespace ReserveBlockCore.Dealer
{
    public class DealerStartup
    {
        public IConfiguration Configuration { get; }

        public DealerStartup(IConfiguration configuration)
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

                endpoints.MapGet("/depositaddress/{address}/{scUID}/{message}/{signature}", async context =>
                {
                    var address = context.Request.RouteValues["address"] as string;
                    var scUID = context.Request.RouteValues["scUID"] as string;
                    var message = context.Request.RouteValues["message"] as long?;
                    var signature = context.Request.RouteValues["signature"] as string;

                    if (string.IsNullOrEmpty(address) || string.IsNullOrEmpty(scUID) || message == null || string.IsNullOrEmpty(signature))
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        var response = JsonConvert.SerializeObject(new { Success = false, Message = $"No Smart Contract UID" }, Formatting.Indented);
                        await context.Response.WriteAsync(response);
                        return;
                    }

                    var signatureVerified = Services.SignatureService.VerifySignature(address, message.Value.ToString(), signature);
                    var now = TimeUtil.GetTime();
                    if(((message.Value + 15) < now) || !signatureVerified)
                    {
                        context.Response.StatusCode = StatusCodes.Status412PreconditionFailed;
                        var response = JsonConvert.SerializeObject(new { Success = false, Message = $"Signature invalid or message too old" }, Formatting.Indented);
                        await context.Response.WriteAsync(response);
                        return;
                    }

                    var account = BitcoinAccount.CreateAddress(false);

                    var gcd = new ExtendedEuclideanAlgorithm<BigInteger>();
                    var split = new ShamirsSecretSharing<BigInteger>(gcd);
                    var shares = split.MakeShares(3, 4, account.PrivateKey);

                    if (shares.OriginalSecret.HasValue)
                    {
                        if(shares.OriginalSecret.Value.ToString() != account.PrivateKey)
                        {
                            context.Response.StatusCode = StatusCodes.Status417ExpectationFailed;
                            var response = JsonConvert.SerializeObject(new { Success = false, Message = $"Shares were not created" }, Formatting.Indented);
                            await context.Response.WriteAsync(response);
                            return;
                        }

                        var share1 = shares[0].ToString(); //save with dealer - (1)
                        var share2 = shares[1].ToString(); //send to requestor - (2)
                        var share3 = shares[2].ToString(); //send to requestor encrypted - (3)
                        var share4 = shares[3].ToString(); //send to validators - (4)

                        //TODO: DONE
                        //Save Shares here
                        var share = new Shares { SCUID = scUID, Share = share1, IsEncrypted = false };
                        Shares.SaveShare(share);

                        //TODO:
                        //Put other share into memory - DONT SAVE
                        var memoryShare = new Shares { SCUID = scUID, Share = share4, IsEncrypted = false };

                        //TODO:DONE
                        //Encrypt the share3 below before sending.
                        DealerResponse.DealerAddressRequest requestorResponse = new DealerResponse.DealerAddressRequest
                        {
                            Address = account.Address,
                            Share = share2,
                            EncryptedShare = share3.ToEncrypt(Globals.DealerEncryptPassword.ToUnsecureString()),
                        };

                        var requestorResponseJson = JsonConvert.SerializeObject(new { Success = true, Message = $"Shares created", Response = requestorResponse }, Formatting.Indented);
                        await context.Response.WriteAsync(requestorResponseJson);
                        return;
                    }
                    else
                    {
                        context.Response.StatusCode = StatusCodes.Status417ExpectationFailed;
                        var response = JsonConvert.SerializeObject(new { Success = false, Message = $"Shares were not created" }, Formatting.Indented);
                        await context.Response.WriteAsync(response);
                        return;
                    }

                });
            });
        }
    }
}
