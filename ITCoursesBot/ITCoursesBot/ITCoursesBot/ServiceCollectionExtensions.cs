using ITCoursesBot.Interfaces;
using ITCoursesBot.ITCoursesBot.Configuration;
using ITCoursesBot.ITCoursesBot.Controllers;
using ITCoursesBot.ITCoursesBot.Models;
using ITCoursesBot.ITCoursesBot.Services;
using ITCoursesBot.ITCoursesBot.Services.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
            services.AddScoped<IQuizRepository, QuizRepository>();
            services.AddScoped<IKeyboardBuilder, KeyboardBuilder>();

            services.AddSingleton<DBRepository>();

            return services;
        }

        public static IServiceCollection AddBotControllers(this IServiceCollection services)
        {
            // ручная регистрация контроллеров в нужном порядке
            services.AddScoped<StartController>();
            services.AddScoped<BeginQuizController>();
            services.AddScoped<PassQuizController>();
            services.AddScoped<ButtonHanlderController>();
            services.AddScoped<CodeExplainController>();
            services.AddScoped<MockInterviewController>();
            services.AddScoped<DialogController>();
            services.AddScoped<DefaultController>();

            // UpdateRouter получит их через IEnumerable<BaseController>
            services.AddSingleton<UpdateRouter>(sp =>
            {
                var ctrls = new BaseController[]
                {
                    sp.GetRequiredService<StartController>(),
                    sp.GetRequiredService<BeginQuizController>(),
                    sp.GetRequiredService<PassQuizController>(),
                    sp.GetRequiredService<ButtonHanlderController>(),
                    sp.GetRequiredService<CodeExplainController>(),
                    sp.GetRequiredService<MockInterviewController>(),
                    sp.GetRequiredService<DialogController>(),
                    sp.GetRequiredService<DefaultController>()
                };
                return new UpdateRouter(ctrls);
            });
            return services;
        }

        public static IServiceCollection AddBotWorker(this IServiceCollection services)
        {
            services.AddHostedService<BotWorker>();
            return services;
        }
    }
}
