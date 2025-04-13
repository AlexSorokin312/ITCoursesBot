using System;
using System.Threading;
using System.Threading.Tasks;
using ITCoursesBot.ITCoursesBot.Services.OpenAI;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ITCoursesBot.ITCoursesBot.Services.Telegram
{
    public class TelegramBotService : ITelegramBotService
    {
        private readonly TelegramBotClient _botClient;
        private readonly IOpenAIClient _openAIClient;

        public TelegramBotService(string telegramApiKey, IOpenAIClient openAIClient)
        {
            _botClient = new TelegramBotClient(telegramApiKey);
            _openAIClient = openAIClient;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandleErrorAsync,
                cancellationToken: cancellationToken
            );
            Console.WriteLine("Telegram бот запущен.");
            return Task.CompletedTask;
        }

        private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
        {
            if (update.Message is not { } message || string.IsNullOrEmpty(message.Text))
                return;

            Console.WriteLine($"Получено сообщение: \"{message.Text}\" от ChatId: {message.Chat.Id}");

            string openAIResponse = await _openAIClient.GetChatResponseAsync(message.Text);

            await bot.SendMessage(
                chatId: message.Chat.Id,
                text: openAIResponse,
                cancellationToken: cancellationToken
            );
        }

        private Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Ошибка в Telegram боте: {exception.Message}");
            return Task.CompletedTask;
        }
    }
}
