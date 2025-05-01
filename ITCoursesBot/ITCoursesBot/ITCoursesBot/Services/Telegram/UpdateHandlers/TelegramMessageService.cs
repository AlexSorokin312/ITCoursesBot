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

    // ---------------------- Markdown V2 утилиты ----------------------
    private static readonly char[] _special =
        { '_','*','[',']','(',')','~','`','>','#','+','-','=','|','{','}','.','!' };

    /// Экранирует текст для Markdown V2 (КРОМЕ фрагментов внутри ``` … ```).
    public static string PrepareMarkdownV2(string raw)
    {
        var parts = raw.Split("```", StringSplitOptions.None);
        var sb = new StringBuilder(raw.Length * 2);
        bool inCode = false;

        foreach (var part in parts)
        {
            if (inCode)
            {
                // внутри code-block трогаем только слэши и сами бэктики
                sb.Append("```")
                  .Append(part.Replace(@"\", @"\\").Replace("```", "\\`\\`\\`"))
                  .Append("```");
            }
            else
            {
                foreach (char ch in part)
                    sb.Append(_special.Contains(ch) ? $"\\{ch}" : ch);
            }
            inCode = !inCode;
        }
        return sb.ToString();
    }
    // -----------------------------------------------------------------

    public async Task SendTextAsync(
        long chatId,
        string text,
        InlineKeyboardMarkup? replyMarkup = null,
        bool asMarkdown = false,
        CancellationToken cancellationToken = default)
    {
        await _bot.SendMessage(
            chatId: chatId,
            text: text,
            parseMode: asMarkdown ? ParseMode.MarkdownV2 : ParseMode.None,
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
