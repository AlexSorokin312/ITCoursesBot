using Bot.Ports;
using ITCoursesBot.Interfaces;
using ITCoursesBot.ITCoursesBot.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

    public PassQuizMiddleware(
        IMessageService msg,
        IKeyboardBuilder kbd,
        IProgressClient progressClient,
        IAiLimitClient aiLimitClient,
        IOpenAIClient openAi,
        IOptions<OpenAISettings> settings,
        ISessionManager sessions)
    {
        _messageService = msg;
        _kbd = kbd;
        _progress = progressClient;
        _aiLimitClient = aiLimitClient;
        _openAi = openAi;
        _settings = settings.Value;
        _sessions = sessions;
    }

    public async Task InvokeAsync(UpdateContext ctx, Func<Task> next)
    {
        var chatId = ctx.GetChatId();
        var session = _sessions.GetOrCreateSession(chatId);
        var cbData = ctx.Update.CallbackQuery?.Data;

        try
        {
            if (session.Mode != BotMode.PassQuiz)
            {
                await next();
                return;
            }

            // кнопка «В главное меню»
            if (cbData == KeyboardBuilder.BACK_TO_MENU_BUTTON_NAME)
            {
                session.Mode = BotMode.None;
                session.QuestionsForQuiz = null;
                session.QuestionIndex = 0;
                LoggerService.LogInfo($"Пользователь {chatId} вернулся в главное меню");

                var info = await SafeGetRequestInfo(chatId, ctx.CancellationToken);
                await SafeSendAsync(
                    chatId,
                    $"🏠 Главное меню:\nВыберите, чем займёмся дальше!\n\n" +
                    $"⚡ Оставшиеся запросы: **{info.RemainingRequests}**\n" +
                    $"⏰ До сброса: **{info.TimeToReset.Hours}ч {info.TimeToReset.Minutes}мин**",
                    _kbd.Build(),
                    ctx.CancellationToken);
                return;
            }

            // inline-кнопки
            if (!string.IsNullOrEmpty(cbData))
            {
                switch (cbData)
                {
                    case KeyboardBuilder.NEXT_QUESTION_BUTTON_NAME:
                        LoggerService.LogInfo($"Пользователь {chatId} нажал «Следующий вопрос»");
                        await SendNextQuestionAsync(ctx, session);
                        return;
                    case KeyboardBuilder.FINISH_BUTTON_NAME:
                        LoggerService.LogInfo($"Пользователь {chatId} нажал «Завершить»");
                        await FinishQuizAsync(ctx, session);
                        return;
                    case KeyboardBuilder.SHOW_ALL_BUTTON_NAME:
                        LoggerService.LogInfo($"Пользователь {chatId} нажал «Показать все вопросы»");
                        await ShowAllQuestionsAsync(ctx, session);
                        return;
                }
            }

            // ответ текстом или голосом
            var msg = ctx.Update.Message;
            if (msg == null) return;

            var answer = !string.IsNullOrWhiteSpace(msg.Text)
                ? msg.Text.Trim()
                : await SafeTranscribeAsync(ctx, msg);

            if (string.IsNullOrWhiteSpace(answer)) return;

            // проверка квоты
            bool limitReached = await SafeCheckLimit(chatId, ctx.CancellationToken);
            if (limitReached)
            {
                await SafeSendAsync(
                    chatId,
                    "❗️ Квота запросов к ИИ исчерпана. Попробуйте позже.",
                    ctx.CancellationToken);
                return;
            }

            if (session.QuestionsForQuiz == null || session.QuestionsForQuiz.Count == 0)
            {
                LoggerService.LogWarning($"Нет вопросов в сессии для {chatId}");
                await SafeSendAsync(
                    chatId,
                    "❗️ Вопросы не загружены. Введите /beginquiz для начала теста.",
                    ctx.CancellationToken);
                return;
            }

            await ProcessAnswerAsync(ctx, session, answer);
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Неожиданная ошибка в PassQuizMiddleware для {chatId}: {ex}");
        }

        try
        {
            await next();
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Ошибка downstream после PassQuizMiddleware для {chatId}: {ex}");
        }
    }

    private async Task<(int RemainingRequests, TimeSpan TimeToReset)> SafeGetRequestInfo(long chatId, CancellationToken ct)
    {
        try
        {
            var info = await _aiLimitClient.GetRequestInfoAsync(chatId, ct);
            return (info.RemainingRequests, info.TimeToReset);
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Ошибка получения квоты для {chatId}: {ex}");
            return (0, TimeSpan.Zero);
        }
    }

    private async Task<bool> SafeCheckLimit(long chatId, CancellationToken ct)
    {
        try
        {
            return await _aiLimitClient.IsLimitReachedAsync(chatId, ct);
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Ошибка проверки квоты ИИ для {chatId}: {ex}");
            return true;
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
            LoggerService.LogError($"Ошибка транскрипции голоса для {ctx.GetChatId()}: {ex}");
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
            LoggerService.LogError($"Ошибка запроса к OpenAI для {chatId}: {ex}");
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
            LoggerService.LogError($"Ошибка сохранения ответа пользователя {chatId}: {ex}");
        }

        await SafeSendAsync(chatId, comment, ctx.CancellationToken);

        if (isCorrect)
            await SendCorrectOrNextAsync(ctx, session);
        else
            await SafeSendAsync(chatId, "❌ Неправильно. Попробуйте ещё раз.", ctx.CancellationToken);
    }

    private (bool, string) ParseAiResponse(string aiReply)
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
            LoggerService.LogInfo($"Пользователь {chatId} прошёл все {total} вопросов");
            var info = await SafeGetRequestInfo(chatId, ctx.CancellationToken);

            await SafeSendAsync(
                chatId,
                $"✅ Все вопросы пройдены!\n\n" +
                $"⚡ Оставшиеся запросы: **{info.RemainingRequests}**\n" +
                $"⏰ До сброса: **{info.TimeToReset.Hours}ч {info.TimeToReset.Minutes}мин**",
                _kbd.Build(),
                ctx.CancellationToken);

            session.Mode = BotMode.None;
            session.QuestionsForQuiz?.Clear();
            session.QuestionIndex = 0;
        }
        else
        {
            await SafeSendAsync(chatId, "✅ Правильно! Перейти к следующему?", _kbd.BuildNextFinish(true), ctx.CancellationToken);
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
        LoggerService.LogInfo($"Пользователь {chatId} завершил викторину");

        var info = await SafeGetRequestInfo(chatId, ctx.CancellationToken);
        await SafeSendAsync(
            chatId,
            $"🔚 Тест завершён.\n\n" +
            $"⚡ Оставшиеся запросы: **{info.RemainingRequests}**\n" +
            $"⏰ До сброса: **{info.TimeToReset.Hours}ч {info.TimeToReset.Minutes}мин**",
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
            LoggerService.LogError($"Не удалось отправить сообщение: {ex}");
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
            await _messageService.SendTextAsync(chatId, text, replyMarkup: markup, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Не удалось отправить клавиатуру: {ex}");
        }
    }

    #endregion
}
