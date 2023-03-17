using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using ReserveBlockCore.Models;
using ReserveBlockCore.Models.DST;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ReserveBlockCore
{
    public class Startup
    {
        public static bool APIEnabled = false;
        public static bool IsTestNet = false;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            //services.AddApiVersioning(options =>
            //{
            //    options.ReportApiVersions = true;
            //    options.DefaultApiVersion = new Microsoft.AspNetCore.Mvc.ApiVersion(1, 0);
            //});

            services.AddSwaggerGen(c => {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "ReserveBlock CLI API", Version = "v1" });
                c.DocumentFilter<SwaggerDocumentFilter<Account>>();
                c.DocumentFilter<SwaggerDocumentFilter<AccountKeystore>>();
                c.DocumentFilter<SwaggerDocumentFilter<AccountStateTrei>>();
                c.DocumentFilter<SwaggerDocumentFilter<AdjNodeInfo>>();
                c.DocumentFilter<SwaggerDocumentFilter<AdjPool>>();
                c.DocumentFilter<SwaggerDocumentFilter<AdjudicatorPool>>();
                c.DocumentFilter<SwaggerDocumentFilter<Adjudicators>>();
                c.DocumentFilter<SwaggerDocumentFilter<AdjVoteInReqs>>();
                c.DocumentFilter<SwaggerDocumentFilter<Adnr>>();
                c.DocumentFilter<SwaggerDocumentFilter<Auction>>();
                c.DocumentFilter<SwaggerDocumentFilter<Beacons>>();
                c.DocumentFilter<SwaggerDocumentFilter<Bid>>();
                c.DocumentFilter<SwaggerDocumentFilter<Block>>();
                c.DocumentFilter<SwaggerDocumentFilter<DecShop>>();
                c.DocumentFilter<SwaggerDocumentFilter<FortisPool>>();
                c.DocumentFilter<SwaggerDocumentFilter<HDWallet>>();
                c.DocumentFilter<SwaggerDocumentFilter<Keystore>>();
                c.DocumentFilter<SwaggerDocumentFilter<Listing>>();
                c.DocumentFilter<SwaggerDocumentFilter<Mother>>();
                c.DocumentFilter<SwaggerDocumentFilter<Mother.Kids>>();
                c.DocumentFilter<SwaggerDocumentFilter<Mother.MotherJoinPayload>>();
                c.DocumentFilter<SwaggerDocumentFilter<Mother.MotherStartPayload>>();
                c.DocumentFilter<SwaggerDocumentFilter<NodeInfo>>();
                c.DocumentFilter<SwaggerDocumentFilter<Peers>>();
                c.DocumentFilter<SwaggerDocumentFilter<ReserveAccount>>();
                c.DocumentFilter<SwaggerDocumentFilter<ReserveAccount.ReserveAccountInfo>>();
                c.DocumentFilter<SwaggerDocumentFilter<ReserveAccount.ReserveAccountCreatePayload>>();
                c.DocumentFilter<SwaggerDocumentFilter<ReserveAccount.ReserveAccountRestorePayload>>();
                c.DocumentFilter<SwaggerDocumentFilter<ReserveAccount.SendNFTTransferPayload>>();
                c.DocumentFilter<SwaggerDocumentFilter<ReserveAccount.SendTransactionPayload>>();
                c.DocumentFilter<SwaggerDocumentFilter<SmartContractStateTrei>>();
                c.DocumentFilter<SwaggerDocumentFilter<Collection>>();
                c.DocumentFilter<SwaggerDocumentFilter<TaskAnswerResult>>();
                c.DocumentFilter<SwaggerDocumentFilter<TaskNumberAnswerV2>>();
                c.DocumentFilter<SwaggerDocumentFilter<TaskQuestion>>();
                c.DocumentFilter<SwaggerDocumentFilter<TaskWinner>>();
                c.DocumentFilter<SwaggerDocumentFilter<TopicTrei>>();
                c.DocumentFilter<SwaggerDocumentFilter<Transaction>>();
                c.DocumentFilter<SwaggerDocumentFilter<Validators>>();
                c.DocumentFilter<SwaggerDocumentFilter<Vote>>();
                c.DocumentFilter<SwaggerDocumentFilter<WorldTrei>>();
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
                if(Globals.APIToken?.Length > 0)
                    c.OperationFilter<SwaggerHeaderFilter>();
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseSwagger();
            app.UseSwaggerUI(c => {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "ReserveBlock API v1");
                c.DisplayRequestDuration();
            });


            app.Use((context, func) =>
            {
                if (APIEnabled)
                {
                    if (Globals.AlwaysRequireAPIPassword == true)
                    {
                        return func.Invoke();
                    }
                    if (Globals.APIUnlockTime == null)
                    {
                        return func.Invoke();
                    }
                    else
                    {
                        var now = DateTime.UtcNow;
                        var target = context.Request.Path.HasValue ? context.Request.Path.Value.ToLower() : "NA";
                        if(target.Contains("/api/v1/unlockwallet/"))
                        {
                            return func.Invoke();
                        }
                        if(now < Globals.APIUnlockTime)
                        {
                            return func.Invoke();
                        }
                        else
                        {
                            context.Response.StatusCode = 403;//if u want to return specific status code when not ready to accept requests
                            return Task.CompletedTask;
                        }
                    }
                }
                context.Response.StatusCode = 403;//if u want to return specific status code when not ready to accept requests
                return Task.CompletedTask;
            });

            //if(Globals.TestURL)
                //app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
