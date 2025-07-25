using ITCoursesBot.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;

public abstract class BaseController
{
    protected ITelegramBotClient _bot { get; private set; } = null!;
    protected IMessageService _messageService { get; private set; }
    protected ISessionManager _sessionManager { get; private set; }
    protected IKeyboardBuilder _keyboardBuilder { get; private set; }

    protected Update CurrentUpdate { get; private set; } = null!;

    public BaseController(ITelegramBotClient bot, IMessageService messageService, ISessionManager sessionManager, IKeyboardBuilder keyboardBuilder)
    {
        _bot = bot;
        _messageService = messageService;
        _sessionManager = sessionManager;
        _keyboardBuilder = keyboardBuilder;
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
      ?? CurrentUpdate.CallbackQuery?.Message?.Chat.Id
      ?? 0;
}