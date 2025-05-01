using ITCoursesBot.ITCoursesBot.Services.Telegram;
using Telegram.Bot.Types;
using Telegram.Bot;

public class TelegramBotService : ITelegramBotService
{
    private readonly ITelegramBotClient _bot;
    private readonly IMessageService _messageService;
    private readonly IEnumerable<IUpdateHandler> _handlers;
    private readonly IKeyboardBuilder _kbBuilder;


    public TelegramBotService(
        ITelegramBotClient botClient,
        IMessageService messageService,
        IEnumerable<IUpdateHandler> handlers,
        IKeyboardBuilder kbBuilder)
    {
        _bot = botClient;
        _handlers = handlers;
        _messageService=  messageService;
        _kbBuilder = kbBuilder;
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

    public string LastQuestion = string.Empty;

    private async Task HandleUpdateAsync(
        ITelegramBotClient bot,
        Update update,
        CancellationToken ct)
    {
        long chatId;
        var message = update.Message;
        var data = update?.CallbackQuery?.Data;

        if (update.Message.Text == "/start")
        {
            // Строим клавиатуру
            var keyboard = _kbBuilder.Build();
            chatId = update.Message.Chat.Id;
            // Отправляем текст вместе с Inline-клавиатурой
            await _messageService.SendTextAsync(
                chatId: chatId,
                text: "Выберите опцию работы с чатом",
                replyMarkup: keyboard,
                cancellationToken: ct
            );

            // После /start выходим, чтобы не обрабатывать это сообщение дальше
            return;
        }

        else if (data != null)
        {
            chatId = update.CallbackQuery.Message.Chat.Id;

            if (data == "questions")
            {
             await _messageService.SendTextAsync(chatId, "Вы нажали «Вопросы", cancellationToken: ct);
            }
            if (data == "code_explanations")
            {
                await _messageService.SendTextAsync(chatId, "Вы нажали «Объяснения кода»", cancellationToken: ct);
            }
            if (data == "progress")
            {
                await _messageService.SendTextAsync(chatId, "Вы нажали «Прогресс", cancellationToken: ct);
            }
        }
        else
        {
            var keyboard = _kbBuilder.Build();
            chatId = update.Message.Chat.Id;

            await _messageService.SendTextAsync(
             chatId: chatId,
             text: "Выберите опцию работы с чатом",
             replyMarkup: keyboard,
             cancellationToken: ct
);
        }
    }

    private Task HandleErrorAsync( ITelegramBotClient bot, Exception ex,CancellationToken ct)
    {
        Console.WriteLine($"Ошибка: {ex.Message}");
        return Task.CompletedTask;
    }
}
