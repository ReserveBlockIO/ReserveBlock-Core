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
                    if (Program.AlwaysRequireAPIPassword == true)
                    {
                        return func.Invoke();
                    }
                    if (Program.APIUnlockTime == null)
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
                        if(now < Program.APIUnlockTime)
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
