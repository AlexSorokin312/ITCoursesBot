using Bot.Ports;
using ITCoursesBot.Interfaces;
using Microsoft.Extensions.Logging;

public class StartMiddleware : IUpdateMiddleware
{
    private readonly IUserClient _usersClient;
    private readonly IMessageService _msg;
    private readonly ISessionManager _sessions;
    private readonly IKeyboardBuilder _kbd;
    private readonly ILogger<StartMiddleware> _log;
    private readonly IAiLimitClient _aiLimitClient;

    public StartMiddleware(
        IUserClient usersClient,
        IMessageService msg,
        ISessionManager sessions,
        IKeyboardBuilder kbd,
        ILogger<StartMiddleware> log,
        IAiLimitClient aiLimitClient)
    {
        _usersClient = usersClient;
        _msg = msg;
        _sessions = sessions;
        _kbd = kbd;
        _log = log;
        _aiLimitClient = aiLimitClient;
    }

    public async Task InvokeAsync(UpdateContext ctx, Func<Task> next)
    {
        var update = ctx.Update;
        var message = update.Message;
        var text = message?.Text?.Trim();

        if (string.Equals(text, "/start", StringComparison.OrdinalIgnoreCase))
        {
            var chatId = message.Chat.Id;
            var userId = message.From?.Id ?? 0;

            _log.LogInformation("Получена команда /start от пользователя {UserId}", userId);

            UserDto user;
            try
            {
                user = await _usersClient.GetOrCreateAsync(
                    new CreateUserDto(userId, message.From?.Username ?? message.From?.FirstName ?? "Unknown"),
                    ctx.CancellationToken);
                _log.LogInformation("Пользователь {UserId} зарегистрирован/получен: Name={UserName}", userId, user.Name);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Ошибка при регистрации пользователя {UserId}", userId);
                try
                {
                    await _msg.SendTextAsync(
                        chatId,
                        "❌ Не удалось зарегистрировать пользователя. Попробуйте позже.",
                        cancellationToken: ctx.CancellationToken);
                }
                catch (Exception sendEx)
                {
                    _log.LogError(sendEx, "Не удалось отправить сообщение об ошибке регистрации пользователю {UserId}", userId);
                }
                return;
            }

            try
            {
                // Получаем информацию о запросах пользователя
                var requestInfo = await _aiLimitClient.GetRequestInfoAsync(chatId, ctx.CancellationToken);

                // Форматируем сообщение с информацией о запросах
                string requestInfoMessage = $"Оставшиеся запросы: {requestInfo.RemainingRequests}\n" +
                                            $"Время до сброса: {requestInfo.TimeToReset.Hours}ч {requestInfo.TimeToReset.Minutes}мин";

                // Отправляем приветственное сообщение с информацией о запросах
                await _msg.SendTextAsync(
                    chatId,
                    $"👋 Привет, {user.Name}! Выберите опцию работы с чатом:\n\n{requestInfoMessage}",
                    replyMarkup: _kbd.Build(),
                    cancellationToken: ctx.CancellationToken);

                _log.LogInformation("Отправлено приветственное сообщение пользователю {UserId}", userId);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Не удалось отправить приветствие пользователю {UserId}", userId);
            }

            _sessions.GetOrCreateSession(chatId);
            _log.LogDebug("Сессия для пользователя {UserId} установлена/обновлена", userId);

            return;
        }

        try
        {
            await next();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Ошибка в конвейере middleware после StartMiddleware");
        }
    }
}
