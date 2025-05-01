using ITCoursesBot.ITCoursesBot.Services.OpenAI;
using Telegram.Bot;
using Telegram.Bot.Types;

public class UserSession
{
    public int UserId;
    public string Mode;
}

public class TextMessageHandler : IUpdateHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly IOpenAIClient _openAI;
    private readonly IKeyboardBuilder _kbBuilder;
    private readonly ITextToSpeechService _tts;

    public TextMessageHandler(
        ITelegramBotClient bot,
        IOpenAIClient openAI,
        IKeyboardBuilder kbBuilder,
        ITextToSpeechService tts)
    {
        _bot = bot;
        _openAI = openAI;
        _kbBuilder = kbBuilder;
        _tts = tts;
    }

    public async Task HandleAsync(Update update, CancellationToken ct)
    {
        var msg = update.Message!;
        var chatId = msg.Chat.Id;
        Console.WriteLine($"Text: \"{msg.Text}\" from {chatId}");

        // 1) Получаем ответ OpenAI
        string aiText = await _openAI.GetChatResponseAsync(msg.Text);

        // 2) Строим клавиатуру
        var keyboard = _kbBuilder.Build();

        // 3) Отправляем текст + кнопки
        await _bot.SendMessage(
            chatId: chatId,
            text: aiText,
            replyMarkup: keyboard,
            cancellationToken: ct
        );

        // 4) Отправляем озвучку
        await _tts.SendSpeechAsync(_bot, chatId, aiText, ct);
    }

    public bool CanHandle(Update update)
    {
        return update.Message?.Text is not null
            || update.CallbackQuery is not null;
    }
}
