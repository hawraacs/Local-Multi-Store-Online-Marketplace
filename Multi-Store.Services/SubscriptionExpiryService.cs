using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Multi_Store.Services
{
        public class SubscriptionExpiryService : BackgroundService
        {
            private readonly IServiceProvider _services;

            public SubscriptionExpiryService(IServiceProvider services)
            {
                _services = services;
            }

            protected override async Task ExecuteAsync(CancellationToken stoppingToken)
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    using (var scope = _services.CreateScope())
                    {
                    var subscriptionService =
  scope.ServiceProvider.GetRequiredService<Multi_Store.Services.SubscriptionService>();
                    subscriptionService.UpdateExpiredStores();
                    }
                    // Run once every 24 hours
                    await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
                }
            }
        }
    }