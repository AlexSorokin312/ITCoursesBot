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

        private async Task HandleUpdateAsync(
            ITelegramBotClient bot,
            Update update,
            CancellationToken cancellationToken)
        {
            if (update.Message is not { } message)
                return;

            // 1) Текстовые сообщения
            if (!string.IsNullOrEmpty(message.Text))
            {
                Console.WriteLine($"Text: \"{message.Text}\" from {message.Chat.Id}");

                // Получаем ответ от OpenAI
                string aiText = await _openAIClient.GetChatResponseAsync(message.Text);

                // 1.1) Отправляем текст
                await bot.SendMessage(
                    chatId: message.Chat.Id,
                    text: aiText,
                    cancellationToken: cancellationToken
                );

                // 1.2) Генерируем голосовое из ответа и отправляем
                await SendTtsAsync(bot, message.Chat.Id, aiText, cancellationToken);
                return;
            }

            // 2) Голосовые / аудио-сообщения
            if (message.Voice is not null || message.Audio is not null)
            {
                string fileId = message.Voice?.FileId ?? message.Audio!.FileId;
                var file = await bot.GetFile(fileId, cancellationToken);

                await using var ms = new MemoryStream();
                await bot.DownloadFile(file.FilePath!, ms, cancellationToken);
                ms.Position = 0;

                string name = Path.GetFileName(file.FilePath!) ?? "audio.ogg";
                Console.WriteLine($"Received audio {name} from {message.Chat.Id}");

                // Расшифровка через Whisper
                string transcription = await _openAIClient.TranscribeAudioAsync(ms, name);
                Console.WriteLine($"Whisper → \"{transcription}\"");

                // Получаем ответ чат-модели
                string aiText = await _openAIClient.GetChatResponseAsync(transcription);

                // 2.1) Отправляем текстовый ответ
                await bot.SendMessage(
                    chatId: message.Chat.Id,
                    text: aiText,
                    cancellationToken: cancellationToken
                );

                // 2.2) Отправляем озвученный ответ
                await SendTtsAsync(bot, message.Chat.Id, aiText, cancellationToken);
            }
        }

        private async Task SendTtsAsync(
            ITelegramBotClient bot,
            long chatId,
            string text,
            CancellationToken cancellationToken)
        {
            // Генерируем поток с opus-аудио через OpenAI TTS API :contentReference[oaicite:0]{index=0}
            await using Stream speechStream = await _openAIClient.GenerateSpeechAsync(
                text,
                model: "tts-1",
                voice: "onyx",
                format: "opus"
            );

            // Отправляем как голосовое сообщение
            var input = InputFile.FromStream(speechStream, "response.ogg");
            await bot.SendVoice(
                chatId: chatId,
                voice: input,
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
