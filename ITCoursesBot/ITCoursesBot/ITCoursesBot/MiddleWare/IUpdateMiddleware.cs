using Telegram.Bot;
using Telegram.Bot.Types;

public interface IUpdateMiddleware
{
    Task InvokeAsync(UpdateContext ctx, Func<Task> next);
}

public record UpdateContext(ITelegramBotClient BotClient, Update Update, CancellationToken CancellationToken);