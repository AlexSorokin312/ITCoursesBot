using Bot.Ports;
using ITCoursesBot.Interfaces;
using ITCoursesBot.ITCoursesBot.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

public class MockInterviewMiddleware : IUpdateMiddleware
{
    private readonly IMessageService _msg;
    private readonly IKeyboardBuilder _kbd;
    private readonly IProgressClient _progress;
    private readonly IOpenAIClient _openAi;
    private readonly OpenAISettings _settings;
    private readonly ISessionManager _sessions;
    private readonly ILogger<MockInterviewMiddleware> _log;
    private readonly IAiLimitClient _aiLimit;

    public MockInterviewMiddleware(
        IMessageService msg,
        IKeyboardBuilder kbd,
        IProgressClient progressClient,
        IAiLimitClient aiLimitClient,
        IOpenAIClient openAi,
        IOptions<OpenAISettings> opts,
        ISessionManager sessions,
        ILogger<MockInterviewMiddleware> log,
        IAiLimitClient aiLimit)
    {
        _msg = msg;
        _kbd = kbd;
        _progress = progressClient;
        _openAi = openAi;
        _settings = opts.Value;
        _sessions = sessions;
        _log = log;
        _aiLimit = aiLimit;
    }

    public async Task InvokeAsync(UpdateContext ctx, Func<Task> next)
    {
        var chatId = ctx.GetChatId();
        var from = ctx.Update.CallbackQuery?.From ?? ctx.Update.Message?.From;
        if (from == null) { await next(); return; }

        var userId = from.Id;
        var session = _sessions.GetOrCreateSession(chatId);
        var data = ctx.Update.CallbackQuery?.Data;

        try
        {
            // 0) Кнопка «В главное меню»
            if (data == KeyboardBuilder.BACK_TO_MENU_BUTTON_NAME)
            {
                session.Mode = BotMode.None;
                session.MockQuestions = null;
                session.QuestionIndex = 0;
                session.MockInterviewResults.Clear();
                _log.LogInformation("Пользователь {ChatId} вернулся в главное меню", chatId);

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

            // 1) Старт «собеседования»
            if (data == KeyboardBuilder.MOCK_INTERVIEW_BUTTON_NAME)
            {
                session.Mode = BotMode.MockInterview;
                _log.LogInformation("Пользователь {ChatId} начал собеседование", chatId);

                // собрать вопросы пользователя
                var okQs = await SafeCallAsync(() => _progress.GetCorrectQuestionsAsync(userId, ctx.CancellationToken), "GetCorrectQuestions");
                var badQs = await SafeCallAsync(() => _progress.GetIncorrectQuestionsAsync(userId, ctx.CancellationToken), "GetIncorrectQuestions");
                var allQs = okQs.Concat(badQs)
                                   .Select(q => new Question { Id = q.Id, Text = q.Text })
                                   .DistinctBy(q => q.Id)
                                   .ToList();

                // Перемешиваем вопросы
                var rnd = Random.Shared;
                allQs = allQs
                       .OrderBy(_ => rnd.Next())
                       .ToList();

                if (allQs.Count == 0)
                {
                    await SafeSendAsync(
                        chatId,
                        "❗️ Нет отвеченных вопросов — нечему тренироваться!",
                        _kbd.BuildBackToMenu(),
                        ctx.CancellationToken);
                    session.Mode = BotMode.None;
                    return;
                }

                session.MockQuestions = allQs;
                session.QuestionIndex = 0;
                session.MockInterviewResults.Clear();

                await SafeSendAsync(
                    chatId,
                    $"🎯 Собеседование: вопрос 1/{allQs.Count}\n{allQs[0].Text}",
                    _kbd.BuildNextFinish(allQs.Count > 1),
                    ctx.CancellationToken);
                return;
            }

            // 2) В режиме MockInterview: кнопки или ответы
            if (session.Mode == BotMode.MockInterview)
            {
                if (!string.IsNullOrEmpty(data))
                {
                    switch (data)
                    {
                        case KeyboardBuilder.NEXT_QUESTION_BUTTON_NAME:
                            _log.LogInformation("Пользователь {ChatId} нажал «Следующий вопрос»", chatId);
                            await SendNextAsync(ctx, session);
                            return;
                        case KeyboardBuilder.FINISH_BUTTON_NAME:
                            _log.LogInformation("Пользователь {ChatId} прервал собеседование", chatId);
                            await FinishAsync(ctx, session);
                            return;
                    }
                }

                var msg = ctx.Update.Message;
                if (msg != null)
                {
                    string answer = !string.IsNullOrWhiteSpace(msg.Text)
                        ? msg.Text.Trim()
                        : await SafeTranscribeAsync(ctx, msg);

                    if (!string.IsNullOrWhiteSpace(answer))
                    {
                        _log.LogInformation("Пользователь {ChatId} дал ответ на вопрос {Index}", chatId, session.QuestionIndex + 1);
                        await ProcessAnswerAsync(ctx, session, answer);
                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Неожиданная ошибка в MockInterviewMiddleware для {ChatId}", chatId);
        }

        await next();
    }

    private async Task<T> SafeCallAsync<T>(Func<Task<T>> func, string operation)
    {
        try
        {
            return await func();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Ошибка операции {Op}", operation);
            return (T)Activator.CreateInstance(typeof(T))!;
        }
    }

    private async Task<string?> SafeTranscribeAsync(UpdateContext ctx, Telegram.Bot.Types.Message msg)
    {
        try
        {
            if (msg.Voice == null) return null;
            await using var audio = await DownloadVoiceAsync(ctx, msg.Voice.FileId, ctx.CancellationToken);
            return await _openAi.TranscribeAudioAsync(audio, $"{msg.Voice.FileUniqueId}.ogg");
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
        var question = session.MockQuestions![session.QuestionIndex];

        string raw = null;
        try
        {
            raw = await _openAi.GetChatResponseAsync(
                _settings.InstructionsInterview,
                $"Вопрос: {question.Text}\nОтвет студента: {answer}");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Ошибка запроса к OpenAI для собеседования у {ChatId}", chatId);
        }

        var (isCorrect, comment) = raw != null
            ? ParseAiResponse(raw)
            : (false, "⚠️ Не удалось получить оценку от ИИ.");

        try
        {
            await _progress.AddAnswerAsync(
                new AddAnswerDto(ctx.GetUserId(), question.Id, answer, isCorrect),
                ctx.CancellationToken);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Ошибка сохранения ответа пользователя {ChatId}", chatId);
        }

        session.MockInterviewResults.Add(new InterviewEntry
        {
            QuestionText = question.Text,
            AnswerText = answer,
            IsCorrect = isCorrect
        });

        await SafeSendAsync(chatId, comment, ctx.CancellationToken);
        await SendNextAsync(ctx, session);
    }

    private (bool, string) ParseAiResponse(string raw)
    {
        const string TrueTag = "(TRUE_ANSWER)";
        const string FalseTag = "(FALSE_ANSWER)";
        bool ok = raw.Contains(TrueTag, StringComparison.OrdinalIgnoreCase);
        var cleaned = raw
            .Replace(TrueTag, "", StringComparison.OrdinalIgnoreCase)
            .Replace(FalseTag, "", StringComparison.OrdinalIgnoreCase)
            .Trim();
        return (ok, cleaned);
    }

    private async Task SendNextAsync(UpdateContext ctx, ChatSession session)
    {
        session.QuestionIndex++;
        var list = session.MockQuestions!;
        var chatId = ctx.GetChatId();

        if (session.QuestionIndex >= list.Count)
        {
            // собирать итоговый отчёт…
            var total = session.MockInterviewResults.Count;
            var correct = session.MockInterviewResults.Count(e => e.IsCorrect);
            var sb = new StringBuilder()
                .AppendLine("🔔 Собеседование завершено!")
                .AppendLine($"Всего вопросов: {total}, правильных: {correct}")
                .AppendLine();

            // вердикт ИИ
            try
            {
                var verdict = await _openAi.GetChatResponseAsync(
                    _settings.InstructionsMockInterview,
                    sb.ToString());
                sb.AppendLine("💬 Комментарий ИИ:").AppendLine(verdict);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Не удалось получить вердикт AI");
            }

            await SafeSendAsync(
                chatId,
                sb.ToString(),
                _kbd.Build(),
                ctx.CancellationToken);

            session.Mode = BotMode.None;
            session.MockQuestions = null;
            session.QuestionIndex = 0;
            session.MockInterviewResults.Clear();
        }
        else
        {
            var idx = session.QuestionIndex + 1;
            var text = list[session.QuestionIndex].Text;
            await SafeSendAsync(
                chatId,
                $"🎯 Вопрос {idx}/{list.Count}:\n{text}",
                _kbd.BuildNextFinish(idx < list.Count),
                ctx.CancellationToken);
        }
    }

    private async Task FinishAsync(UpdateContext ctx, ChatSession session)
    {
        var chatId = ctx.GetChatId();
        session.Mode = BotMode.None;
        session.MockQuestions = null;
        session.QuestionIndex = 0;
        session.MockInterviewResults.Clear();
        _log.LogInformation("Пользователь {ChatId} прервал собеседование", chatId);

        await SafeSendAsync(
            chatId,
            "🔚 Собеседование прервано. Выберите опцию:",
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
            await _msg.SendTextAsync(
                chatId,
                text,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Не удалось отправить сообщение чату {ChatId}: {Text}", chatId, text);
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
            _log.LogError(ex, "Не удалось отправить клавиатуру чату {ChatId}: {Text}", chatId, text);
        }
    }

    #endregion
}

public class InterviewEntry
{
    public string QuestionText { get; set; }
    public string AnswerText { get; set; }
    public bool IsCorrect { get; set; }
}
