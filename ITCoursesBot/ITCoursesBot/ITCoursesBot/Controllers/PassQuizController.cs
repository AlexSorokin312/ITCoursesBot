using ITCoursesBot.Interfaces;
using ITCoursesBot.ITCoursesBot.Configuration;
using ITCoursesBot.ITCoursesBot.Services;
using Telegram.Bot;

namespace ITCoursesBot.ITCoursesBot.Controllers
{
    internal class PassQuizController : BaseController
    {
        private readonly IOpenAIClient _openAi;
        private readonly OpenAISettings _openAISettings;
        private readonly IQuizRepository _quizRepository;
        private readonly QuestionsRepository _repository;
        private readonly UserDbRepository _userDbRepository;

        public PassQuizController(
            ITelegramBotClient bot,
            IMessageService messageService,
            ISessionManager sessionManager,
            IKeyboardBuilder keyboardBuilder,
            IOpenAIClient openAi,
            OpenAISettings openAISettings,
            IQuizRepository quizRepository,
            QuestionsRepository repository,
            UserDbRepository userDbRepository) : base(bot, messageService, sessionManager, keyboardBuilder)
        {
            _openAi = openAi;
            _openAISettings = openAISettings;
            _quizRepository = quizRepository;
            _repository = repository;
            _userDbRepository = userDbRepository;
        }

        public override async Task<bool> HandleAsync(CancellationToken ct)
        {
            var session = GetCurrentSessionById(ChatId);
            if (session?.Mode != BotMode.PassQuiz)
                return false;

            // 1. inline-кнопки
            if (await HandleInlineCallbackAsync(session, ct))
                return true;

            // 2. получаем «ввод» пользователя — текст или расшифровку аудио
            var msg = CurrentUpdate.Message;
            if (msg == null)
                return false;

            string? answerText = msg.Text;
            if (string.IsNullOrWhiteSpace(answerText) && msg.Voice != null)
            {
                // скачиваем и расшифровываем голосовое
                using var audio = await DownloadVoiceStreamAsync(msg.Voice.FileId, ct);
                answerText = await _openAi.TranscribeAudioAsync(
                    audio,
                    fileName: $"{msg.Voice.FileUniqueId}.ogg"
                );
            }

            await _userDbRepository.UpdateUsageAsync(session.ChatId);

            if (string.IsNullOrWhiteSpace(answerText))
                return false;   // ни текст, ни голос

            // 3. загрузка вопросов, если нужно
            if (await EnsureQuestionsLoadedAsync(session, answerText, ct))
                return true;

            // 4. работаем с ответом
            await ProcessAnswerAsync(session, answerText, ct);
            return true;
        }

        /// <summary>
        /// Скачивает голосовое из Telegram и выдаёт в виде Stream.
        /// </summary>
        private async Task<Stream> DownloadVoiceStreamAsync(string fileId, CancellationToken ct)
        {
            // 1) метаданные
            var file = await _bot.GetFile(fileId, cancellationToken: ct);

            // 2) скачиваем в память
            var ms = new MemoryStream();
            await _bot.DownloadFile(
                filePath: file.FilePath!,
                destination: ms,
                cancellationToken: ct
            );
            ms.Position = 0;
            return ms;
        }

        // --- 1. Inline-кнопки ---
        private async Task<bool> HandleInlineCallbackAsync(ChatSession session, CancellationToken ct)
        {
            var data = CurrentUpdate.CallbackQuery?.Data;
            if (data == null) return false;

            switch (data)
            {
                case "next_question":
                    await SendNextQuestionAsync(session, ct);
                    break;

                case "finish":
                    await FinishQuizAsync(session, ct);
                    break;

                default:
                    return false;
            }

            return true;
        }

        private async Task FinishQuizAsync(ChatSession session, CancellationToken ct)
        {
            session.Mode = BotMode.None;
            session.QuestionsForQuiz?.Clear();
            session.QuestionIndex = 0;

            await _messageService.SendTextAsync(
                ChatId,
                "Выберите опцию работы с чатом",
                replyMarkup: _keyboardBuilder.Build()
            );
        }

        // --- 2+3. Загрузка списка вопросов ---
        private async Task<bool> EnsureQuestionsLoadedAsync(ChatSession session, string lessonId, CancellationToken ct)
        {
            if (session.QuestionsForQuiz != null && session.QuestionsForQuiz.Any())
                return false;

            // загружаем
            var questions = _repository.GetQuestions(lessonId);
            if (questions == null || !questions.Any())
            {
                await _messageService.SendTextAsync(
                    ChatId,
                    "Вопросов для этого урока не найдено. Попробуйте другой номер."
                );
                return true; // апдейт «съеден»
            }

            session.QuestionsForQuiz = questions;
            session.QuestionIndex = 0;
            await SendNextQuestionAsync(session, ct);
            return true;
        }

        // --- 4. Обработка ответа студента ---
        private async Task ProcessAnswerAsync(ChatSession session, string answerText, CancellationToken ct)
        {
            // 1) достаём DTO вопроса
            var questionDto = session.QuestionsForQuiz![session.QuestionIndex];

            // 2) формируем prompt для AI
            var systemInst = _openAISettings.InstructionsQuestions;
            var prompt = $"Вопрос: {questionDto.Text}\nОтвет студента: {answerText}";

            // 3) получаем ответ от AI и парсим его
            string raw = await _openAi.GetChatResponseAsync(systemInst, prompt);
            var (isCorrect, comment) = ParseAiResponse(raw);

            // 4) сохраняем каждую попытку в БД (INSERT новой записи)
            await _userDbRepository.AddUserAnswerAsync(
                telegramId: CurrentUpdate.Message!.From!.Id,
                username: CurrentUpdate.Message.From.Username ?? string.Empty,
                questionId: questionDto.Id,
                answerText: answerText,
                isCorrect: isCorrect
            );

            // 5) шлём пользователю комментарий
            await _messageService.SendTextAsync(ChatId, comment);

            // 6) если ответ верный — двигаем индекс и предлагаем следующий
            if (isCorrect)
            {
                session.QuestionIndex++;
                await SendCorrectAsync(session, ct);
            }
            else
            {
                await _messageService.SendTextAsync(
                    ChatId,
                    "Попробуйте ещё раз — уточните ответ."
                );
            }
        }

        private (bool IsCorrect, string Comment) ParseAiResponse(string aiReply)
        {
            const string trueTag = "(TRUE_ANSWER)";
            const string falseTag = "(FALSE_ANSWER)";

            bool ok = aiReply.Contains(trueTag, StringComparison.OrdinalIgnoreCase);
            var cleaned = aiReply
                .Replace(trueTag, "", StringComparison.OrdinalIgnoreCase)
                .Replace(falseTag, "", StringComparison.OrdinalIgnoreCase)
                .Trim();

            return (ok, cleaned);
        }

        private async Task SendCorrectAsync(ChatSession session, CancellationToken ct)
        {
            bool hasMore = session.QuestionIndex < session.QuestionsForQuiz!.Count;
            var kb = _keyboardBuilder.BuildNextFinish(hasMore);

            await _messageService.SendTextAsync(
                ChatId,
                hasMore
                    ? "Правильно! Переходим к следующему?"
                    : "Правильно! Это был последний вопрос.",
                replyMarkup: kb
            );
        }

        // --- Вспомогательный метод не трогаем ---
        private async Task SendNextQuestionAsync(ChatSession session, CancellationToken ct)
        {
            if (session.QuestionsForQuiz == null
                || session.QuestionIndex >= session.QuestionsForQuiz.Count)
            {
                // конец квиза…
            }
            else
            {
                var nextDto = session.QuestionsForQuiz[session.QuestionIndex];
                await _messageService.SendTextAsync(
                    ChatId,
                    $"❓ Вопрос {session.QuestionIndex + 1}/{session.QuestionsForQuiz.Count}:\n{nextDto.Text}"
                );
            }
        }

        public override bool CanHandle()
        {
            var session = GetCurrentSessionById(ChatId);
            if (session == null)
                return false;

            if (session.Mode != BotMode.PassQuiz)
                return false;

            return true;
        }
    }
}
