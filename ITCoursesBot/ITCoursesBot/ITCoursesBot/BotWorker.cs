using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;

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
            var updates = await _bot.GetUpdates(_offset, cancellationToken: stoppingToken);
            foreach (var upd in updates)
            {
                _offset = upd.Id + 1;
                try
                {
                    using var scope = _sp.CreateScope();
                    var router = scope.ServiceProvider.GetRequiredService<UpdateRouter>();
                    await router.RouteAsync(upd, stoppingToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ DI‑error: {ex}");
                    await Task.Delay(3000, stoppingToken);    // чтобы не уйти в цикл падений
                
                }
            }
        }
    }
}
