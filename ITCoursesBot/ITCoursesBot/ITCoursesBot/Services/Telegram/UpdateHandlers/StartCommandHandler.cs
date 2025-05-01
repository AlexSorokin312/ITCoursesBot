using Telegram.Bot;
using Telegram.Bot.Types;

public class StartCommandHandler : IUpdateHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly IKeyboardBuilder _kbBuilder;

    public StartCommandHandler(
        ITelegramBotClient bot,
        IKeyboardBuilder kbBuilder)
    {
        _bot = bot;
        _kbBuilder = kbBuilder;
    }

    public bool CanHandle(Update update) =>
        update.Message?.Text?.Trim() == "/start";

    public async Task HandleAsync(Update update, CancellationToken ct)
    {
       
    }
}

public class StartButtonHandler : IUpdateHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly IKeyboardBuilder _kbBuilder;

    public StartButtonHandler(ITelegramBotClient bot, IKeyboardBuilder kbBuilder)
    {
        _bot = bot;
        _kbBuilder = kbBuilder;
    }

    // Ловим именно текст «Старт» после того, как показали ReplyKeyboard
    public bool CanHandle(Update update) =>
        update.Message?.Text == "Старт";

    public async Task HandleAsync(Update update, CancellationToken ct)
    {
        var chatId = update.Message!.Chat.Id;

        // Убираем ReplyKeyboard и сразу выводим наш Inline-кейборд
        var inlineKb = _kbBuilder.Build();
        await _bot.SendMessage(
            chatId: chatId,
            text: "Вот меню бота:",
            replyMarkup: inlineKb,
            cancellationToken: ct
        );
    }
}