using ITCoursesBot.DB;
using ITCoursesBot.ITCoursesBot;
using ITCoursesBot.ITCoursesBot.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
                    .AddDatabase(ctx.Configuration)
                    .AddBotServices()
                    .AddBotControllers()
                    .AddBotWorker()
                )
                .Build();

            using (var scope = host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<BotDbContext>();

                db.Database.Migrate();    // применит все миграции к файлу базы
                DataSeeder.Seed(db);
            }

            await host.RunAsync();
        }
    }
}
