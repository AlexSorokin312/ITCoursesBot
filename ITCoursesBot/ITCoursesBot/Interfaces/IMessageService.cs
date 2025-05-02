using Telegram.Bot.Types.ReplyMarkups;

public interface IMessageService
{
    Task SendTextAsync(
        long chatId,
        string text,
        InlineKeyboardMarkup? replyMarkup = null,
        bool asMarkdown = false,
        CancellationToken cancellationToken = default);

    Task SendVoiceAsync(
        long chatId,
        Stream voiceStream,
        string fileName,
        CancellationToken cancellationToken = default);
}
