using ITCoursesBot.ITCoursesBot.Services.Telegram;
using Telegram.Bot.Types;
using Telegram.Bot;

public class TelegramBotService : ITelegramBotService
{
    private readonly ITelegramBotClient _bot;
    private readonly IEnumerable<IUpdateHandler> _handlers;

    public TelegramBotService(
        ITelegramBotClient botClient,
        IEnumerable<IUpdateHandler> handlers)
    {
        _bot = botClient;
        _handlers = handlers;
    }

    public Task StartAsync(CancellationToken ct)
    {
        _bot.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            cancellationToken: ct
        );
        Console.WriteLine("Telegram бот запущен.");
        return Task.CompletedTask;
    }

    private async Task HandleUpdateAsync(
        ITelegramBotClient bot,
        Update update,
        CancellationToken ct)
    {
        foreach (var handler in _handlers)
        {
            if (handler.CanHandle(update))
            {
                await handler.HandleAsync(update, ct);
                return; 
            }
        }
    }

    private Task HandleErrorAsync(
        ITelegramBotClient bot,
        Exception ex,
        CancellationToken ct)
    {
        Console.WriteLine($"Ошибка: {ex.Message}");
        return Task.CompletedTask;
    }
}
