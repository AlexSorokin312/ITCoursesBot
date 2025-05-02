using ITCoursesBot.Interfaces;
using ITCoursesBot.ITCoursesBot.Configuration;
using ITCoursesBot.ITCoursesBot.Services.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;

namespace ITCoursesBot
{
    public static class CoreInstaller
    {
        /// <summary>
        /// Регистрирует все сервисы бота: OpenAI, Telegram и т.п.
        /// </summary>
        public static IServiceCollection AddBotServices(this IServiceCollection services, BotSettings settings)
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration(cfg =>
                {
                    cfg.AddJsonFile("appsettings.json", optional: false);
                });

            services.AddSingleton(settings);
            services.AddHttpClient();

            services.AddSingleton(settings.OpenAI);

            services.AddSingleton<IOpenAIClient>(sp =>
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var client = factory.CreateClient();
                return new OpenAIClient(client, settings.OpenAI);
            });

            services.AddSingleton<ITelegramBotClient>(sp =>
                new TelegramBotClient(settings.Telegram.ApiKey));
            services.AddSingleton<ITextToSpeechService, TtsService>();
            services.AddSingleton<IKeyboardBuilder, KeyboardBuilder>();

            services.AddTransient<IUpdateHandler, TextMessageHandler>();

            services.AddTransient<IUpdateHandler, AudioMessageHandler>();

            services.AddSingleton<ITelegramBotService, TelegramBotService>();
            services.AddSingleton<IMessageService, TelegramMessageService>();

            services.AddSingleton<IQuizRepository, QuizRepository>();
            return services;
        }

    }
}
