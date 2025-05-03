// TelegramMessageService.cs
// .NET 6 · Telegram.Bot 18.x

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
/// Отправляет текст и голос, превращая простую Markdown-подобную разметку
/// ( **bold**, *italic*, `inline`, ```block``` ) в массив MessageEntity,
/// пропуская возможный языковой stub (csharp, lua, copy) перед блоком кода.
/// </summary>
public sealed class TelegramMessageService : IMessageService
{
    private readonly ITelegramBotClient _bot;

    public TelegramMessageService(ITelegramBotClient bot) => _bot = bot;

    public async Task SendTextAsync(
        long chatId,
        string rawText,
        InlineKeyboardMarkup? replyMarkup = null,
        CancellationToken cancellationToken = default)
    {
        var (plain, entities) = ParseMarkdownLike(rawText);

        await _bot.SendMessage(
            chatId: chatId,
            text: plain,
            parseMode: ParseMode.None,
            entities: entities,
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
            chatId: chatId,
            voice: InputFile.FromStream(voiceStream, fileName),
            cancellationToken: cancellationToken);
    }

    private static (string Plain, MessageEntity[] Entities) ParseMarkdownLike(string md)
    {
        var entities = new List<MessageEntity>();
        var sb = new StringBuilder();
        int utf16Pos = 0;

        for (int i = 0; i < md.Length;)
        {
            // 1) ```code-block``` (возможен stub перед кодом)
            if (i + 3 <= md.Length && md[i] == '`' && md[i + 1] == '`' && md[i + 2] == '`')
            {
                int langStart = i + 3;
                int firstNl = md.IndexOf('\n', langStart);
                if (firstNl < 0) firstNl = langStart;

                string potentialLang = md.Substring(langStart, firstNl - langStart);
                bool hasLangStub = potentialLang.Length > 0 && IsAlphaNum(potentialLang);

                int codeStart = hasLangStub ? firstNl + 1 : langStart;
                int end = md.IndexOf("```", codeStart, StringComparison.Ordinal);
                if (end < 0) end = md.Length;

                string rawCode = md.Substring(codeStart, end - codeStart);
                string code = StripLangStub(rawCode);

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
            if (i + 1 < md.Length && md[i] == '*' && md[i + 1] == '*')
            {
                int end = md.IndexOf("**", i + 2, StringComparison.Ordinal);
                if (end < 0)
                {
                    AppendChar(md[i++]);
                    continue;
                }

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
                continue;
            }

            // 4) *italic*
            if (md[i] == '*')
            {
                int end = md.IndexOf('*', i + 1);
                if (end < 0)
                {
                    AppendChar(md[i++]);
                    continue;
                }

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
                continue;
            }

            // 5) обычный символ
            AppendChar(md[i++]);
        }

        return (sb.ToString(), entities.ToArray());

        void AppendChar(char c)
        {
            sb.Append(c);
            utf16Pos += 1;
        }

        static bool IsAlphaNum(string s)
        {
            foreach (char ch in s)
                if (!char.IsLetterOrDigit(ch)) return false;
            return true;
        }

        static string StripLangStub(string code)
        {
            int nl = code.IndexOf('\n');
            if (nl < 0) return code;

            string firstLine = code.Substring(0, nl).Trim();
            if (firstLine.Equals("csharp", StringComparison.OrdinalIgnoreCase) ||
                firstLine.Equals("lua", StringComparison.OrdinalIgnoreCase) ||
                firstLine.Equals("copy", StringComparison.OrdinalIgnoreCase))
            {
                return code[(nl + 1)..];
            }

            return code;
        }
    }
}
