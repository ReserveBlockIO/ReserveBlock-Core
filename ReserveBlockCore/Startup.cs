using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using ReserveBlockCore.Models;
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

            services.AddSwaggerGen(c => {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "ReserveBlock CLI API", Version = "v1" });
                c.DocumentFilter<SwaggerDocumentFilter<Account>>();
                c.DocumentFilter<SwaggerDocumentFilter<AccountKeystore>>();
                c.DocumentFilter<SwaggerDocumentFilter<AccountStateTrei>>();
                c.DocumentFilter<SwaggerDocumentFilter<AdjNodeInfo>>();
                c.DocumentFilter<SwaggerDocumentFilter<AdjPool>>();
                c.DocumentFilter<SwaggerDocumentFilter<AdjudicatorPool>>();
                c.DocumentFilter<SwaggerDocumentFilter<Adjudicators>>();
                c.DocumentFilter<SwaggerDocumentFilter<Adnr>>();
                c.DocumentFilter<SwaggerDocumentFilter<Block>>();
                c.DocumentFilter<SwaggerDocumentFilter<DecShop>>();
                c.DocumentFilter<SwaggerDocumentFilter<FortisPool>>();
                c.DocumentFilter<SwaggerDocumentFilter<HDWallet>>();
                c.DocumentFilter<SwaggerDocumentFilter<Keystore>>();
                c.DocumentFilter<SwaggerDocumentFilter<NodeInfo>>();
                c.DocumentFilter<SwaggerDocumentFilter<Peers>>();
                c.DocumentFilter<SwaggerDocumentFilter<SmartContractStateTrei>>();
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

            if(Globals.TestURL)
                app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
