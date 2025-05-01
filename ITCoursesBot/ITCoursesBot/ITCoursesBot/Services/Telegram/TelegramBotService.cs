using ITCoursesBot.ITCoursesBot.Services.Telegram;
using Telegram.Bot.Types;
using Telegram.Bot;
using System.Collections.Concurrent;
using ITCoursesBot.ITCoursesBot.Services.OpenAI;
using ITCoursesBot.ITCoursesBot.Configuration;
using System.Text;


// ------------- перечисления/классы для состояния ------------------------------------------------
public enum BotMode { None, Questions, CodeExplain, MockInterview, Dialog }

public class TelegramBotService : ITelegramBotService
{
    private readonly ITelegramBotClient _bot;
    private readonly IMessageService _messageService;
    private readonly IOpenAIClient _openAi;
    private readonly IEnumerable<IUpdateHandler> _handlers;
    private readonly IKeyboardBuilder _kbBuilder;
    private readonly IQuestionRepository _questions;
    private readonly OpenAISettings _openAISettings;


    // Храним состояние диалога на пользователя
    private readonly ConcurrentDictionary<long, ChatSession> _sessions = new();


    public TelegramBotService(
        ITelegramBotClient botClient,
        IMessageService messageService,
        IEnumerable<IUpdateHandler> handlers,
        IKeyboardBuilder kbBuilder,
        OpenAISettings openAISettings,
        IQuestionRepository questions,
        IOpenAIClient openAi
        )
    {
        _bot = botClient;
        _handlers = handlers;
        _messageService = messageService;
        _kbBuilder = kbBuilder;
        _questions = questions;
        _openAi = openAi;
        _openAISettings = openAISettings;
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
        // ---- определяем chatId, text и callback-data -------------------------------------------
        var msg = update.Message;
        var callbackData = update.CallbackQuery?.Data;
        long chatId = update.CallbackQuery?.Message.Chat.Id
                            ?? msg?.Chat.Id
                            ?? 0;



        // ---- /start ---------------------------------------------------------------------------
        if (msg?.Text == "/start")
        {
            await _messageService.SendTextAsync(
                chatId,
                "Выберите опцию работы с чатом",
                replyMarkup: _kbBuilder.Build(),
                cancellationToken: ct);
            _sessions.TryRemove(chatId, out _);          // сбрасываем любую прошлую сессию
            return;
        }

        // ---------------------------------------------------------------------------------------
        // ❶ ОБРАБОТКА НАЖАТИЙ ИНЛАЙН-КНОПОК
        // ---------------------------------------------------------------------------------------

        if (callbackData is not null)
        {
            var session = _sessions.GetOrAdd(chatId, _ => new ChatSession());

            switch (callbackData)
            {
                case "questions":
                    session.Mode = BotMode.Questions;
                    session.LessonIdentifier = null;
                    await _messageService.SendTextAsync(chatId,
                        "Введите идентификатор урока", cancellationToken: ct);
                    break;

                case "code_explanations":
                    session.Mode = BotMode.CodeExplain;
                    await _messageService.SendTextAsync(chatId,
                        "Включен режим «Объяснение кода», пришлите фрагмент — я объясню.",
                        cancellationToken: ct);
                    break;

                case "mock_interview":
                    session.Mode = BotMode.MockInterview;
                    await _messageService.SendTextAsync(chatId,
                        "Выберите раздел собеседования:\n• базовый курс\n• продвинутый курс\n• ООП\n• все блоки",
                        cancellationToken: ct);
                    break;

                case "dialog":
                    session.Mode = BotMode.Dialog;
                    await _messageService.SendTextAsync(chatId,
                        "Вы в режиме диалога. Спрашивайте — отвечаю.",
                        cancellationToken: ct);
                    break;

                case "next_question":                           // кнопка «Следующий вопрос»
                    await SendNextQuestionAsync(chatId, session, ct);
                    break;

                case "finish":                                  // кнопка «Завершить»
                    _sessions.TryRemove(chatId, out _);
                    await _messageService.SendTextAsync(chatId,
                        "Сессия завершена. Чтобы начать заново нажмите /start.",
                        cancellationToken: ct);
                    break;
            }

            return;
        }

        // ---------------------------------------------------------------------------------------
        // ❷ ОБРАБОТКА ТЕКСТОВЫХ СООБЩЕНИЙ
        // ---------------------------------------------------------------------------------------
        if (!_sessions.TryGetValue(chatId, out var s) || s.Mode == BotMode.None)
        {
            // пользователь вне контекста: предлагаем меню
            await _messageService.SendTextAsync(chatId,
                "Выберите опцию работы с чатом",
                replyMarkup: _kbBuilder.Build(),
                cancellationToken: ct);
            return;
        }

        string systemInst = s.Mode switch
        {
            BotMode.Questions => _openAISettings.InstructionsQuestions,
            BotMode.Dialog => _openAISettings.InstructionsDialog,
            BotMode.CodeExplain => _openAISettings.InstructionsCodeExplain,
            BotMode.MockInterview => _openAISettings.InstructionsMockInterview,
            _ => _openAISettings.InstructionsDialog
        };

        // -------- режим “Questions” ------------------------------------------------------------
        if (s.Mode == BotMode.Questions)
        {
            // шаг 1: ждём номер урока
            if (s.WaitingForLessonNumber)
            {
                // получаем вопросы из БД
                s.LessonIdentifier = msg.Text;
                s.QuestionPool = _questions.GetQuestionsByLessonAsync(msg.Text, ct);

                if (s.QuestionPool.Count == 0)
                {
                    await _messageService.SendTextAsync(chatId,
                        "Вопросов для этого урока не найдено. Попробуйте другой номер.", cancellationToken: ct);
                    s.LessonIdentifier = null;                           // ждём корректный номер
                    return;
                }

                s.QuestionIndex = 0;
                await SendNextQuestionAsync(chatId, s, ct);
                return;
            }

            // шаг 2: обрабатываем ответ пользователя на текущий вопрос
            if (s.WaitingForAnswer)
            {
                string question = s.QuestionPool![s.QuestionIndex];
                string userAnswer = msg!.Text;

                // формируем промпт = вопрос + ответ пользователя
                var promptForAI = $"Вопрос: {question}\nОтвет студента: {userAnswer}";
                string aiReply = await _openAi.GetChatResponseAsync(systemInst, promptForAI);

                // ищем маркеры TRUE_ANSWER / FALSE_ANSWER
                bool isCorrect = aiReply.Contains("(TRUE_ANSWER)", StringComparison.OrdinalIgnoreCase);
                string cleaned = aiReply
                                   .Replace("(TRUE_ANSWER)", "", StringComparison.OrdinalIgnoreCase)
                                   .Replace("(FALSE_ANSWER)", "", StringComparison.OrdinalIgnoreCase)
                                   .Trim();

                // отправляем объяснение/комментарий
                await _messageService.SendTextAsync(chatId, cleaned, cancellationToken: ct);

                if (isCorrect)
                {
                    s.QuestionIndex++;
                    // готовим inline-кнопки «Следующий / Завершить»
                    bool more = s.QuestionIndex < s.QuestionPool.Count;
                    var kb = _kbBuilder.BuildNextFinish(more);

                    await _messageService.SendTextAsync(chatId,
                        more ? "Правильно! Переходим к следующему?" : "Правильно! Это был последний вопрос.",
                        replyMarkup: kb, cancellationToken: ct);
                }
                else
                {
                    // ждём новую попытку, ничего не меняем
                    await _messageService.SendTextAsync(chatId,
                        "Попробуйте ещё раз — уточните ответ.", cancellationToken: ct);
                }

                return;
            }
        }

        // -------- режим “Code Explain” ---------------------------------------------------------
        if (s.Mode == BotMode.CodeExplain)
        {
            string explanation = await _openAi.GetChatResponseAsync(systemInst, msg!.Text);

            // Приводим текст к корректному Markdown V2 с code-block'ами
            string markdown = TelegramMessageService.PrepareMarkdownV2(explanation);

            await _messageService.SendTextAsync(
                chatId,
                markdown,
                replyMarkup: _kbBuilder.BuildBackToMenu(),
                asMarkdown: true,
                cancellationToken: ct);

            return;
        }

        // -------- режим “Mock Interview” -------------------------------------------------------
        if (s.Mode == BotMode.MockInterview)
        {
            // если раздел ещё не выбран – текущее сообщение содержит название раздела
            if (s.LessonIdentifier is null)
            {
                string block = msg!.Text.Trim().ToLower();
                s.QuestionPool = _questions.GetQuestionsForInterviewBlockAsync(block, ct);

                if (s.QuestionPool.Count == 0)
                {
                    await _messageService.SendTextAsync(chatId,
                        "Раздел не распознан или в нём нет вопросов. Попробуйте другой.", cancellationToken: ct);
                    return;
                }

                s.LessonIdentifier = string.Empty;       // просто отметка, что раздел выбран
                s.QuestionIndex = 0;
                await SendNextQuestionAsync(chatId, s, ct);
                return;
            }

            // обрабатываем ответ студента (логика аналогична режиму “Questions”, но без TRUE_ / FALSE_)
            string q = s.QuestionPool![s.QuestionIndex];
            string prompt = $"Ты интервьюер. Задай фоллоу-апы при необходимости. Вопрос: {q}\nОтвет кандидата: {msg!.Text}";
            string feedback = await _openAi.GetChatResponseAsync(systemInst, prompt);
            await _messageService.SendTextAsync(chatId, feedback, cancellationToken: ct);

            // сразу следующий вопрос
            await SendNextQuestionAsync(chatId, s, ct);
            return;
        }

        // -------- режим “Dialog” ---------------------------------------------------------------
        if (s.Mode == BotMode.Dialog)
        {
            string answer = await _openAi.GetChatResponseAsync(systemInst, msg!.Text);
            await _messageService.SendTextAsync(chatId, answer, cancellationToken: ct);
            return;
        }
    }

    // =================================================================================================
    //                                        ВСПОМОГАТЕЛЬНЫЙ МЕТОД
    // =================================================================================================
    private async Task SendNextQuestionAsync(long chatId, ChatSession s, CancellationToken ct)
    {
        if (s.QuestionPool is null || s.QuestionIndex >= s.QuestionPool.Count)
        {
            await _messageService.SendTextAsync(chatId,
                "Вопросы закончились. Нажмите /start, чтобы начать заново.",
                cancellationToken: ct);
            _sessions.TryRemove(chatId, out _);
            return;
        }

        string next = s.QuestionPool[s.QuestionIndex];
        await _messageService.SendTextAsync(chatId,
            $"❓ Вопрос {s.QuestionIndex + 1}/{s.QuestionPool.Count}:\n{next}",
            cancellationToken: ct);
    }

    private Task HandleErrorAsync( ITelegramBotClient bot, Exception ex,CancellationToken ct)
    {
        Console.WriteLine($"Ошибка: {ex.Message}");
        return Task.CompletedTask;
    }
}


public class ChatSession
{
    public BotMode Mode { get; set; } = BotMode.None;

    // --- блок “Questions” / “MockInterview” ---
    public string? LessonIdentifier { get; set; }
    public List<string>? QuestionPool { get; set; }
    public int QuestionIndex { get; set; }

    // --- общие поля ---
    public bool WaitingForLessonNumber => Mode is BotMode.Questions && LessonIdentifier is null;
    public bool WaitingForAnswer => Mode is BotMode.Questions or BotMode.MockInterview
                                                 && LessonIdentifier is not null
                                                 && QuestionPool is not null;
}