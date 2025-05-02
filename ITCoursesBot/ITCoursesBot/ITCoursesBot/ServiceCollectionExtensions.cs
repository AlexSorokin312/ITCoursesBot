using ITCoursesBot.Interfaces;
using ITCoursesBot.ITCoursesBot.Configuration;
using ITCoursesBot.ITCoursesBot.Controllers;
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
            // TelegramSettings
            var tg = config.GetSection("TelegramSettings").Get<TelegramSettings>();
            services.AddSingleton(tg);
            services.AddSingleton<ITelegramBotClient>(_ => new TelegramBotClient(tg.ApiKey));

            // OpenAISettings
            var ai = config.GetSection("OpenAISettings").Get<OpenAISettings>();
            services.AddSingleton(ai);

            return services;
        }

        public static IServiceCollection AddBotState(this IServiceCollection services)
        {
            services.AddSingleton<IUserStateStore, InMemoryStateStore>();
            return services;
        }

        public static IServiceCollection AddBotServices(this IServiceCollection services)
        {
            services.AddHttpClient();
            services.AddScoped<IMessageService, TelegramMessageService>();
            services.AddScoped<IOpenAIClient, OpenAIClient>();
            services.AddScoped<IQuestionRepository, QuestionRepository>();
            return services;
        }

        public static IServiceCollection AddBotControllers(this IServiceCollection services)
        {
            services.AddScoped<BaseController, QuizController>();
            services.AddScoped<BaseController, CodeExplainController>();
            services.AddScoped<BaseController, MockInterviewController>();
            services.AddScoped<BaseController, DialogController>();
            services.AddScoped<BaseController, DefaultController>();

            services.AddSingleton<UpdateRouter>();
            return services;
        }

        public static IServiceCollection AddBotWorker(this IServiceCollection services)
        {
            services.AddHostedService<BotWorker>();
            return services;
        }
    }
}
