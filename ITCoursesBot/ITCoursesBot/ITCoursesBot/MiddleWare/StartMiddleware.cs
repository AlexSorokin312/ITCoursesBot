using Bot.Ports;
using ITCoursesBot.Interfaces;

public class StartMiddleware : IUpdateMiddleware
{
    private readonly IUserClient _usersClient;
    private readonly IMessageService _msg;
    private readonly ISessionManager _sessions;
    private readonly IKeyboardBuilder _kbd;
    private readonly IAiLimitClient _aiLimitClient;

    public StartMiddleware(
        IUserClient usersClient,
        IMessageService msg,
        ISessionManager sessions,
        IKeyboardBuilder kbd,
        IAiLimitClient aiLimitClient)
    {
        _usersClient = usersClient;
        _msg = msg;
        _sessions = sessions;
        _kbd = kbd;
        _aiLimitClient = aiLimitClient;
    }

    public async Task InvokeAsync(UpdateContext ctx, Func<Task> next)
    {
        var message = ctx.Update.Message;
        var text = message?.Text?.Trim();

        if (string.Equals(text, "/start", StringComparison.OrdinalIgnoreCase))
        {
            var chatId = message.Chat.Id;
            var userId = message.From?.Id ?? 0;

            LoggerService.LogInfo($"Получена команда /start от пользователя {userId}");

            UserDto user;
            try
            {
                user = await _usersClient.GetOrCreateAsync(
                    new CreateUserDto(userId, message.From?.Username ?? message.From?.FirstName ?? "Unknown"),
                    ctx.CancellationToken);
                LoggerService.LogInfo($"Пользователь {userId} зарегистрирован/получен: Name={user.Name}");
            }
            catch (Exception ex)
            {
                LoggerService.LogError(ex.ToString(), $"Ошибка при регистрации пользователя {userId}");
                try
                {
                    await _msg.SendTextAsync(
                        chatId,
                        "❌ Не удалось зарегистрировать пользователя. Попробуйте позже.",
                        cancellationToken: ctx.CancellationToken);
                }
                catch (Exception sendEx)
                {
                    LoggerService.LogError(sendEx.ToString(), $"Не удалось отправить сообщение об ошибке регистрации пользователю {userId}");
                }
                return;
            }

            try
            {
                var info = await _aiLimitClient.GetRequestInfoAsync(chatId, ctx.CancellationToken);
                string requestInfoMessage =
                    $"⚡ Оставшиеся запросы: **{info.RemainingRequests}**\n" +
                    $"⏰ Время до сброса: **{info.TimeToReset.Hours}ч {info.TimeToReset.Minutes}мин**";

                await _msg.SendTextAsync(
                    chatId,
                    $"👋 Привет, {user.Name}! Выберите опцию работы с чатом:\n\n{requestInfoMessage}",
                    replyMarkup: _kbd.Build(),
                    cancellationToken: ctx.CancellationToken);

                LoggerService.LogInfo($"Отправлено приветственное сообщение пользователю {userId}");
            }
            catch (Exception ex)
            {
                LoggerService.LogError(ex.ToString(), $"Не удалось отправить приветственное сообщение пользователю {userId}");
            }

            _sessions.GetOrCreateSession(chatId);
            LoggerService.LogDebug($"Сессия для пользователя {userId} установлена/обновлена");

            return;
        }

        try
        {
            await next();
        }
        catch (Exception ex)
        {
            LoggerService.LogError(ex.ToString(), "Ошибка в конвейере middleware после StartMiddleware");
        }
    }
}
