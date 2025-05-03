using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ITCoursesBot.ITCoursesBot;

namespace ITCoursesBot
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((ctx, cfg) =>
                {
                    cfg.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    cfg.AddJsonFile("quizData.json", optional: false, reloadOnChange: true);
                })
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
