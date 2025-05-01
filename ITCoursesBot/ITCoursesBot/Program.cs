using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ITCoursesBot.ITCoursesBot.Configuration;
using ITCoursesBot.ITCoursesBot.Services.Telegram;
using Telegram.Bot;

namespace ITCoursesBot
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            BotSettings settings = ConfigurationLoader.LoadSettings();

            var services = new ServiceCollection().AddBotServices(settings);

            var provider = services.BuildServiceProvider();

            // 3) Запускаем Telegram-бота
                using var cts = new CancellationTokenSource();
                var botService = provider.GetRequiredService<ITelegramBotService>();
                await botService.StartAsync(cts.Token);

                Console.WriteLine("Нажмите любую клавишу для остановки...");
                Console.ReadKey();

                cts.Cancel();
                Console.WriteLine("Бот остановлен.");
            }
    }
}
