using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using ITCoursesBot.ITCoursesBot.Configuration;
using ITCoursesBot.ITCoursesBot.Services.OpenAI;
using ITCoursesBot.ITCoursesBot.Services.Telegram;
using ITCoursesBot.Services;

namespace ITCoursesBot
{
    public static class CoreInstaller
    {
        /// <summary>
        /// Регистрирует все сервисы бота: OpenAI, Telegram и т.п.
        /// </summary>
        public static IServiceCollection AddBotServices(this IServiceCollection services, BotSettings settings)
        {

            services.AddSingleton<IOpenAIClient>(sp =>
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var client = factory.CreateClient();
                return new OpenAIClient(client, settings.OpenAI);
            });

            services
                .AddSingleton<ITelegramBotClient>(sp => new TelegramBotClient(settings.Telegram.ApiKey))
                .AddSingleton<ITextToSpeechService, OpenAITtsService>()
                .AddSingleton<IKeyboardBuilder, SimpleKeyboardBuilder>()
                .AddTransient<IUpdateHandler, TextMessageHandler>()
                .AddTransient<IUpdateHandler, AudioMessageHandler>()
                .AddSingleton<ITelegramBotService, TelegramBotService>()
                .AddSingleton(settings);
             services.AddHttpClient(); ;

            return services;
        }
    }
}
