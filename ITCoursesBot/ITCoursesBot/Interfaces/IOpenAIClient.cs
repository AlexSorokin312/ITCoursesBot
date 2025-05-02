namespace ITCoursesBot.Interfaces
{
    public interface IOpenAIClient
    {
        Task<string> GetChatResponseAsync(string userMessage);
        Task<string> GetChatResponseAsync(string systemInstructions, string userMessage);
        Task<AnswerResult> EvaluateAsync(string questionText,
                                         string userAnswer,
                                         CancellationToken cancellationToken);
        Task<string> TranscribeAudioAsync(Stream audioStream, string fileName);
        Task<Stream> GenerateSpeechAsync(string text,
                                           string model = "tts-1",
                                           string voice = "alloy",
                                           string format = "opus");
    }
}