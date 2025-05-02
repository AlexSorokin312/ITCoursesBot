using Telegram.Bot;
using Telegram.Bot.Types;

public abstract class BaseController
{
    protected ITelegramBotClient Bot { get; private set; } = null!;
    protected Update Update { get; private set; } = null!;

    public BaseController(ITelegramBotClient bot)
        => Bot = bot;

    public void SetUpdate(Update upd) => Update = upd;

    /// <summary>
    /// Возвращает true, если контроллер «забрал» апдейт.
    /// </summary>
    public abstract Task<bool> HandleAsync(CancellationToken ct);

    protected long ChatId =>
        Update.Message?.Chat.Id
      ?? Update.CallbackQuery?.Message.Chat.Id
      ?? 0;
}