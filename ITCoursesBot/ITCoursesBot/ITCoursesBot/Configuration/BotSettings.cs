namespace ITCoursesBot.ITCoursesBot.Configuration
{
    public class BotSettings
    {
        public TelegramSettings Telegram { get; set; }
        public OpenAISettings OpenAI { get; set; }
    }

    public class TelegramSettings
    {
        public string ApiKey { get; set; }
    }

    public class OpenAISettings
    {
        public string AssistantId { get; set; }
        public string ApiKey { get; set; }
        public string ChatInstructions { get; set; }
    }
}
