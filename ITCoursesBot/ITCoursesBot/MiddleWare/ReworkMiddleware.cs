using Bot.Ports;
using ITCoursesBot.Interfaces;
using Telegram.Bot.Types;

public class ReworkMiddleware : IUpdateMiddleware
{
    private readonly IMessageService _msg;
    private readonly IKeyboardBuilder _kbd;
    private readonly ISessionManager _sessions;
    private readonly IProgressClient _progress;
    private IAiLimitClient _aiLimit;

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

            var badDtos = await _progress.GetIncorrectQuestionsAsync(userId, ctx.CancellationToken);
            if (badDtos.Count == 0)
            {
                // Получаем информацию о запросах пользователя
                var requestInfo = await _aiLimit.GetRequestInfoAsync(userId, ctx.CancellationToken);

                // Форматируем сообщение с информацией о запросах
                string requestInfoMessage = $"⚡Оставшиеся запросы: **{requestInfo.RemainingRequests}**\n" +
                                            $"⏰ **Время до сброса**: **{requestInfo.TimeToReset.Hours}ч {requestInfo.TimeToReset.Minutes}мин**";


                await _msg.SendTextAsync(userId,
                    $"У вас нет вопросов с ошибками — нечего повторять! 🎉. \nВыберите, чем займёмся дальше!\n\n{requestInfoMessage}\"",
                    replyMarkup: _kbd.Build(),
                    cancellationToken: ctx.CancellationToken);
                return;
            }

            var badQs = badDtos
                .Select(d => new Question { Id = d.Id, LessonId = d.LessonId, Text = d.Text })
                .ToList();

            var session = _sessions.GetOrCreateSession(userId);
            session.Mode = BotMode.PassQuiz;
            session.QuestionsForQuiz = badQs;
            session.QuestionIndex = 0;

            await _msg.SendTextAsync(userId,
                $"❓ Вопрос 1/{badQs.Count}:\n{badQs[0].Text}",
                replyMarkup: _kbd.BuildBackToMenu(),
                cancellationToken: ctx.CancellationToken);
            return;
        }

        await next();
    }
}
