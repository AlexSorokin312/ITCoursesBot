using ITCoursesBot.Interfaces;

public class DefaultMiddleware : IUpdateMiddleware
{
    private readonly IMessageService _msg;
    private readonly IKeyboardBuilder _kbd;
    private readonly ISessionManager _sessions;

    public DefaultMiddleware(IMessageService msg, IKeyboardBuilder kbd, ISessionManager sessions)
    {
        _msg = msg;
        _kbd = kbd;
        _sessions = sessions;
    }

    public async Task InvokeAsync(UpdateContext ctx, Func<Task> next)
    {
        var session = _sessions.GetOrCreateSession(ctx.Update.Id);
        if (session.Mode == BotMode.None)
        {
            await _msg.SendTextAsync(
                ctx.Update.Id,
                "Выберите опцию работы с чатом:",
                replyMarkup: _kbd.Build(),
                cancellationToken: ctx.CancellationToken);
            return;
        }

        await next();
    }
}
