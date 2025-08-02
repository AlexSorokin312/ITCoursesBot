using Bot.Ports;
using ITCoursesBot.Interfaces;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types.ReplyMarkups;

public class BeginQuizMiddleware : IUpdateMiddleware
{
    private readonly IMessageService _messageService;
    private readonly IKeyboardBuilder _keyboardBuilder;
    private readonly IAiLimitClient _aiLimitClient;
    private readonly IQuestionsClient _questionsClient;
    private readonly ISessionManager _sessions;
    private readonly ILogger<BeginQuizMiddleware> _log;

    public BeginQuizMiddleware(
        IMessageService messageService,
        IKeyboardBuilder keyboardBuilder,
        IQuestionsClient questionsClient,
        ISessionManager sessions,
        ILogger<BeginQuizMiddleware> log,
        IAiLimitClient aiLimitClient)
    {
        _messageService = messageService;
        _keyboardBuilder = keyboardBuilder;
        _questionsClient = questionsClient;
        _sessions = sessions;
        _log = log;
        _aiLimitClient = aiLimitClient;
    }

    public async Task InvokeAsync(UpdateContext ctx, Func<Task> next)
    {
        var chatId = ctx.GetChatId();
        var session = _sessions.GetOrCreateSession(chatId);
        var cbData = ctx.Update.CallbackQuery?.Data;

        try
        {
            // 0) Нажата кнопка «В главное меню» (callback)
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


                await SafeSendAsync(
                    chatId,
                    $"🏠 Главное меню:\nВыберите, чем займёмся дальше!\n\n{requestInfoMessage}",
                    _keyboardBuilder.Build(),
                    ctx.CancellationToken);

                return;
            }

            // 1) Нажата кнопка «Начать викторину»
            if (cbData == KeyboardBuilder.BEGIN_QUIZ_BUTTON_NAME)
            {
                session.Mode = BotMode.BeginQuiz;
                _log.LogInformation("Пользователь {ChatId} вошёл в режим ввода номера урока", chatId);

                await SafeSendAsync(
                    chatId,
                    "Введите номер урока в формате: «КурсБлокУрок», например \"База21\"",
                    _keyboardBuilder.BuildBackToMenu(),
                    ctx.CancellationToken);
                return;
            }

            // 2) В режиме BeginQuiz ждём текстовый ввод номера урока
            if (session.Mode == BotMode.BeginQuiz)
            {
                var text = ctx.Update.Message?.Text?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    _log.LogDebug("Пустое сообщение от {ChatId} в режиме BeginQuiz — игнорируем", chatId);
                    return;
                }

                if (!CourseRefParser.TryParse(text, out var lessonRef))
                {
                    _log.LogWarning("Неверный формат урока от {ChatId}: «{Text}»", chatId, text);
                    await SafeSendAsync(
                        chatId,
                        "❗️ Неверный формат. Пожалуйста, введите в формате «Курс:Блок:Урок».",
                        _keyboardBuilder.BuildBackToMenu(),
                        ctx.CancellationToken);
                    return;
                }

                _log.LogInformation(
                    "Запрос вопросов для {ChatId}: курс={Course}, блок={Block}, урок={Lesson}",
                    chatId, lessonRef.Course, lessonRef.Block, lessonRef.Lesson);

                Question[] questions;
                try
                {
                    questions = (await _questionsClient
                        .GetLessonQuestionsAsync(lessonRef.Course, lessonRef.Block, lessonRef.Lesson))
                        .ToArray();
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Ошибка при загрузке вопросов для {ChatId}", chatId);
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
                    _log.LogInformation(
                        "Вопросы не найдены для {ChatId}: курс={Course}, блок={Block}, урок={Lesson}",
                        chatId, lessonRef.Course, lessonRef.Block, lessonRef.Lesson);
                    await SafeSendAsync(
                        chatId,
                        $"❗️ В курсе «{lessonRef.Course}» блок {lessonRef.Block}, урок {lessonRef.Lesson} вопросов не найдено.",
                        _keyboardBuilder.BuildBackToMenu(),
                        ctx.CancellationToken);
                    session.Mode = BotMode.None;
                    return;
                }

                // Инициализируем PassQuiz
                session.QuestionsForQuiz = questions
                    .Select(q => new Question { Id = q.Id, LessonId = q.LessonId, Text = q.Text })
                    .ToList();
                session.QuestionIndex = 0;
                session.Mode = BotMode.PassQuiz;

                _log.LogInformation("Загружено {Count} вопросов для {ChatId}", questions.Length, chatId);

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
            _log.LogError(ex, "Неожиданная ошибка в BeginQuizMiddleware для {ChatId}", chatId);
        }

        // 3) Передаём управление дальше
        try
        {
            await next();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Ошибка в последующих middleware после BeginQuizMiddleware для {ChatId}", chatId);
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
}
