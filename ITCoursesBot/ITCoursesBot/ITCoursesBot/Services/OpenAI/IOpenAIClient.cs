namespace ITCoursesBot.ITCoursesBot.Services.OpenAI
{
    public interface IOpenAIClient
    {
        Task<string> GetChatResponseAsync(string userMessage);
        Task<string> TranscribeAudioAsync(Stream audioStream, string fileName);

        Task<Stream> GenerateSpeechAsync(string text, string model = "tts-1", string voice = "alloy", string format = "opus");

    }
}