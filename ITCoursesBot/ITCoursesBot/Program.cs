using ITCoursesBot.ITCoursesBot.Configuration;
using ITCoursesBot.ITCoursesBot.Services.OpenAI;
using ITCoursesBot.ITCoursesBot.Services.Telegram;
using ITCoursesBot.Services;

namespace ITCoursesBot
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            // Загружаем настройки из appsettings.json
            BotSettings settings = ConfigurationLoader.LoadSettings();

            using CancellationTokenSource cts = new CancellationTokenSource();

            using var httpClient = new HttpClient();
            // Передаём настройки OpenAI в клиент.
            IOpenAIClient openAIClient = new OpenAIClient(httpClient, settings.OpenAI);

            // Передаём настройки Telegram в сервис бота.
            ITelegramBotService telegramBotService = new TelegramBotService(settings.Telegram.ApiKey, openAIClient);
            await telegramBotService.StartAsync(cts.Token);

            Console.WriteLine("Нажмите любую клавишу для остановки...");
            Console.ReadKey();

            cts.Cancel();
            Console.WriteLine("Бот остановлен.");
        }
    }
}
