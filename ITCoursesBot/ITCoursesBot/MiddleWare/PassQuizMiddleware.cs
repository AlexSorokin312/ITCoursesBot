using Bot.Ports;
using ITCoursesBot.Interfaces;
using ITCoursesBot.ITCoursesBot.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

public class PassQuizMiddleware : IUpdateMiddleware
{
    private readonly IMessageService _messageService;
    private readonly IKeyboardBuilder _kbd;
    private readonly IProgressClient _progress;
    private readonly IAiLimitClient _aiLimitClient;
    private readonly IOpenAIClient _openAi;
    private readonly OpenAISettings _settings;
    private readonly ISessionManager _sessions;
    private readonly ILogger<PassQuizMiddleware> _log;

    public PassQuizMiddleware(
        IMessageService msg,
        IKeyboardBuilder kbd,
        IProgressClient progressClient,
        IAiLimitClient aiLimitClient,
        IOpenAIClient openAi,
        IOptions<OpenAISettings> settings,
        ISessionManager sessions,
        ILogger<PassQuizMiddleware> log)
    {
        _messageService = msg;
        _kbd = kbd;
        _progress = progressClient;
        _aiLimitClient = aiLimitClient;
        _openAi = openAi;
        _settings = settings.Value;
        _sessions = sessions;
        _log = log;
    }

    public async Task InvokeAsync(UpdateContext ctx, Func<Task> next)
    {
        var chatId = ctx.GetChatId();
        var session = _sessions.GetOrCreateSession(chatId);
        var cbData = ctx.Update.CallbackQuery?.Data;

        try
        {
            // 1) Только если режим PassQuiz
            if (session.Mode != BotMode.PassQuiz)
            {
                await next();
                return;
            }

            // 0) Обработка «В главное меню»
            if (cbData == KeyboardBuilder.BACK_TO_MENU_BUTTON_NAME)
            {
                session.Mode = BotMode.None;
                session.QuestionsForQuiz = null;
                session.QuestionIndex = 0;
                _log.LogInformation("Пользователь {ChatId} вернулся в главное меню", chatId);

                // Получаем информацию о запросах пользователя
                var requestInfo = await _aiLimitClient.GetRequestInfoAsync(chatId, ctx.CancellationToken);

                // Форматируем сообщение с информацией о запросах
                string requestInfoMessage = $"⚡Оставшиеся запросы: **{requestInfo.RemainingRequests}**\n" +
                                            $"⏰ **Время до сброса**: **{requestInfo.TimeToReset.Hours}ч {requestInfo.TimeToReset.Minutes}мин**";

                // Отправляем сообщение с главным меню и информацией о запросах
                await SafeSendAsync(
                    chatId,
                    $"🏠 Главное меню:\nВыберите, чем займёмся дальше!\n\n{requestInfoMessage}",
                    _kbd.Build(),
                    ctx.CancellationToken);

                return;
            }

            // 2) Обработка inline-кнопок
            if (!string.IsNullOrEmpty(cbData))
            {
                switch (cbData)
                {
                    case KeyboardBuilder.NEXT_QUESTION_BUTTON_NAME:
                        _log.LogInformation("Пользователь {ChatId} нажал «Следующий вопрос»", chatId);
                        await SendNextQuestionAsync(ctx, session);
                        return;

                    case KeyboardBuilder.FINISH_BUTTON_NAME:
                        _log.LogInformation("Пользователь {ChatId} нажал «Завершить»", chatId);
                        await FinishQuizAsync(ctx, session);
                        return;

                    case KeyboardBuilder.SHOW_ALL_BUTTON_NAME:
                        _log.LogInformation("Пользователь {ChatId} нажал «Показать все вопросы»", chatId);
                        await ShowAllQuestionsAsync(ctx, session);
                        return;
                }
            }

            // 3) Обработка ответа (текст или голос)
            var msg = ctx.Update.Message;
            if (msg == null)
                return;

            string answer;
            if (!string.IsNullOrWhiteSpace(msg.Text))
            {
                answer = msg.Text.Trim();
            }
            else
            {
                answer = await SafeTranscribeAsync(ctx, msg);
            }

            if (string.IsNullOrWhiteSpace(answer))
                return;

            // 4) Проверка квоты ИИ
            bool limitReached = false;
            try
            {
                limitReached = await _aiLimitClient.IsLimitReachedAsync(chatId, ctx.CancellationToken);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Ошибка проверки квоты ИИ для {ChatId}", chatId);
            }
            if (limitReached)
            {
                await SafeSendAsync(
                    chatId,
                    "❗️ Квота запросов к ИИ исчерпана. Попробуйте позже.",
                    ctx.CancellationToken);
                return;
            }

            // 5) Если вопросов нет — выход
            if (session.QuestionsForQuiz == null || session.QuestionsForQuiz.Count == 0)
            {
                _log.LogWarning("Нет вопросов в сессии для {ChatId}", chatId);
                await SafeSendAsync(
                    chatId,
                    "❗️ Вопросы не загружены. Введите /beginquiz для начала теста.",
                    ctx.CancellationToken);
                return;
            }

            // 6) Обработка ответа
            await ProcessAnswerAsync(ctx, session, answer);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Неожиданная ошибка в PassQuizMiddleware для {ChatId}", chatId);
        }

        // 7) Передаём дальше, если не обработали
        try
        {
            await next();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Ошибка downstream после PassQuizMiddleware для {ChatId}", chatId);
        }
    }

    private async Task<string?> SafeTranscribeAsync(UpdateContext ctx, Message msg)
    {
        try
        {
            if (msg.Voice == null) return null;
            await using var stream = await DownloadVoiceAsync(ctx, msg.Voice.FileId, ctx.CancellationToken);
            return (await _openAi.TranscribeAudioAsync(stream, $"{msg.Voice.FileUniqueId}.ogg"))?.Trim();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Ошибка транскрипции голоса для {ChatId}", ctx.GetChatId());
            return null;
        }
    }

    private async Task ProcessAnswerAsync(UpdateContext ctx, ChatSession session, string answer)
    {
        var chatId = ctx.GetChatId();
        var dto = session.QuestionsForQuiz![session.QuestionIndex];

        string raw = null;
        try
        {
            raw = await _openAi.GetChatResponseAsync(
                _settings.InstructionsQuestions,
                $"Вопрос: {dto.Text}\nОтвет студента: {answer}");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Ошибка запроса к OpenAI для {ChatId}", chatId);
        }

        var (isCorrect, comment) = raw != null
            ? ParseAiResponse(raw)
            : (false, "⚠️ Не удалось получить ответ от ИИ, попробуйте снова.");

        session.PassQuizResults.Add(isCorrect);

        try
        {
            await _progress.AddAnswerAsync(
                new AddAnswerDto(ctx.GetUserId(), dto.Id, answer, isCorrect),
                ctx.CancellationToken);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Ошибка сохранения ответа пользователя {ChatId}", chatId);
        }

        await SafeSendAsync(chatId, comment, ctx.CancellationToken);

        if (isCorrect)
            await SendCorrectOrNextAsync(ctx, session);
        else
            await SafeSendAsync(
                chatId,
                "❌ Неправильно. Попробуйте ещё раз.",
                ctx.CancellationToken);
    }

    private (bool IsCorrect, string Comment) ParseAiResponse(string aiReply)
    {
        const string TrueTag = "(TRUE_ANSWER)";
        const string FalseTag = "(FALSE_ANSWER)";
        bool ok = aiReply.Contains(TrueTag, StringComparison.OrdinalIgnoreCase);
        var cleaned = aiReply
            .Replace(TrueTag, "", StringComparison.OrdinalIgnoreCase)
            .Replace(FalseTag, "", StringComparison.OrdinalIgnoreCase)
            .Trim();
        return (ok, cleaned);
    }

    private async Task SendCorrectOrNextAsync(UpdateContext ctx, ChatSession session)
    {
        session.QuestionIndex++;
        var chatId = ctx.GetChatId();
        var total = session.QuestionsForQuiz!.Count;

        if (session.QuestionIndex >= total)
        {
            _log.LogInformation("Пользователь {ChatId} прошёл все {Total} вопросов", chatId, total);

            // Получаем информацию о запросах пользователя
            var requestInfo = await _aiLimitClient.GetRequestInfoAsync(chatId, ctx.CancellationToken);

            // Форматируем сообщение с информацией о запросах
            string requestInfoMessage = $"⚡Оставшиеся запросы: **{requestInfo.RemainingRequests}**\n" +
                                        $"⏰ **Время до сброса**: **{requestInfo.TimeToReset.Hours}ч {requestInfo.TimeToReset.Minutes}мин**";

            await SafeSendAsync(
                chatId,
                $"✅ Все вопросы пройдены! Выберите следующее действие: \n{requestInfoMessage}",
                _kbd.Build(),
                ctx.CancellationToken);

            session.Mode = BotMode.None;
            session.QuestionsForQuiz?.Clear();
            session.QuestionIndex = 0;
            return;
        }
        else
        {
            await SafeSendAsync(
                chatId,
                "✅ Правильно! Перейти к следующему?",
                _kbd.BuildNextFinish(true),
                ctx.CancellationToken);
        }
    }

    private async Task ShowAllQuestionsAsync(UpdateContext ctx, ChatSession session)
    {
        var chatId = ctx.GetChatId();
        var sb = new StringBuilder();
        for (int i = session.QuestionIndex; i < session.QuestionsForQuiz!.Count; i++)
            sb.AppendLine($"{i + 1}. {session.QuestionsForQuiz[i].Text}");

        await SafeSendAsync(
            chatId,
            sb.Length > 0 ? sb.ToString() : "❗️ Нет оставшихся вопросов.",
            _kbd.BuildBackToMenu(),
            ctx.CancellationToken);
    }

    private async Task SendNextQuestionAsync(UpdateContext ctx, ChatSession session)
    {
        var chatId = ctx.GetChatId();
        var idx = session.QuestionIndex + 1;
        var total = session.QuestionsForQuiz!.Count;
        var dto = session.QuestionsForQuiz[session.QuestionIndex];

        await SafeSendAsync(
            chatId,
            $"❓ Вопрос {idx}/{total}:\n{dto.Text}",
            _kbd.BuildNextFinish(idx < total),
            ctx.CancellationToken);
    }

    private async Task FinishQuizAsync(UpdateContext ctx, ChatSession session)
    {
        var chatId = ctx.GetChatId();
        session.Mode = BotMode.None;
        session.QuestionsForQuiz = null;
        session.QuestionIndex = 0;
        _log.LogInformation("Пользователь {ChatId} завершил викторину", chatId);

        // Получаем информацию о запросах пользователя
        var requestInfo = await _aiLimitClient.GetRequestInfoAsync(chatId, ctx.CancellationToken);

        // Форматируем сообщение с информацией о запросах
        string requestInfoMessage = $"⚡Оставшиеся запросы: **{requestInfo.RemainingRequests}**\n" +
                                    $"⏰ **Время до сброса**: **{requestInfo.TimeToReset.Hours}ч {requestInfo.TimeToReset.Minutes}мин**";

        await SafeSendAsync(
            chatId,
            $"🔚 Тест завершён. Что дальше?\n{requestInfoMessage}",
            _kbd.Build(),
            ctx.CancellationToken);
    }

    private async Task<Stream> DownloadVoiceAsync(UpdateContext ctx, string fileId, CancellationToken ct)
    {
        var file = await ctx.BotClient.GetFile(fileId, cancellationToken: ct);
        var ms = new MemoryStream();
        await ctx.BotClient.DownloadFile(file.FilePath!, ms, cancellationToken: ct);
        ms.Position = 0;
        return ms;
    }

    #region SafeSendAsync

    private async Task SafeSendAsync(
        long chatId,
        string text,
        CancellationToken ct)
    {
        try
        {
            await _messageService.SendTextAsync(chatId, text, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Не удалось отправить сообщение пользователю {ChatId}: {Text}", chatId, text);
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
            await _messageService.SendTextAsync(
                chatId,
                text,
                replyMarkup: markup,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Не удалось отправить клавиатуру пользователю {ChatId}: {Text}", chatId, text);
        }
    }

    #endregion
}
