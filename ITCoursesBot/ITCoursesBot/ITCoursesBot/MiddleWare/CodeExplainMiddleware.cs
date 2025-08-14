using Bot.Ports;
using ITCoursesBot.Interfaces;
using ITCoursesBot.ITCoursesBot.Configuration;
using Microsoft.Extensions.Options;
using System.Net;
using Telegram.Bot.Types.ReplyMarkups;

public class CodeExplainMiddleware : IUpdateMiddleware
{
    private readonly IOpenAIClient _openAi;
    private readonly OpenAISettings _settings;
    private readonly IAiLimitClient _aiLimit;
    private readonly IMessageService _msg;
    private readonly IKeyboardBuilder _kbd;
    private readonly ISessionManager _sessions;
    private readonly IDialogHistoryClient _dialogHistory;

    public CodeExplainMiddleware(
        IOpenAIClient openAi,
        IOptions<OpenAISettings> settings,
        IMessageService msg,
        IKeyboardBuilder kbd,
        ISessionManager sessions,
        IAiLimitClient aiLimit,
        IDialogHistoryClient dialogHistory)   
    {
        _openAi = openAi;
        _settings = settings.Value;
        _msg = msg;
        _kbd = kbd;
        _sessions = sessions;
        _aiLimit = aiLimit;
        _dialogHistory = dialogHistory;       // <— добавили
    }

    public async Task InvokeAsync(UpdateContext ctx, Func<Task> next)
    {
        var chatId = ctx.GetChatId();
        var session = _sessions.GetOrCreateSession(chatId);
        var cbData = ctx.Update.CallbackQuery?.Data;

        // 0) Обработка «В главное меню»
        if (cbData == KeyboardBuilder.BACK_TO_MENU_BUTTON_NAME)
        {
            session.Mode = BotMode.None;
            LoggerService.LogInfo($"Пользователь {chatId} вернулся в главное меню из режима объяснения кода");

            var info = await SafeGetRequestInfo(chatId, ctx.CancellationToken);
            await SafeSendAsync(
                chatId,
                $"🏠 Главное меню:\nВыберите, чем займёмся дальше!\n\n" +
                $"⚡ Оставшиеся запросы: **{info.RemainingRequests}**\n" +
                $"⏰ Время до сброса: **{info.TimeToReset.Hours}ч {info.TimeToReset.Minutes}мин**",
                _kbd.Build(),
                ctx.CancellationToken);
            return;
        }

        try
        {
            // 1) Входим в режим объяснения кода
            if (cbData == KeyboardBuilder.CODE_EXPLANATION_BUTTON_NAME)
            {
                session.Mode = BotMode.CodeExplain;
                LoggerService.LogInfo($"Пользователь {chatId} переключился в режим объяснения кода");

                await SafeSendAsync(
                    chatId,
                    "💡 Включён режим «Объяснение кода». Пришлите фрагмент кода, и я его объясню.",
                    _kbd.BuildBackToMenu(),
                    ctx.CancellationToken);
                return;
            }

            // 2) Если в режиме CodeExplain и пришёл текст
            if (session.Mode == BotMode.CodeExplain)
            {
                var msgText = ctx.Update.Message?.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(msgText))
                {
                    LoggerService.LogInfo($"Пользователь {chatId} прислал код для объяснения");

                    string explanation;
                    try
                    {
                        var wrote = await TryWriteHistoryAsync(chatId, msgText, ctx.CancellationToken);
                        if (!wrote)
                        {
                            var info = await SafeGetRequestInfo(chatId, ctx.CancellationToken);
                            await SafeSendAsync(
                                chatId,
                                $"⛔ Лимит запросов исчерпан.\n" +
                                $"Осталось: {info.RemainingRequests}\n" +
                                $"Попробуйте позже.",
                                _kbd.BuildBackToMenu(),
                                ctx.CancellationToken);
                            return;
                        }
                        explanation = await _openAi.GetChatResponseAsync(
                            _settings.InstructionsCodeExplain,
                            msgText);
                    }
                    catch (Exception ex)
                    {
                        LoggerService.LogError($"Ошибка при запросе к OpenAI для объяснения кода у {chatId}: {ex.Message}");
                        await SafeSendAsync(
                            chatId,
                            "❌ Не удалось получить объяснение от ИИ. Попробуйте позже.",
                            _kbd.BuildBackToMenu(),
                            ctx.CancellationToken);
                        return;
                    }

                    await SafeSendAsync(
                        chatId,
                        explanation,
                        _kbd.BuildBackToMenu(),
                        ctx.CancellationToken);
                    return;
                }

                // если пришло не текстовое сообщение — игнорируем
                LoggerService.LogDebug($"В режиме CodeExplain от {chatId} пришло некорректное сообщение");
                return;
            }
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Неожиданная ошибка в CodeExplainMiddleware для {chatId}: {ex.Message}");
        }

        // 3) Передаём дальше
        try
        {
            await next();
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Ошибка в downstream после CodeExplainMiddleware для {chatId}: {ex.Message}");
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

    #region SafeSendAsync

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

    private async Task<bool> TryWriteHistoryAsync(long chatId, string message, CancellationToken ct)
    {
        try
        {
            await _dialogHistory.WriteAsync(new CreateDialogRequest
            {
                TelegramId = chatId,
                Message = message,
                CountAsRequest = true
            }, ct);

            return true;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            LoggerService.LogInfo($"Лимит исчерпан при записи истории для {chatId}");
            return false;
        }
        catch (Exception ex)
        {
            // Не блокируем логику объяснения кода из-за сбоя логирования истории
            LoggerService.LogError($"Не удалось записать историю (CodeExplain) для {chatId}: {ex.Message}");
            return true;
        }
    }

    #endregion
}
