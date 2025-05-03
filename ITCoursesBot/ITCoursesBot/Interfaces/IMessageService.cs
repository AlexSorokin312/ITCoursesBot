using Telegram.Bot.Types.ReplyMarkups;

public interface IMessageService
{
    Task SendTextAsync(
        long chatId,
        string rawText,
        InlineKeyboardMarkup? replyMarkup = null,
        CancellationToken     ct          = default);

    Task SendVoiceAsync(
        long chatId,
        Stream voiceStream,
        string fileName,
        CancellationToken cancellationToken = default);
}
