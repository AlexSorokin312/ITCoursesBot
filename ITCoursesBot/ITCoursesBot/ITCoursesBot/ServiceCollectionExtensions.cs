using Bot.Adapters.ApiClients;
using Bot.Ports;
using ITCoursesBot.Interfaces;
using ITCoursesBot.ITCoursesBot.Configuration;
using ITCoursesBot.ITCoursesBot.Models;
using ITCoursesBot.ITCoursesBot.Services.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using Refit;
using System.Net;
using Telegram.Bot;

namespace ITCoursesBot.ITCoursesBot
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddBotConfiguration(this IServiceCollection services, IConfiguration config)
        {
            // 1) Telegram
            var tg = config.GetSection("TelegramSettings").Get<TelegramSettings>();
            services.AddSingleton(tg);
            services.AddSingleton<ITelegramBotClient>(_ => new TelegramBotClient(tg.ApiKey));

            // 2) OpenAI
            var ai = config.GetSection("OpenAISettings").Get<OpenAISettings>();
            services.AddSingleton(ai);

            return services;
        }

        public static IServiceCollection AddBotState(this IServiceCollection services)
        {
            services.AddSingleton<IUserStateStore, InMemoryStateStore>();
            services.AddSingleton<ISessionManager, SessionManager>();
            return services;
        }

        public static IServiceCollection AddBotServices(this IServiceCollection services)
        {
            services.AddHttpClient();
            services.AddScoped<IMessageService, TelegramMessageService>();
            services.AddScoped<IOpenAIClient, OpenAIClient>();
            services.AddScoped<IKeyboardBuilder, KeyboardBuilder>();

            return services;
        }

        public static IServiceCollection AddApiClients(this IServiceCollection services,
                                                IConfiguration cfg)
        {
            // 1) Читаем URL из конфига
            var baseUrl = cfg["Api:BaseUrl"]
                          ?? throw new InvalidOperationException("Api:BaseUrl не задан");

            // 2) Refit‑клиент для Users
            services.AddRefitClient<IUsersApi>()
                    .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl))
                    .ConfigurePrimaryHttpMessageHandler(_ =>
                        new HttpClientHandler
                        {
                            ServerCertificateCustomValidationCallback =
                                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                        });
            services.AddTransient<IUserClient, UsersApiClient>();

            // 3) HttpClient для Questions
            services.AddHttpClient<IQuestionsClient, QuestionsClient>(c =>
            {
                c.BaseAddress = new Uri(baseUrl);
                c.Timeout = TimeSpan.FromSeconds(60);
            }).AddPolicyHandler(GetRetry());

            // 4) HttpClient для Progress
            services.AddHttpClient<IProgressClient, ProgressClient>(c =>
            {
                c.BaseAddress = new Uri(baseUrl);
                c.Timeout = TimeSpan.FromSeconds(50);
            }).AddPolicyHandler(GetRetry());

            // 5) HttpClient для Lesson
            services.AddHttpClient<ILessonClient, LessonClient>(c =>
            {
                c.BaseAddress = new Uri(baseUrl);
                c.Timeout = TimeSpan.FromSeconds(50);
            }).AddPolicyHandler(GetRetry());

            // 6) HttpClient для AI‑лимита
            services.AddHttpClient<IAiLimitClient, AiLimitClient>(c =>
            {
                c.BaseAddress = new Uri(baseUrl);
                c.Timeout = TimeSpan.FromSeconds(60);
            }).AddPolicyHandler(GetRetry());

            return services;
        }

        private static IAsyncPolicy<HttpResponseMessage> GetRetry() =>
            HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
                .WaitAndRetryAsync(3,
                    attempt => TimeSpan.FromMilliseconds(200 * attempt));

        public static IServiceCollection AddBotWorker(this IServiceCollection services)
        {
            services.AddHostedService<BotWorker>();
            return services;
        }
    }
}
