using Telegram.Bot.Types;
using Telegram.Bot;
using ITCoursesBot.Interfaces;

public class AudioMessageHandler : IUpdateHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly IOpenAIClient _openAI;
    private readonly ITextToSpeechService _tts;

    public AudioMessageHandler(
        ITelegramBotClient bot,
        IOpenAIClient openAI,
        ITextToSpeechService tts)
    {
        _bot = bot;
        _openAI = openAI;
        _tts = tts;
    }

    public bool CanHandle(Update update) =>
        update.Message?.Voice is not null
     || update.Message?.Audio is not null;

    public async Task HandleAsync(Update update, CancellationToken ct)
    {
       /* var msg = update.Message!;
        var chatId = msg.Chat.Id;
        string fileId = msg.Voice?.FileId ?? msg.Audio!.FileId;

        // 1) Скачиваем
        var file = await _bot.GetFile(fileId, ct);
        await using var ms = new MemoryStream();
        await _bot.DownloadFile(file.FilePath!, ms, ct);
        ms.Position = 0;

        // 2) Транскрибация
        string name = Path.GetFileName(file.FilePath!) ?? "audio.ogg";
        Console.WriteLine($"Received {name} from {chatId}");
        string transcription = await _openAI.TranscribeAudioAsync(ms, name);
        Console.WriteLine($"Whisper → \"{transcription}\"");

        // 3) Ответ от модели
        string aiText = await _openAI.GetChatResponseAsync(transcription ,null);

        // 4) Отправляем текст
        await _bot.SendMessage(
            chatId: chatId,
            text: aiText,
            cancellationToken: ct
        );

        // 5) и озвучку
        await _tts.SendSpeechAsync(_bot, chatId, aiText, ct);*/
    }
}