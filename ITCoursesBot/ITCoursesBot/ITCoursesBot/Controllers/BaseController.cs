using ITCoursesBot.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;

public abstract class BaseController
{
    protected ITelegramBotClient Bot { get; private set; } = null!;

    protected IMessageService _messageService { get; private set; }
    protected ISessionManager _sessionManager { get; private set; }

    protected Update CurrentUpdate { get; private set; } = null!;

    public BaseController(ITelegramBotClient bot, IMessageService messageService, ISessionManager sessionManager)
    {
        Bot = bot;
        _messageService = messageService;
        _sessionManager = sessionManager;
    }

    protected BaseController(ITelegramBotClient bot)
    {
        Bot = bot;
    }

    public abstract bool CanHandle();

    public ChatSession GetCurrentSessionById(long id)
    {
       return _sessionManager.GetOrCreateSession(id);
    }

    public void SetUpdate(Update upd) => CurrentUpdate = upd;

    /// <summary>
    /// Возвращает true, если контроллер «забрал» апдейт.
    /// </summary>
    public abstract Task<bool> HandleAsync(CancellationToken ct);
    protected long ChatId =>
        CurrentUpdate.Message?.Chat.Id
      ?? CurrentUpdate.CallbackQuery?.Message.Chat.Id
      ?? 0;
}