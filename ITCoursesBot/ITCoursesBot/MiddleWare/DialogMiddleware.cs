using Bot.Ports;
using ITCoursesBot.Interfaces;
using ITCoursesBot.ITCoursesBot.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

public class DialogMiddleware : IUpdateMiddleware
{
    private readonly IOpenAIClient _openAi;
    private readonly OpenAISettings _settings;
    private readonly IMessageService _msg;
    private readonly IAiLimitClient _aiLimit;
    private readonly IKeyboardBuilder _kbd;
    private readonly ISessionManager _sessions;
    private readonly ILogger<DialogMiddleware> _logger;

    public DialogMiddleware(
        IOpenAIClient openAi,
        IOptions<OpenAISettings> options,
        IMessageService msg,
        IKeyboardBuilder kbd,
        ISessionManager sessions,
        ILogger<DialogMiddleware> logger,
        IAiLimitClient aiLimit)
    {
        _openAi = openAi;
        _settings = options.Value;
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

        try
        {
            // 0) «В главное меню»
            if (cbData == KeyboardBuilder.BACK_TO_MENU_BUTTON_NAME)
            {
                session.Mode = BotMode.None;

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

            // 1) Переход в режим диалога
            if (cbData == KeyboardBuilder.DIALOG_BUTTON_NAME)
            {
                session.Mode = BotMode.Dialog;
                _logger.LogInformation("Пользователь {ChatId} вошёл в режим диалога", chatId);
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
                            _logger.LogError(ex, "Ошибка при транскрипции голоса в DialogMiddleware для {ChatId}", chatId);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(userQuestion))
                    {
                        _logger.LogInformation("Пользователь {ChatId} спрашивает: {Question}", chatId, userQuestion);

                        string prompt = new StringBuilder()
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
                            _logger.LogError(ex, "Ошибка при запросе к OpenAI в DialogMiddleware для {ChatId}", chatId);
                            await SafeSendAsync(
                                chatId,
                                "❌ Не удалось получить ответ от ИИ. Попробуйте чуть позже.",
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

                    _logger.LogDebug("Пустое сообщение или неподдерживаемый формат в режиме диалога от {ChatId}", chatId);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Неожиданная ошибка в DialogMiddleware для {ChatId}", chatId);
        }

        // 3) Передаём дальше
        try
        {
            await next();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка downstream после DialogMiddleware для {ChatId}", chatId);
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
