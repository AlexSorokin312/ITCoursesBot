using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Сервис-обёртка над Telegram-API: умеет отправлять текст / аудио
/// и умеет приводить текст к корректному формату Markdown V2.
/// </summary>
public class TelegramMessageService : IMessageService
{
    private readonly ITelegramBotClient _bot;

    public async Task SendTextAsync(
        long chatId,
        string text,
        InlineKeyboardMarkup? replyMarkup = null,
        bool asMarkdown = false,
        CancellationToken cancellationToken = default)
    {
        var finalText = asMarkdown
            ? EscapeMarkdownV2(text)
            : text;

        await _bot.SendMessage(
            chatId: chatId,
            text: finalText,
            parseMode: asMarkdown ? ParseMode.MarkdownV2 : ParseMode.None,
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


        await _bot.SendVoice(
            chatId: chatId,
            voice: InputFile.FromStream(voiceStream, fileName),
            cancellationToken: cancellationToken
        );
    }

    // 1) Шаблон для code-блоков (```…```)
    private static readonly Regex CodeBlockRegex =
        new(@"(```[\s\S]*?```)", RegexOptions.Compiled);

    // 2) Спецсимволы MarkdownV2 (без *)
    private static readonly Regex EscapeRegex =
        new(@"([_\[\]\(\)~`>#+\-=|{}\.\!])", RegexOptions.Compiled);

    public TelegramMessageService(ITelegramBotClient bot) => _bot = bot;

    public static string EscapeMarkdownV2(string text)
    {
        var parts = CodeBlockRegex.Split(text);
        for (int i = 0; i < parts.Length; i++)
        {
            if (!parts[i].StartsWith("```"))
                parts[i] = EscapeRegex.Replace(parts[i], "\\$1");
        }
        return string.Concat(parts);
    }
}