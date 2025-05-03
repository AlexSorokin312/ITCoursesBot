using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

/// <summary>
/// Отправляет текст и голос, превращая Markdown‑похожие маркеры в массив MessageEntity.
/// </summary>
public class TelegramMessageService : IMessageService
{
    private readonly ITelegramBotClient _bot;

    public TelegramMessageService(ITelegramBotClient bot)
        => _bot = bot;

    public async Task SendTextAsync(
        long chatId,
        string rawText,
        InlineKeyboardMarkup? replyMarkup = null,
        CancellationToken ct = default)
    {
        // 1) распарсим Markdown-подобную разметку в "plain" и соберём MessageEntity[]
        var (plain, entities) = ParseMarkdownLike(rawText);

        // 2) отправляем через настоящий метод SendTextMessageAsync:
        await _bot.SendMessage(
            chatId: chatId,
            text: plain,
            parseMode: ParseMode.None,   // не нужен, мы передаём entities
            entities: entities,
            disableNotification: false,
            replyMarkup: replyMarkup,
            cancellationToken: ct);
    }

    public async Task SendVoiceAsync(
        long chatId,
        Stream voiceStream,
        string fileName,
        CancellationToken ct = default)
    {
        await _bot.SendVoice(
            chatId: chatId,
            voice: InputFile.FromStream(voiceStream, fileName),
            cancellationToken: ct);
    }

    /// <summary>
    /// Разбирает "**bold**", "*italic*", "`inline`", "```block```"
    /// и возвращает plain-text + массив MessageEntity.
    /// </summary>
    private static (string Plain, MessageEntity[] Entities) ParseMarkdownLike(string md)
    {
        var entities = new List<MessageEntity>();
        var sb = new StringBuilder();
        int utf16Pos = 0;

        for (int i = 0; i < md.Length;)
        {
            // 1) ```block```
            if (i + 3 <= md.Length && md.Substring(i, 3) == "```")
            {
                int end = md.IndexOf("```", i + 3, StringComparison.Ordinal);
                if (end < 0) end = md.Length;
                string code = md.Substring(i + 3, end - (i + 3));

                entities.Add(new MessageEntity
                {
                    Type = MessageEntityType.Pre,
                    Offset = utf16Pos,
                    Length = code.Length
                });

                sb.Append(code);
                utf16Pos += code.Length;
                i = end + 3;
                continue;
            }

            // 2) `inline`
            if (md[i] == '`')
            {
                int end = md.IndexOf('`', i + 1);
                if (end < 0) end = md.Length;
                string code = md.Substring(i + 1, end - (i + 1));

                entities.Add(new MessageEntity
                {
                    Type = MessageEntityType.Code,
                    Offset = utf16Pos,
                    Length = code.Length
                });

                sb.Append(code);
                utf16Pos += code.Length;
                i = end + 1;
                continue;
            }

            // 3) **bold**
            if (i + 2 < md.Length && md[i] == '*' && md[i + 1] == '*')
            {
                int end = md.IndexOf("**", i + 2, StringComparison.Ordinal);
                if (end < 0)
                {
                    // не нашли закрывающих — просто копируем символ
                    sb.Append(md[i]);
                    utf16Pos++;
                    i++;
                }
                else
                {
                    string bold = md.Substring(i + 2, end - (i + 2));
                    entities.Add(new MessageEntity
                    {
                        Type = MessageEntityType.Bold,
                        Offset = utf16Pos,
                        Length = bold.Length
                    });

                    sb.Append(bold);
                    utf16Pos += bold.Length;
                    i = end + 2;
                }
                continue;
            }

            // 4) *italic*
            if (md[i] == '*')
            {
                int end = md.IndexOf('*', i + 1);
                if (end < 0)
                {
                    sb.Append(md[i]);
                    utf16Pos++;
                    i++;
                }
                else
                {
                    string italic = md.Substring(i + 1, end - (i + 1));
                    entities.Add(new MessageEntity
                    {
                        Type = MessageEntityType.Italic,
                        Offset = utf16Pos,
                        Length = italic.Length
                    });

                    sb.Append(italic);
                    utf16Pos += italic.Length;
                    i = end + 1;
                }
                continue;
            }

            // 5) всё остальное
            sb.Append(md[i]);
            utf16Pos++;
            i++;
        }

        return (sb.ToString(), entities.ToArray());
    }
}