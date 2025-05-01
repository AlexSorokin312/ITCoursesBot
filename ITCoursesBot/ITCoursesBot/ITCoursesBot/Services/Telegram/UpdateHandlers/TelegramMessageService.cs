using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

public interface IMessageService
{
    Task SendTextAsync(
        long chatId,
        string text,
        InlineKeyboardMarkup? replyMarkup = null,
        CancellationToken cancellationToken = default);

    Task SendVoiceAsync(
        long chatId,
        Stream voiceStream,
        string fileName,
        CancellationToken cancellationToken = default);
}

public class TelegramMessageService : IMessageService
{
    private readonly ITelegramBotClient _bot;

    public TelegramMessageService(ITelegramBotClient bot)
    {
        _bot = bot;
    }

    public async Task SendTextAsync(
        long chatId,
        string text,
        InlineKeyboardMarkup? replyMarkup = null,
        CancellationToken cancellationToken = default)
    {
        await _bot.SendMessage(
            chatId: chatId,
            text: text,
            replyMarkup: replyMarkup,
            cancellationToken: cancellationToken
        );
    }

    public async Task SendVoiceAsync(
        long chatId,
        Stream voiceStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var input = InputFile.FromStream(voiceStream, fileName);
        await _bot.SendVoice(
            chatId: chatId,
            voice: input,
            cancellationToken: cancellationToken
        );
    }
}