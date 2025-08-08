using Bot.Ports;
using ITCoursesBot.Interfaces;
using ITCoursesBot.ITCoursesBot.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

public class MockInterviewMiddleware : IUpdateMiddleware
{
    private readonly IMessageService _msg;
    private readonly IKeyboardBuilder _kbd;
    private readonly IProgressClient _progress;
    private readonly IAiLimitClient _aiLimit;
    private readonly IOpenAIClient _openAi;
    private readonly OpenAISettings _settings;
    private readonly ISessionManager _sessions;

    public MockInterviewMiddleware(
        IMessageService msg,
        IKeyboardBuilder kbd,
        IProgressClient progressClient,
        IAiLimitClient aiLimitClient,
        IOpenAIClient openAi,
        IOptions<OpenAISettings> opts,
        ISessionManager sessions)
    {
        _msg = msg;
        _kbd = kbd;
        _progress = progressClient;
        _aiLimit = aiLimitClient;
        _openAi = openAi;
        _settings = opts.Value;
        _sessions = sessions;
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
                LoggerService.LogInfo($"Пользователь {chatId} вернулся в главное меню");

                var info = await SafeGetRequestInfo(chatId, ctx.CancellationToken);
                await SafeSendAsync(
                    chatId,
                    $"🏠 Главное меню:\nВыберите опцию\n\n" +
                    $"⚡ Оставшиеся запросы: **{info.RemainingRequests}**\n" +
                    $"⏰ До сброса: **{info.TimeToReset.Hours}ч {info.TimeToReset.Minutes}мин**",
                    _kbd.Build(),
                    ctx.CancellationToken);
                return;
            }

            // 1) Старт «собеседования»
            if (data == KeyboardBuilder.MOCK_INTERVIEW_BUTTON_NAME)
            {
                session.Mode = BotMode.MockInterview;
                LoggerService.LogInfo($"Пользователь {chatId} начал собеседование");

                var okQs = await SafeCallAsync(() => _progress.GetCorrectQuestionsAsync(userId, ctx.CancellationToken), "GetCorrectQuestions");
                var badQs = await SafeCallAsync(() => _progress.GetIncorrectQuestionsAsync(userId, ctx.CancellationToken), "GetIncorrectQuestions");
                var allQs = okQs.Concat(badQs)
                                .Select(q => new Question { Id = q.Id, Text = q.Text })
                                .DistinctBy(q => q.Id)
                                .OrderBy(_ => Random.Shared.Next())
                                .ToList();

                if (!allQs.Any())
                {
                    await SafeSendAsync(chatId, "❗️ Нет отвеченных вопросов — нечему тренироваться!", _kbd.BuildBackToMenu(), ctx.CancellationToken);
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

            // 2) В режиме MockInterview
            if (session.Mode == BotMode.MockInterview)
            {
                if (!string.IsNullOrEmpty(data))
                {
                    if (data == KeyboardBuilder.NEXT_QUESTION_BUTTON_NAME)
                    {
                        LoggerService.LogInfo($"Пользователь {chatId} нажал «Следующий вопрос»");
                        await SendNextAsync(ctx, session);
                        return;
                    }
                    if (data == KeyboardBuilder.FINISH_BUTTON_NAME)
                    {
                        LoggerService.LogInfo($"Пользователь {chatId} прервал собеседование");
                        await FinishAsync(ctx, session);
                        return;
                    }
                }

                var msg = ctx.Update.Message;
                if (msg != null)
                {
                    var answer = !string.IsNullOrWhiteSpace(msg.Text)
                        ? msg.Text.Trim()
                        : await SafeTranscribeAsync(ctx, msg);

                    if (!string.IsNullOrWhiteSpace(answer))
                    {
                        LoggerService.LogInfo($"Пользователь {chatId} ответил на вопрос {session.QuestionIndex + 1}");
                        await ProcessAnswerAsync(ctx, session, answer);
                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Неожиданная ошибка в MockInterviewMiddleware для {chatId}: {ex.Message}");
        }

        await next();
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
            LoggerService.LogError($"Ошибка получения квоты для {chatId}: {ex.Message}");
            return (0, TimeSpan.Zero);
        }
    }

    private async Task<T> SafeCallAsync<T>(Func<Task<T>> func, string op)
    {
        try
        {
            return await func();
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Ошибка операции {op}: {ex.Message}");
            return Activator.CreateInstance<T>();
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
            LoggerService.LogError($"Ошибка транскрипции голоса: {ex.Message}");
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
                $"Вопрос: {question.Text}\nОтвет: {answer}");
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Ошибка запроса к OpenAI: {ex.Message}");
        }

        var (ok, comment) = raw != null
            ? ParseAiResponse(raw)
            : (false, "⚠️ Не удалось получить оценку ИИ.");

        try
        {
            await _progress.AddAnswerAsync(
                new AddAnswerDto(ctx.GetUserId(), question.Id, answer, ok),
                ctx.CancellationToken);
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Ошибка сохранения ответа: {ex.Message}");
        }

        session.MockInterviewResults.Add(new InterviewEntry
        {
            QuestionText = question.Text,
            AnswerText = answer,
            IsCorrect = ok
        });

        await SafeSendAsync(chatId, comment, ctx.CancellationToken);
        await SendNextAsync(ctx, session);
    }

    private (bool, string) ParseAiResponse(string raw)
    {
        const string T = "(TRUE_ANSWER)";
        const string F = "(FALSE_ANSWER)";
        bool correct = raw.Contains(T, StringComparison.OrdinalIgnoreCase);
        var cleaned = raw.Replace(T, "", StringComparison.OrdinalIgnoreCase)
                          .Replace(F, "", StringComparison.OrdinalIgnoreCase)
                          .Trim();
        return (correct, cleaned);
    }

    private async Task SendNextAsync(UpdateContext ctx, ChatSession session)
    {
        session.QuestionIndex++;
        var list = session.MockQuestions!;
        var chatId = ctx.GetChatId();

        if (session.QuestionIndex >= list.Count)
        {
            var total = session.MockInterviewResults.Count;
            var correct = session.MockInterviewResults.Count(e => e.IsCorrect);
            var sb = new StringBuilder()
                .AppendLine("🔔 Собеседование завершено!")
                .AppendLine($"Всего: {total}, правильных: {correct}")
                .AppendLine();

            try
            {
                var verdict = await _openAi.GetChatResponseAsync(
                    _settings.InstructionsMockInterview,
                    sb.ToString());
                sb.AppendLine("💬 Итог от ИИ:").AppendLine(verdict);
            }
            catch (Exception ex)
            {
                LoggerService.LogError($"Не удалось получить вердикт AI: {ex.Message}");
            }

            await SafeSendAsync(chatId, sb.ToString(), _kbd.Build(), ctx.CancellationToken);

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
        LoggerService.LogInfo($"Пользователь {chatId} прервал собеседование");

        await SafeSendAsync(
            chatId,
            "🔚 Собеседование прервано. Выберите опцию:",
            _kbd.Build(),
            ctx.CancellationToken);
    }

    private async Task<Stream> DownloadVoiceAsync(UpdateContext ctx, string fileId, CancellationToken ct)
    {
        var file = await ctx.BotClient.GetFile(fileId, cancellationToken: ct);
        using var ms = new MemoryStream();
        await ctx.BotClient.DownloadFile(file.FilePath!, ms, cancellationToken: ct);
        ms.Position = 0;
        return ms;
    }

    #region SafeSendAsync

    private async Task SafeSendAsync(long chatId, string text, CancellationToken ct)
    {
        try
        {
            await _msg.SendTextAsync(chatId, text, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Не удалось отправить сообщение: {ex.Message}");
        }
    }

    private async Task SafeSendAsync(long chatId, string text, InlineKeyboardMarkup markup, CancellationToken ct)
    {
        try
        {
            await _msg.SendTextAsync(chatId, text, replyMarkup: markup, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Не удалось отправить клавиатуру: {ex.Message}");
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
