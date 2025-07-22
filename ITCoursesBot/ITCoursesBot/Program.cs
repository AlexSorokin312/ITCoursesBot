// Program.cs  (.NET 8, консоль‑worker)
using ITCoursesBot.ITCoursesBot;             // ― здесь лежат AddBot*
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

internal class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            // ---------- Конфиги ----------
            .ConfigureAppConfiguration(cfg =>
            {
                cfg.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                   .AddEnvironmentVariables()       // чтобы переопределять переменные в Docker
                   .AddCommandLine(args);           // из аргументов запуска
            })
            // ---------- DI‑контейнер ----------
            .ConfigureServices((ctx, services) =>
            {
                // 1. Telegram / OpenAI ключи
                services.AddBotConfiguration(ctx.Configuration);

                // 2. Краткосрочное состояние бота (сессии)
                services.AddBotState();

                // 3. Вспомогательные сервисы (KeyboardBuilder, MessageService и т.д.)
                services.AddBotServices();

                // 4. HTTP‑клиент к вашему Web‑API (то, что мы добавили = Users)
                services.AddApiClients(ctx.Configuration);

                // 5. Контроллеры + роутер
                services.AddBotControllers();

                // 6. Фоновый worker, который крутит long‑polling
                services.AddBotWorker();
            })
            .Build();

        await host.RunAsync();
    }
}
