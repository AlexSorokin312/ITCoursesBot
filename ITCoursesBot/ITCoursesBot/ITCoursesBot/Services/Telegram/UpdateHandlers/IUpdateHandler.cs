using Telegram.Bot.Types;

public interface IUpdateHandler
{
    /// <summary>Готов ли обработчик взять этот апдейт на себя?</summary>
    bool CanHandle(Update update);

    /// <summary>Обработать апдейт</summary>
    Task HandleAsync(Update update, CancellationToken ct);
}
