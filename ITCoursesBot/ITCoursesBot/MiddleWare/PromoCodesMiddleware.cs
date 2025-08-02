using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Bot.Ports;
using ITCoursesBot.Interfaces;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types.ReplyMarkups;

public class PromoCodesMiddleware : IUpdateMiddleware
{
    private readonly IMessageService _msg;
    private readonly IKeyboardBuilder _kbd;
    private readonly IAiLimitClient _aiLimitClient;
    private readonly ISessionManager _sessions;
    private readonly ILogger<PromoCodesMiddleware> _log;

    public PromoCodesMiddleware(
        IMessageService msg,
        IKeyboardBuilder kbd,
        IAiLimitClient aiLimitClient,
        ISessionManager sessions,
        ILogger<PromoCodesMiddleware> log)
    {
        _msg = msg;
        _kbd = kbd;
        _aiLimitClient = aiLimitClient;
        _sessions = sessions;
        _log = log;
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
                _log.LogInformation("Пользователь {ChatId} вернулся в главное меню из режима промокодов", chatId);

                var info = await SafeGetRequestInfo(chatId, ctx.CancellationToken);
                await SafeSendAsync(
                    chatId,
                    $"🏠 Главное меню:\nВыберите, чем займёмся дальше!\n\n" +
                    $"⚡ Оставшиеся запросы: **{info.RemainingRequests}**\n" +
                    $"⏰ Время до сброса: **{info.TimeToReset.Hours}ч {info.TimeToReset.Minutes}мин**",
                    _kbd.Build(),
                    ctx.CancellationToken);

                return;
            }

            // 1) Вход в режим промокодов
            if (cbData == KeyboardBuilder.PROMO_CODES_BUTTON_NAME)
            {
                session.Mode = BotMode.PromoCodes;
                _log.LogInformation("Пользователь {ChatId} вошёл в режим промокодов", chatId);

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
                _log.LogInformation("Пользователь {ChatId} пытается применить промокод «{Code}»", chatId, code);

                try
                {
                    await _aiLimitClient.ApplyPromoCodeAsync(chatId, code, ctx.CancellationToken);
                    _log.LogInformation("Промокод «{Code}» успешно применён для {ChatId}", code, chatId);

                    var info = await SafeGetRequestInfo(chatId, ctx.CancellationToken);
                    session.Mode = BotMode.None;

                    await SafeSendAsync(
                        chatId,
                        $"✅ Промокод «{code}» успешно применён!\n\n🏠 Главное меню:\nВыберите, чем займёмся дальше!\n\n" +
                        $"⚡ Оставшиеся запросы: **{info.RemainingRequests}**\n" +
                        $"⏰ Время до сброса: **{info.TimeToReset.Hours}ч {info.TimeToReset.Minutes}мин**",
                        _kbd.Build(),
                        ctx.CancellationToken);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Ошибка применения промокода «{Code}» для {ChatId}", code, chatId);

                    var info = await SafeGetRequestInfo(chatId, ctx.CancellationToken);
                    session.Mode = BotMode.None;

                    await SafeSendAsync(
                        chatId,
                        $"❌ Не удалось применить промокод «{code}»: {ex.Message}\n\n🏠 Главное меню:\nВыберите, чем займёмся дальше!\n\n" +
                        $"⚡ Оставшиеся запросы: **{info.RemainingRequests}**\n" +
                        $"⏰ Время до сброса: **{info.TimeToReset.Hours}ч {info.TimeToReset.Minutes}мин**",
                        _kbd.Build(),
                        ctx.CancellationToken);
                }

                return;
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Неожиданная ошибка в PromoCodesMiddleware для {ChatId}", chatId);
        }

        await next();
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
            _log.LogError(ex, "Не удалось получить информацию по квоте для {ChatId}", chatId);
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
            await _msg.SendTextAsync(
                chatId,
                text,
                replyMarkup: markup,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Не удалось отправить сообщение чату {ChatId}: {Text}", chatId, text);
        }
    }

    #endregion
}
