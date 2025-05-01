using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
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

/// <summary>
/// Сервис-обёртка над Telegram-API: умеет отправлять текст / аудио
/// и умеет приводить текст к корректному формату Markdown V2.
/// </summary>
public class TelegramMessageService : IMessageService
{
    private readonly ITelegramBotClient _bot;

    public TelegramMessageService(ITelegramBotClient bot) => _bot = bot;


    public static string EscapeMarkdownV2(string text)
    {
        var escapeChars = "_*[]()~`>#+-=|{}.!";
        var sb = new StringBuilder();
        bool inCodeBlock = false;

        foreach (var line in text.Split('\n'))
        {
            if (line.StartsWith("```"))
            {
                inCodeBlock = !inCodeBlock;
                sb.AppendLine(line);
                continue;
            }

            if (inCodeBlock)
            {
                // В код-блоке эскейпим только '\' и '`'
                sb.AppendLine(line
                    .Replace(@"\", @"\\")
                    .Replace("`", "\\`"));
            }
            else
            {
                foreach (char c in line)
                    sb.Append(escapeChars.Contains(c) ? $"\\{c}" : c.ToString());
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }

    public async Task SendTextAsync(
        long chatId,
        string text,
        InlineKeyboardMarkup? replyMarkup = null,
        bool asMarkdown = false,
        CancellationToken cancellationToken = default)
    {
        var finalText = asMarkdown
            ? TelegramMessageService.EscapeMarkdownV2(text)
            : text;

        await _bot.SendMessage(
            chatId: chatId,
            text: finalText,
            parseMode: asMarkdown
                ? ParseMode.MarkdownV2
                : ParseMode.None,
            replyMarkup: replyMarkup,
            cancellationToken: cancellationToken);
    }

    public async Task SendVoiceAsync(
        long chatId,
        Stream voiceStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        await _bot.SendVoice(
            chatId,
            InputFile.FromStream(voiceStream, fileName),
            cancellationToken: cancellationToken);
    }
}
