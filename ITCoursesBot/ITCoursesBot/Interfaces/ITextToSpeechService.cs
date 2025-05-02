using Telegram.Bot;

public interface ITextToSpeechService
{
    Task SendSpeechAsync(
        ITelegramBotClient bot,
        long chatId,
        string text,
        CancellationToken ct);
}