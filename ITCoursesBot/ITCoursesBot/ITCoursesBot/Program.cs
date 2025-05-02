// Program.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MyTelegramBot.Configuration;
using Telegram.Bot;
using MyTelegramBot.State;
using MyTelegramBot.Services.Telegram;
using MyTelegramBot.Services.OpenAI;
using MyTelegramBot.Services;
using MyTelegramBot.Controllers;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(cfg => {
        cfg.AddJsonFile("appsettings.json", optional: false);
    })
    .ConfigureServices((ctx, services) => {
        // 1) Настраиваем BotClient
        var botSettings = new BotSettings();
        ctx.Configuration.Bind("BotSettings", botSettings);
        services.AddSingleton(botSettings);
        services.AddSingleton<ITelegramBotClient>(_ =>
            new TelegramBotClient(botSettings.BotToken));

        // 2) State-store (InMemory для начала)
        services.AddSingleton<IUserStateStore, InMemoryStateStore>();

        // 3) Ваши сервисы
        services.AddScoped<IMessageService, TelegramMessageService>();
        services.AddScoped<IOpenAIClient, OpenAIClient>();
        services.AddScoped<IQuestionRepository, QuestionRepository>();

        // 4) Контроллеры
        services.Scan(scan => scan
            .FromAssemblyOf<BaseController>()
            .AddClasses(c => c.AssignableTo<BaseController>())
            .AsSelf()
            .WithScopedLifetime());

        // 5) Фоновый воркер
        services.AddHostedService<BotWorker>();
    })
    .Build();

await host.RunAsync();
