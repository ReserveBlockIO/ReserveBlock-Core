using Microsoft.AspNetCore.SignalR;
using ReserveBlockCore.P2P;

namespace ReserveBlockCore.Services
{
    public class MonitorLoop
    {
        #region snippet_Monitor
        
            private readonly CancellationToken _cancellationToken;
            private readonly IHubContext<P2PAdjServer> _hubContext;

        public MonitorLoop(
                IHostApplicationLifetime applicationLifetime,
                IHubContext<P2PAdjServer> hubContext)
            {
                
                _cancellationToken = applicationLifetime.ApplicationStopping;
                _hubContext = hubContext;
        }

            public void StartMonitorLoop()
            {


                // Run a console user input loop in a background thread
                Task.Run(async () => await MonitorAsync());
            }

            private async ValueTask MonitorAsync()
            {
                while (!_cancellationToken.IsCancellationRequested)
                {
                    var keyStroke = Console.ReadKey();

                    if (keyStroke.Key == ConsoleKey.W)
                    {
                        // Enqueue a background work item
                        
                    }
                }
            }

            private async ValueTask BuildWorkItem(CancellationToken token)
            {
                
            }
        
        #endregion
    }
}
