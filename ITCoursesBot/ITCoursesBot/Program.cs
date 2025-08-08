using ITCoursesBot.Interfaces;
using ITCoursesBot.ITCoursesBot;
using ITCoursesBot.ITCoursesBot.Configuration;
using ITCoursesBot.ITCoursesBot.Services.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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

                // 2. Настройки OpenAI
                services.Configure<OpenAISettings>(
                    ctx.Configuration.GetSection("OpenAISettings"));
                services.AddSingleton(resolver =>
                    resolver.GetRequiredService<IOptions<OpenAISettings>>().Value);
                services.AddHttpClient<IOpenAIClient, OpenAIClient>();

                // 2. Краткосрочное состояние бота (сессии)
                services.AddBotState();

                // 3. Вспомогательные сервисы (KeyboardBuilder, MessageService и т.д.)
                services.AddBotServices();

                // 4. HTTP‑клиент к вашему Web‑API (то, что мы добавили = Users)
                services.AddApiClients(ctx.Configuration);

                // 6. Фоновый worker, который крутит long‑polling
                services.AddBotWorker();

                services.AddTransient<IUpdateMiddleware, StartMiddleware>();
                services.AddTransient<IUpdateMiddleware, ReworkMiddleware>();
                services.AddTransient<IUpdateMiddleware, ProgressSummaryMiddleware>();
                services.AddTransient<IUpdateMiddleware, CodeExplainMiddleware>();
                services.AddTransient<IUpdateMiddleware, BeginQuizMiddleware>();
                services.AddTransient<IUpdateMiddleware, PassQuizMiddleware>();
                services.AddTransient<IUpdateMiddleware, DialogMiddleware>();
                services.AddTransient<IUpdateMiddleware, ReworkMiddleware>();
                services.AddTransient<IUpdateMiddleware, MockInterviewMiddleware>();
                services.AddTransient<IUpdateMiddleware, PromoCodesMiddleware>();

                services.AddSingleton<UpdateMiddlewarePipeline>();
                services.AddHostedService<BotWorker>();
            })
            .Build();

        await host.RunAsync();
    }
}
