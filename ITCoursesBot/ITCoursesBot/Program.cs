using ITCoursesBot;
using ITCoursesBot.ITCoursesBot;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ITCoursesBot
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(cfg =>
                    cfg.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true))
                .ConfigureServices((ctx, services) => services
                    .AddBotConfiguration(ctx.Configuration)
                    .AddBotState()
                    .AddBotServices()
                    .AddBotControllers()
                    .AddBotWorker()
      )
      .Build();

            await host.RunAsync();
        }
    }
}
