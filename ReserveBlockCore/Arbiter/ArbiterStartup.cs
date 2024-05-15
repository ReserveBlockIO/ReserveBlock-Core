using ImageMagick;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using ReserveBlockCore.Bitcoin.Models;
using ReserveBlockCore.Models;
using ReserveBlockCore.SecretSharing.Cryptography;
using ReserveBlockCore.SecretSharing.Math;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System.Numerics;

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

                endpoints.MapGet("/depositaddress/{address}", async context =>
                 {
                    var address = context.Request.RouteValues["address"] as string;

                    if (string.IsNullOrEmpty(address))
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        var response = JsonConvert.SerializeObject(new { Success = false, Message = $"No Address" }, Formatting.Indented);
                        await context.Response.WriteAsync(response);
                        return;
                    }

                    //TODO MAKE THIS WORK!
                    var publicKey = BitcoinAccount.CreatePublicKeyForArbiter(Globals.ArbiterSigningAddress.GetKey, 0);

                    var signature = SignatureService.CreateSignature(publicKey, Globals.ArbiterSigningAddress.GetPrivKey, Globals.ArbiterSigningAddress.PublicKey);

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
            });
        }
    }
}
