using Bot.Ports;
using ITCoursesBot.Interfaces;
using ITCoursesBot.ITCoursesBot.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types.ReplyMarkups;

public class CodeExplainMiddleware : IUpdateMiddleware
{
    private readonly IOpenAIClient _openAi;
    private readonly OpenAISettings _settings;
    private readonly IAiLimitClient _aiLimit;
    private readonly IMessageService _msg;
    private readonly IKeyboardBuilder _kbd;
    private readonly ISessionManager _sessions;
    private readonly ILogger<CodeExplainMiddleware> _logger;

    public CodeExplainMiddleware(
        IOpenAIClient openAi,
        IOptions<OpenAISettings> settings,
        IMessageService msg,
        IKeyboardBuilder kbd,
        ISessionManager sessions,
        ILogger<CodeExplainMiddleware> logger,
        IAiLimitClient aiLimit)
    {
        _openAi = openAi;
        _settings = settings.Value;
        _msg = msg;
        _kbd = kbd;
        _sessions = sessions;
        _logger = logger;
        _aiLimit = aiLimit;
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
            _logger.LogInformation("Пользователь {ChatId} вернулся в главное меню из кода", chatId);

            // Получаем информацию о запросах пользователя
            var requestInfo = await _aiLimit.GetRequestInfoAsync(chatId, ctx.CancellationToken);

            // Форматируем сообщение с информацией о запросах
            string requestInfoMessage = $"⚡Оставшиеся запросы: **{requestInfo.RemainingRequests}**\n" +
                                        $"⏰ **Время до сброса**: **{requestInfo.TimeToReset.Hours}ч {requestInfo.TimeToReset.Minutes}мин**";

            await SafeSendAsync(
                chatId,
                $"🏠 Главное меню:\nВыберите, чем займёмся дальше!\n\n{requestInfoMessage}",
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
                _logger.LogInformation("Пользователь {ChatId} переключился в режим объяснения кода", chatId);

                await SafeSendAsync(
                    chatId,
                    "💡 Включён режим «Объяснение кода». Пришлите фрагмент кода, и я его объясню.",
                    _kbd.BuildBackToMenu(),
                    ctx.CancellationToken);
                return;
            }

            // 2) Если в режиме CodeExplain и пришёл текст или голос
            if (session.Mode == BotMode.CodeExplain)
            {
                var msgText = ctx.Update.Message?.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(msgText))
                {
                    _logger.LogInformation("Пользователь {ChatId} прислал код для объяснения", chatId);

                    string explanation;
                    try
                    {
                        explanation = await _openAi.GetChatResponseAsync(
                            _settings.InstructionsCodeExplain,
                            msgText);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при запросе к OpenAI для объяснения кода у {ChatId}", chatId);
                        await SafeSendAsync(
                            chatId,
                            "❌ Не удалось получить объяснение от ИИ. Попробуйте повторить чуть позже.",
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
                _logger.LogDebug("В режиме CodeExplain от {ChatId} пришёл не текст", chatId);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Неожиданная ошибка в CodeExplainMiddleware для {ChatId}", chatId);
        }

        // 3) Передаём дальше, если не обработали
        try
        {
            await next();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в downstream после CodeExplainMiddleware для {ChatId}", chatId);
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
            _logger.LogError(ex, "Не удалось отправить сообщение пользователю {ChatId}: {Text}", chatId, text);
        }
    }

    #endregion
}
