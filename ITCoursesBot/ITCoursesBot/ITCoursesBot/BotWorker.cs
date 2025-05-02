// BotWorker.cs
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot.Types;

public class BotWorker : BackgroundService
{
    private readonly ITelegramBotClient _bot;
    private readonly IServiceProvider _sp;
    private int _offset = 0;

    public BotWorker(ITelegramBotClient bot, IServiceProvider sp)
    {
        _bot = bot;
        _sp = sp;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var updates = await _bot.GetUpdatesAsync(_offset, cancellationToken: stoppingToken);
            foreach (var upd in updates)
            {
                _offset = upd.Id + 1;
                using var scope = _sp.CreateScope();
                var router = scope.ServiceProvider.GetRequiredService<UpdateRouter>();
                _ = router.RouteAsync(upd, stoppingToken);
            }
        }
    }
}
