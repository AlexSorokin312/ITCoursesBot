using Microsoft.Extensions.Configuration;

namespace ITCoursesBot.ITCoursesBot.Configuration
{
    public static class ConfigurationLoader
    {
        public static BotSettings LoadSettings()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory()) // устанавливаем базовую директорию
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true) // подключаем JSON файл
                .Build();

            BotSettings settings = new BotSettings();
            configuration.Bind(settings);
            return settings;
        }
    }
}
