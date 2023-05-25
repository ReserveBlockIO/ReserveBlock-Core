using Microsoft.AspNetCore.Http.Features;
using ReserveBlockCore.Models;
using ReserveBlockCore.Utilities;
using System.Diagnostics;
using System.Net;

namespace ReserveBlockCore.Beacon
{
    public class BeaconStartup
    {
        public BeaconStartup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }
        public static string SaveArea =  GetPathUtility.GetBeaconPath();
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.Configure<FormOptions>(x =>
            {
                x.ValueLengthLimit = 152 * 1024 * 1024; // 150 MB
                x.MultipartBodyLengthLimit = 152 * 1024 * 1024; // 150 MB
            });
        }
        public void Configure(IApplicationBuilder app)
        {
            // Configure the request pipeline
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/data", async context =>
                {
                    // Handle the GET request
                    var ipAddress = context.Connection.RemoteIpAddress?.MapToIPv4().ToString();
                    await context.Response.WriteAsync($"Hello {ipAddress}, this is the server's response!");
                });

                endpoints.MapPost("/upload/{scUID}", async context =>
                {
                    // Increase the maximum request body size
                    try
                    {
                        context.Features.Get<IHttpMaxRequestBodySizeFeature>().MaxRequestBodySize = 152 * 1024 * 1024; // 150 MB
                        var scUID = context.Request.RouteValues["scUID"] as string;
                        var ipAddress = context.Connection.RemoteIpAddress?.MapToIPv4().ToString();
                        // Check if the request contains a file
                        if (context.Request.Form.Files.Count > 0)
                        {
                            var file = context.Request.Form.Files[0];

                            // Save the uploaded file
                            var fileName = file.FileName;
                            var scuidFolder = scUID.Replace(":", "");
                            var filePath = $@"{SaveArea}{scuidFolder}{Path.DirectorySeparatorChar}{fileName}";

                            var extChkResult = CheckExtension(fileName);
                            if (!extChkResult)
                            {
                                //Extension found in reject list
                                context.Response.StatusCode = StatusCodes.Status403Forbidden; // Bad Request
                                await context.Response.WriteAsync("No file was uploaded. Extension was found in auto reject list.");
                                return;
                            }

                            bool fileExist = File.Exists(filePath);
                            if (fileExist)
                            {
                                context.Response.StatusCode = StatusCodes.Status202Accepted;
                                await context.Response.WriteAsync("No file was uploaded. The file already exist");
                                return;
                            }

                            if (!Directory.Exists($@"{SaveArea}{scuidFolder}{Path.DirectorySeparatorChar}"))
                                Directory.CreateDirectory($@"{SaveArea}{scuidFolder}{Path.DirectorySeparatorChar}");


                            var beaconData = BeaconData.GetBeaconData();
                            if (beaconData != null)
                            {
                                var authCheck = beaconData.Exists(x => x.IPAdress == ipAddress && x.AssetName == fileName);
                                if (!authCheck)
                                {
                                    context.Response.StatusCode = StatusCodes.Status403Forbidden; // Bad Request
                                    await context.Response.WriteAsync("No file was uploaded. Extension was found in auto reject list.");
                                    return;
                                }
                                else
                                {
                                    var _beaconData = beaconData.Where(x => x.IPAdress == ipAddress && x.AssetName == fileName).FirstOrDefault();
                                    if (_beaconData != null)
                                    {
                                        using (var stream = new FileStream(filePath, FileMode.Create))
                                        {
                                            await file.CopyToAsync(stream);
                                        }

                                        await context.Response.WriteAsync($"File uploaded successfully!");

                                        _beaconData.AssetReceiveDate = TimeUtil.GetTime();//received today
                                        _beaconData.AssetExpireDate = TimeUtil.GetTimeForBeaconRelease(); //expires in 5 days
                                        var beaconDatas = BeaconData.GetBeacon();
                                        if (beaconDatas != null)
                                        {
                                            beaconDatas.UpdateSafe(_beaconData);
                                        }

                                        return;
                                    }

                                }
                            }
                            else
                            {
                                context.Response.StatusCode = StatusCodes.Status204NoContent; // Bad Request
                                await context.Response.WriteAsync("No file was uploaded. Extension was found in auto reject list.");
                                return;
                            }


                        }
                        else
                        {
                            context.Response.StatusCode = 400; // Bad Request
                            await context.Response.WriteAsync("No file was uploaded.");
                        }
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine($"Error: {ex.ToString()}");
                    }
                    
                });

                endpoints.MapGet("/download/{scUID}/{fileName}", async context =>
                {
                    var scUID = context.Request.RouteValues["scUID"] as string;
                    var fileName = context.Request.RouteValues["fileName"] as string;
                    var ipAddress = context.Connection.RemoteIpAddress?.MapToIPv4().ToString();

                    if (string.IsNullOrEmpty(fileName))
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsync("Invalid file name.");
                        return;
                    }

                    var scuidFolder = scUID.Replace(":", "");

                    var filePath = $@"{SaveArea}{scuidFolder}{Path.DirectorySeparatorChar}{fileName}";
                    bool fileExist = File.Exists(filePath);
                    if (!fileExist)
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsync("Invalid file name.");
                        return;
                    }

                    var beaconDataDb = BeaconData.GetBeacon();
                    if (beaconDataDb != null)
                    {
                        var bdd = beaconDataDb.FindOne(x => x.AssetName.ToLower() == fileName.ToLower() && x.DownloadIPAddress == ipAddress && x.SmartContractUID == scUID);
                        if (bdd != null)
                        {
                            context.Response.ContentType = "application/octet-stream";
                            context.Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName}\"");

                            await using var stream = new FileStream(filePath, FileMode.Open);
                            await stream.CopyToAsync(context.Response.Body);
                        }
                        else
                        {
                            context.Response.StatusCode = StatusCodes.Status400BadRequest;
                            await context.Response.WriteAsync("Invalid file name.");
                            return;
                        }
                    }
                    else
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsync("Invalid file name.");
                        return;
                    }
                });
            });
        }

        private static bool CheckExtension(string fileName)
        {
            bool output = false;

            string ext = Path.GetExtension(fileName);

            if (!string.IsNullOrEmpty(ext))
            {
                var rejectedExtList = Globals.RejectAssetExtensionTypes;
                var exist = rejectedExtList.Contains(ext);
                if (!exist)
                    output = true;
            }
            return output;
        }
    }
}
