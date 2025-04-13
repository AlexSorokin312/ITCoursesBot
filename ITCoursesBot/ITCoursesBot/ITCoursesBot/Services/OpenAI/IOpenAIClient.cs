namespace ITCoursesBot.ITCoursesBot.Services.OpenAI
{
    public interface IOpenAIClient
    {
        Task<string> GetChatResponseAsync(string userMessage);
    }
}