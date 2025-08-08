using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Bot.Ports;
using ITCoursesBot.Interfaces;
using Telegram.Bot.Types.ReplyMarkups;

public class PromoCodesMiddleware : IUpdateMiddleware
{
    private readonly IMessageService _msg;
    private readonly IKeyboardBuilder _kbd;
    private readonly IAiLimitClient _aiLimitClient;
    private readonly ISessionManager _sessions;

    public PromoCodesMiddleware(
        IMessageService msg,
        IKeyboardBuilder kbd,
        IAiLimitClient aiLimitClient,
        ISessionManager sessions)
    {
        _msg = msg;
        _kbd = kbd;
        _aiLimitClient = aiLimitClient;
        _sessions = sessions;
    }

    public async Task InvokeAsync(UpdateContext ctx, Func<Task> next)
    {
        var chatId = ctx.GetChatId();
        var session = _sessions.GetOrCreateSession(chatId);
        var cbData = ctx.Update.CallbackQuery?.Data;

        try
        {
            // 0) Выход в главное меню из режима промокодов
            if (cbData == KeyboardBuilder.BACK_TO_MENU_BUTTON_NAME
                && session.Mode == BotMode.PromoCodes)
            {
                session.Mode = BotMode.None;
                LoggerService.LogInfo($"Пользователь {chatId} вернулся в главное меню из режима промокодов");

                var (remaining, reset) = await SafeGetRequestInfo(chatId, ctx.CancellationToken);
                await SafeSendAsync(
                    chatId,
                    $"🏠 Главное меню:\nВыберите, чем займёмся дальше!\n\n" +
                    $"⚡ Оставшиеся запросы: **{remaining}**\n" +
                    $"⏰ Время до сброса: **{reset.Hours}ч {reset.Minutes}мин**",
                    _kbd.Build(),
                    ctx.CancellationToken);

                return;
            }

            // 1) Вход в режим промокодов
            if (cbData == KeyboardBuilder.PROMO_CODES_BUTTON_NAME)
            {
                session.Mode = BotMode.PromoCodes;
                LoggerService.LogInfo($"Пользователь {chatId} вошёл в режим промокодов");

                await SafeSendAsync(
                    chatId,
                    "🎟️ Режим промокодов.\nВведите код промокода для применения:",
                    _kbd.BuildBackToMenu(),
                    ctx.CancellationToken);

                return;
            }

            // 2) Обработка текста в режиме промокодов
            if (session.Mode == BotMode.PromoCodes
                && !string.IsNullOrWhiteSpace(ctx.Update.Message?.Text))
            {
                var code = ctx.Update.Message.Text.Trim();
                LoggerService.LogInfo($"Пользователь {chatId} пытается применить промокод «{code}»");

                try
                {
                    await _aiLimitClient.ApplyPromoCodeAsync(chatId, code, ctx.CancellationToken);
                    LoggerService.LogInfo($"Промокод «{code}» успешно применён для пользователя {chatId}");

                    var (remaining, reset) = await SafeGetRequestInfo(chatId, ctx.CancellationToken);
                    session.Mode = BotMode.None;

                    await SafeSendAsync(
                        chatId,
                        $"✅ Промокод «{code}» успешно применён!\n\n🏠 Главное меню:\nВыберите, чем займёмся дальше!\n\n" +
                        $"⚡ Оставшиеся запросы: **{remaining}**\n" +
                        $"⏰ Время до сброса: **{reset.Hours}ч {reset.Minutes}мин**",
                        _kbd.Build(),
                        ctx.CancellationToken);
                }
                catch (Exception ex)
                {
                    LoggerService.LogError(ex.ToString(), $"Ошибка применения промокода «{code}» для пользователя {chatId}");

                    var (remaining, reset) = await SafeGetRequestInfo(chatId, ctx.CancellationToken);
                    session.Mode = BotMode.None;

                    await SafeSendAsync(
                        chatId,
                        $"❌ Не удалось применить промокод!\n\n🏠 Главное меню:\nВыберите, чем займёмся дальше!\n\n" +
                        $"⚡ Оставшиеся запросы: **{remaining}**\n" +
                        $"⏰ Время до сброса: **{reset.Hours}ч {reset.Minutes}мин**",
                        _kbd.Build(),
                        ctx.CancellationToken);
                }

                return;
            }
        }
        catch (Exception ex)
        {
            LoggerService.LogError(ex.ToString(), $"Необработанная ошибка в PromoCodesMiddleware для пользователя {chatId}");
        }

        await next();
    }

    private async Task<(int Remaining, TimeSpan TimeToReset)> SafeGetRequestInfo(long chatId, CancellationToken ct)
    {
        try
        {
            var info = await _aiLimitClient.GetRequestInfoAsync(chatId, ct);
            return (info.RemainingRequests, info.TimeToReset);
        }
        catch (Exception ex)
        {
            LoggerService.LogError(ex.ToString(), $"Не удалось получить информацию по квоте для пользователя {chatId}");
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