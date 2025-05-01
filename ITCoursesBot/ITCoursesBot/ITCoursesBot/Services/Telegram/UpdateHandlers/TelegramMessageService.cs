using System.Text;
using System.Text.RegularExpressions;
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

    // 1) Шаблон на code-block (```…```) — всё, что совпало, мы не трогаем.
    private static readonly Regex CodeBlockRegex = new Regex(@"(```[\s\S]*?```)", RegexOptions.Compiled);

    // 2) Regex для экранирования спецсимволов MarkdownV2 (без звёздочек!)
    private static readonly Regex EscapeRegex = new Regex(@"([_\[\]\(\)~`>#+\-=|{}\.\!])", RegexOptions.Compiled);

    public TelegramMessageService(ITelegramBotClient bot) => _bot = bot;

    /// <summary>
    /// Экранирует текст для MarkdownV2:
    /// — не трогает всё, что внутри ```…```
    /// — вне кода экранирует все спецсимволы, кроме звёздочек (*),
    ///   так что **жирный** будет работать.
    /// </summary>
    public static string EscapeMarkdownV2(string text)
    {
        // Разбиваем на фрагменты: либо кусок «code-block», либо простая строка
        var parts = CodeBlockRegex.Split(text);

        for (int i = 0; i < parts.Length; i++)
        {
            // Если это не code-block (не начинается с ```), экранируем все спецсимволы
            if (!parts[i].StartsWith("```"))
            {
                parts[i] = EscapeRegex.Replace(parts[i], "\\$1");
            }
        }

        // Собираем обратно
        return string.Concat(parts);
    }

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
            parseMode: asMarkdown
                               ? ParseMode.MarkdownV2
                               : ParseMode.None,
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
}
