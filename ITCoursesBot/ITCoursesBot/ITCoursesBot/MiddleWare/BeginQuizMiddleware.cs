using Bot.Ports;
using ITCoursesBot.Interfaces;
using Telegram.Bot.Types.ReplyMarkups;

public class BeginQuizMiddleware : IUpdateMiddleware
{
    private readonly IMessageService _messageService;
    private readonly IKeyboardBuilder _keyboardBuilder;
    private readonly IAiLimitClient _aiLimitClient;
    private readonly IQuestionsClient _questionsClient;
    private readonly ISessionManager _sessions;

    public BeginQuizMiddleware(
        IMessageService messageService,
        IKeyboardBuilder keyboardBuilder,
        IQuestionsClient questionsClient,
        ISessionManager sessions,
        IAiLimitClient aiLimitClient)
    {
        _messageService = messageService;
        _keyboardBuilder = keyboardBuilder;
        _questionsClient = questionsClient;
        _sessions = sessions;
        _aiLimitClient = aiLimitClient;
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
                session.QuestionsForQuiz = null;
                session.QuestionIndex = 0;
                LoggerService.LogInfo($"Пользователь {chatId} вернулся в главное меню");

                var info = await SafeGetRequestInfo(chatId, ctx.CancellationToken);
                await SafeSendAsync(
                    chatId,
                    $"🏠 Главное меню:\nВыберите, чем займёмся дальше!\n\n" +
                    $"⚡ Оставшиеся запросы: **{info.RemainingRequests}**\n" +
                    $"⏰ Время до сброса: **{info.TimeToReset.Hours}ч {info.TimeToReset.Minutes}мин**",
                    _keyboardBuilder.Build(),
                    ctx.CancellationToken);
                return;
            }

            // 1) «Начать викторину»
            if (cbData == KeyboardBuilder.BEGIN_QUIZ_BUTTON_NAME)
            {
                session.Mode = BotMode.BeginQuiz;
                LoggerService.LogInfo($"Пользователь {chatId} вошёл в режим ввода номера урока");

                await SafeSendAsync(
                    chatId,
                    "Введите номер урока в формате: «КурсБлокУрок», например **\"База21\"**",
                    _keyboardBuilder.BuildBackToMenu(),
                    ctx.CancellationToken);
                return;
            }

            // 2) В режиме BeginQuiz
            if (session.Mode == BotMode.BeginQuiz)
            {
                var text = ctx.Update.Message?.Text?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    LoggerService.LogDebug($"Пустое сообщение от {chatId} в режиме BeginQuiz — игнорируем");
                    return;
                }

                if (!CourseRefParser.TryParse(text, out var lessonRef))
                {
                    LoggerService.LogWarning($"Неверный формат урока от {chatId}: «{text}»");
                    await SafeSendAsync(
                        chatId,
                        "❗️ Неверный формат. Введите в формате «КурсБлокУрок». Например, \b\"База21\"\b.",
                        _keyboardBuilder.BuildBackToMenu(),
                        ctx.CancellationToken);
                    return;
                }

                LoggerService.LogInfo($"Запрос вопросов для {chatId}: курс={lessonRef.Course}, блок={lessonRef.Block}, урок={lessonRef.Lesson}");

                Question[] questions;
                try
                {
                    questions = (await _questionsClient
                        .GetLessonQuestionsAsync(lessonRef.Course, lessonRef.Block, lessonRef.Lesson))
                        .ToArray();
                }
                catch (Exception ex)
                {
                    LoggerService.LogError($"Ошибка при загрузке вопросов для {chatId}: {ex.Message}");
                    await SafeSendAsync(
                        chatId,
                        "❌ Не удалось загрузить вопросы. Попробуйте позже.",
                        _keyboardBuilder.BuildBackToMenu(),
                        ctx.CancellationToken);
                    session.Mode = BotMode.None;
                    return;
                }

                if (questions.Length == 0)
                {
                    LoggerService.LogInfo($"Вопросы не найдены для {chatId}: курс={lessonRef.Course}, блок={lessonRef.Block}, урок={lessonRef.Lesson}");
                    await SafeSendAsync(
                        chatId,
                        $"❗️ В курсе «{lessonRef.Course}» блок {lessonRef.Block}, урок {lessonRef.Lesson} вопросов не найдено.",
                        _keyboardBuilder.BuildBackToMenu(),
                        ctx.CancellationToken);
                    session.Mode = BotMode.None;
                    return;
                }

                session.QuestionsForQuiz = questions
                    .Select(q => new Question { Id = q.Id, LessonId = q.LessonId, Text = q.Text })
                    .ToList();
                session.QuestionIndex = 0;
                session.Mode = BotMode.PassQuiz;

                LoggerService.LogInfo($"Загружено {questions.Length} вопросов для {chatId}");

                await SafeSendAsync(
                    chatId,
                    $"❓ Вопрос 1/{questions.Length}:\n{questions[0].Text}",
                    _keyboardBuilder.BuildBackToMenu(),
                    ctx.CancellationToken);
                return;
            }
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Неожиданная ошибка в BeginQuizMiddleware для {chatId}: {ex.Message}");
        }

        // Передаём дальше
        try
        {
            await next();
        }
        catch (Exception ex)
        {
            LoggerService.LogError($"Ошибка downstream после BeginQuizMiddleware для {chatId}: {ex.Message}");
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
            await _messageService.SendTextAsync(
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
}
