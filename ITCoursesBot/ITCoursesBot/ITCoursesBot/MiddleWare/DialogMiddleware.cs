using Bot.Ports;
using ITCoursesBot.Interfaces;
using ITCoursesBot.ITCoursesBot.Configuration;
using Microsoft.Extensions.Options;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

public class DialogMiddleware : IUpdateMiddleware
{
    private readonly IOpenAIClient _openAi;
    private readonly OpenAISettings _settings;
    private readonly IAiLimitClient _aiLimit;
    private readonly IMessageService _msg;
    private readonly IKeyboardBuilder _kbd;
    private readonly ISessionManager _sessions;

    public DialogMiddleware(
        IOpenAIClient openAi,
        IOptions<OpenAISettings> options,
        IMessageService msg,
        IKeyboardBuilder kbd,
        ISessionManager sessions,
        IAiLimitClient aiLimit)
    {
        _openAi = openAi;
        _settings = options.Value;
        _msg = msg;
        _kbd = kbd;
        _sessions = sessions;
        _aiLimit = aiLimit;
    }

    public async Task InvokeAsync(UpdateContext ctx, Func<Task> next)
    {
        var chatId = ctx.GetChatId();
        var session = _sessions.GetOrCreateSession(chatId);
        var cbData = ctx.Update.CallbackQuery?.Data;

        try
        {
            // 0) «В главное меню»
            if (cbData == KeyboardBuilder.BACK_TO_MENU_BUTTON_NAME)
            {
                session.Mode = BotMode.None;
                LoggerService.LogInfo($"Пользователь {chatId} вернулся в главное меню из диалога");

                var info = await SafeGetRequestInfo(chatId, ctx.CancellationToken);
                await SafeSendAsync(
                    chatId,
                    $"🏠 Главное меню:\nВыберите опцию\n\n" +
                    $"⚡ Оставшиеся запросы: **{info.RemainingRequests}**\n" +
                    $"⏰ Время до сброса: **{info.TimeToReset.Hours}ч {info.TimeToReset.Minutes}мин**",
                    _kbd.Build(),
                    ctx.CancellationToken);
                return;
            }

            // 1) Переход в режим диалога
            if (cbData == KeyboardBuilder.DIALOG_BUTTON_NAME)
            {
                session.Mode = BotMode.Dialog;
                LoggerService.LogInfo($"Пользователь {chatId} вошёл в режим диалога");

                await SafeSendAsync(
                    chatId,
                    "💬 Режим диалога: задайте вопрос по программированию (текст или голос).",
                    _kbd.BuildBackToMenu(),
                    ctx.CancellationToken);
                return;
            }

            // 2) В режиме Dialog
            if (session.Mode == BotMode.Dialog)
            {
                var message = ctx.Update.Message;
                if (message != null)
                {
                    string userQuestion = null;

                    if (!string.IsNullOrWhiteSpace(message.Text))
                    {
                        userQuestion = message.Text.Trim();
                    }
                    else if (message.Voice != null)
                    {
                        try
                        {
                            using var audio = await DownloadVoiceAsync(ctx, message.Voice.FileId, ctx.CancellationToken);
                            userQuestion = await _openAi.TranscribeAudioAsync(
                                audio,
                                $"{message.Voice.FileUniqueId}.ogg");
                        }
                        catch (Exception ex)
                        {
                            LoggerService.LogError($"Ошибка транскрипции голоса для {chatId}: {ex.Message}");
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(userQuestion))
                    {
                        LoggerService.LogInfo($"Пользователь {chatId} спрашивает: {userQuestion}");

                        var prompt = new StringBuilder()
                            .AppendLine(_settings.InstructionsDialog)
                            .AppendLine()
                            .AppendLine("Вопрос пользователя:")
                            .AppendLine(userQuestion)
                            .ToString();

                        string answer;
                        try
                        {
                            answer = await _openAi.GetChatResponseAsync(prompt, userQuestion);
                        }
                        catch (Exception ex)
                        {
                            LoggerService.LogError($"Ошибка запроса к OpenAI для {chatId}: {ex.Message}");
                            await SafeSendAsync(
                                chatId,
                                "❌ Не удалось получить ответ от ИИ. Попробуйте позже.",
                                _kbd.BuildBackToMenu(),
                                ctx.CancellationToken);
                            return;
                        }

                        await SafeSendAsync(
                            chatId,
                            answer,
                            _kbd.BuildBackToMenu(),
                            ctx.CancellationToken);
                        return;
                    }

                    LoggerService.LogDebug($"В режиме диалога от {chatId} пришло пустое или неподходящее сообщение");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Неожиданная ошибка в DialogMiddleware для {chatId}: {ex.Message}");
        }

        // 3) Передаём дальше
        try
        {
            await next();
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Ошибка downstream после DialogMiddleware для {chatId}: {ex.Message}");
        }
    }

    private async Task<(int RemainingRequests, TimeSpan TimeToReset)> SafeGetRequestInfo(long chatId, CancellationToken ct)
    {
        try
        {
            var info = await _aiLimit.GetRequestInfoAsync(chatId, ct);
            return (info.RemainingRequests, info.TimeToReset);
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Не удалось получить информацию по квоте для {chatId}: {ex.Message}");
            return (0, TimeSpan.Zero);
        }
    }

    private async Task SafeSendAsync(
        long chatId,
        string text,
        InlineKeyboardMarkup markup,
        CancellationToken ct)
    {
        try
        {
            await _msg.SendTextAsync(
                chatId,
                text,
                replyMarkup: markup,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Не удалось отправить сообщение чату {chatId}: {ex.Message}");
        }
    }

    private async Task<Stream> DownloadVoiceAsync(UpdateContext ctx, string fileId, CancellationToken ct)
    {
        var fileInfo = await ctx.BotClient.GetFile(fileId, cancellationToken: ct);
        var ms = new MemoryStream();
        await ctx.BotClient.DownloadFile(fileInfo.FilePath!, ms, cancellationToken: ct);
        ms.Position = 0;
        return ms;
    }
}
