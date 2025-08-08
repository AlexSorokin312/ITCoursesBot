using Bot.Ports;
using ITCoursesBot.Interfaces;
using Telegram.Bot.Types.ReplyMarkups;

public class ReworkMiddleware : IUpdateMiddleware
{
    private readonly IMessageService _msg;
    private readonly IKeyboardBuilder _kbd;
    private readonly ISessionManager _sessions;
    private readonly IProgressClient _progress;
    private readonly IAiLimitClient _aiLimit;

    public ReworkMiddleware(
        IMessageService msg,
        IKeyboardBuilder kbd,
        ISessionManager sessions,
        IProgressClient progress,
        IAiLimitClient aiLimit)
    {
        _msg = msg;
        _kbd = kbd;
        _sessions = sessions;
        _progress = progress;
        _aiLimit = aiLimit;
    }

    public async Task InvokeAsync(UpdateContext ctx, Func<Task> next)
    {
        var data = ctx.Update.CallbackQuery?.Data;
        if (data == KeyboardBuilder.REWORK_BUTTON_NAME)
        {
            var userId = ctx.Update.CallbackQuery.From.Id;

            LoggerService.LogInfo($"Пользователь {userId} запросил повторение неправильных вопросов");

            var badDtos = await _progress.GetIncorrectQuestionsAsync(userId, ctx.CancellationToken);
            if (badDtos.Count == 0)
            {
                LoggerService.LogInfo($"У пользователя {userId} нет вопросов с ошибками");

                var (remaining, reset) = await SafeGetRequestInfo(userId, ctx.CancellationToken);
                string requestInfoMessage =
                    $"⚡ Оставшиеся запросы: **{remaining}**\n" +
                    $"⏰ Время до сброса: **{reset.Hours}ч {reset.Minutes}мин**";

                await SafeSendAsync(
                    userId,
                    $"У вас нет вопросов с ошибками — нечего повторять! 🎉\nВыберите, чем займёмся дальше!\n\n{requestInfoMessage}",
                    _kbd.Build(),
                    ctx.CancellationToken);
                return;
            }

            var badQs = badDtos
                .Select(d => new Question { Id = d.Id, LessonId = d.LessonId, Text = d.Text })
                .ToList();

            var session = _sessions.GetOrCreateSession(userId);
            session.Mode = BotMode.PassQuiz;
            session.QuestionsForQuiz = badQs;
            session.QuestionIndex = 0;

            LoggerService.LogInfo($"Начало прохождения квиза для пользователя {userId}, вопросов: {badQs.Count}");

            await SafeSendAsync(
                userId,
                $"❓ Вопрос 1/{badQs.Count}:\n{badQs[0].Text}",
                _kbd.BuildBackToMenu(),
                ctx.CancellationToken);

            return;
        }

        await next();
    }

    private async Task<(int Remaining, TimeSpan TimeToReset)> SafeGetRequestInfo(long userId, CancellationToken ct)
    {
        try
        {
            var info = await _aiLimit.GetRequestInfoAsync(userId, ct);
            return (info.RemainingRequests, info.TimeToReset);
        }
        catch (Exception ex)
        {
            LoggerService.LogError(ex.ToString(), $"Не удалось получить информацию по квоте для пользователя {userId}");
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
            await _msg.SendTextAsync(chatId, text, replyMarkup: markup, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            LoggerService.LogError(ex.ToString(), $"Не удалось отправить сообщение в чат {chatId}: {text}");
        }
    }

    #endregion
}
