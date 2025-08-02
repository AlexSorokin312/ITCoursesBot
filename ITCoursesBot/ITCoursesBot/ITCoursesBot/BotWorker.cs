using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

public class BotWorker : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly UpdateMiddlewarePipeline _pipeline;

    public BotWorker(
        ITelegramBotClient botClient,
        UpdateMiddlewarePipeline pipeline)
    {
        _botClient = botClient;
        _pipeline = pipeline;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LoggerService.LogInfo("Бот запускается и начинает приём обновлений");

        _botClient.StartReceiving(
            (client, update, token) =>
                _pipeline.ProcessAsync(new UpdateContext(client, update, token)),

            (client, exception, token) =>
            {
                LoggerService.LogError(
                    $"Ошибка при приёме обновлений: {exception.Message}\n{exception.StackTrace}");
                return Task.CompletedTask;
            },

            new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>(), // все типы апдейтов
                DropPendingUpdates = false,                     // не сбрасывать ожидающие
                Limit = 5                          // макс. одновр. апдейтов
            },
            cancellationToken: stoppingToken
        );

        LoggerService.LogInfo("Бот запущен и слушает обновления");
        return Task.CompletedTask;
    }
}
