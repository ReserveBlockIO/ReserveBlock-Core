using ReserveBlockCore.Nodes;
using ReserveBlockCore.P2P;
using ReserveBlockCore.Services;


namespace ReserveBlockCore
{
    public class StartupP2PValidator
    {
        public static bool IsTestNet = false;
        public StartupP2PValidator(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddSignalR(options => {
                options.KeepAliveInterval = TimeSpan.FromSeconds(15); //check connections everyone 15 seconds
                options.ClientTimeoutInterval = TimeSpan.FromSeconds(60); //close connection after 60 seconds
                options.MaximumReceiveMessageSize = 1179648;
                options.StreamBufferCapacity = 1024;
                options.EnableDetailedErrors = true;
                options.MaximumParallelInvocationsPerClient = int.MaxValue;
            });

            //Create hosted service for just consensus measures
            services.AddHostedService<ValidatorProcessor>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            //app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();

                if (Globals.AdjudicateAccount != null)
                {
                    endpoints.MapHub<ConsensusServer>("/validator", options =>
                    {
                        options.ApplicationMaxBufferSize = 8388608; // values might need tweaking if mem consumption gets too large
                        options.TransportMaxBufferSize = 8388608; // values might need tweaking if mem consumption gets too large
                    });
                }
            });
        }
    }
}
