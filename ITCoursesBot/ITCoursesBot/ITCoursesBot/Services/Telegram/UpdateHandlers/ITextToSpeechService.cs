using ITCoursesBot.ITCoursesBot.Services.OpenAI;
using Telegram.Bot;
using Telegram.Bot.Types;
public interface ITextToSpeechService
{
    Task SendSpeechAsync(
        ITelegramBotClient bot,
        long chatId,
        string text,
        CancellationToken ct);
}

public class OpenAITtsService : ITextToSpeechService
{
    private readonly IOpenAIClient _openAI;
    public OpenAITtsService(IOpenAIClient openAI) => _openAI = openAI;

    public async Task SendSpeechAsync(
        ITelegramBotClient bot,
        long chatId,
        string text,
        CancellationToken ct)
    {
        // Генерируем opus-стрим
        await using var speechStream = await _openAI.GenerateSpeechAsync(
            text, model: "tts-1", voice: "onyx", format: "opus"
        );

        // Используем новый фабричный метод InputFile
        var input = InputFile.FromStream(speechStream, "response.ogg");

        // Отправляем как голосовое сообщение
        await bot.SendVoice(
            chatId: chatId,
            voice: input,
            cancellationToken: ct
        );
    }
}