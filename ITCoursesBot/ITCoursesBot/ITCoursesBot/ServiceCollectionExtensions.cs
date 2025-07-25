using Bot.Adapters.ApiClients;
using Bot.Ports;
using ITCoursesBot.Interfaces;
using ITCoursesBot.ITCoursesBot.Configuration;
using ITCoursesBot.ITCoursesBot.Controllers;
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

        public static IServiceCollection AddBotControllers(this IServiceCollection services)
        {
            services.AddScoped<StartController>();
            services.AddScoped<BeginQuizController>();
            services.AddScoped<PassQuizController>();
            services.AddScoped<ButtonHandlerController>();
            services.AddScoped<CodeExplainController>();
            services.AddScoped<MockInterviewController>();
            services.AddScoped<DialogController>();
            services.AddScoped<DefaultController>();

            services.AddScoped<UpdateRouter>(sp =>
            {
                var ctrls = new BaseController[]
                {
                        sp.GetRequiredService<StartController>(),
                        sp.GetRequiredService<BeginQuizController>(),
                        sp.GetRequiredService<PassQuizController>(),
                        sp.GetRequiredService<ButtonHandlerController>(),
                        sp.GetRequiredService<CodeExplainController>(),
                        //sp.GetRequiredService<MockInterviewController>(),
                        //sp.GetRequiredService<DialogController>(),
                        //sp.GetRequiredService<DefaultController>()
                };
                return new UpdateRouter(ctrls);
            });
            return services;
        }

        public static IServiceCollection AddApiClients(this IServiceCollection services,
                                                       IConfiguration cfg)
        {
            // 1) получаем URL один раз
            var baseUrl = cfg["Api:BaseUrl"]
                          ?? "https://localhost:7182";    // можно по‑умолчанию

            // 2) Refit-клиент для Users
            services.AddRefitClient<IUsersApi>()
                    .ConfigureHttpClient(c =>
                    {
                        c.BaseAddress = new Uri("https://localhost:7182");
                        // если нужно — handler для dev‑TLS
                        c.DefaultRequestVersion = new Version(2, 0);
                    })
                    .ConfigurePrimaryHttpMessageHandler(_ =>
                        new HttpClientHandler
                        {
                            ServerCertificateCustomValidationCallback =
                                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                        });

            services.AddTransient<IUserClient, UsersApiClient>();

            // 3) Typed HttpClient для вопросов — **всегда**, вне DEBUG
            services.AddHttpClient<IQuestionsClient, QuestionsClient>(client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(60);
            })
            .AddPolicyHandler(GetRetry());   // если вы используете Polly

            services.AddHttpClient<IProgressClient, ProgressClient>(client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(50);
            })
            .AddPolicyHandler(GetRetry());  // если используете Polly
            services.AddHttpClient<ILessonClient, LessonClient>(client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(50);
            })
            .AddPolicyHandler(GetRetry());

            services.AddHttpClient<IAiLimitClient, AiLimitClient>(client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(60);
            })
            .AddPolicyHandler(GetRetry());

            return services;
        }

        private static IAsyncPolicy<HttpResponseMessage> GetRetry() =>
            HttpPolicyExtensions
                .HandleTransientHttpError()                    // 5xx + network
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
