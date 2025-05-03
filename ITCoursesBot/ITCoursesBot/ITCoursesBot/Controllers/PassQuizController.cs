using ITCoursesBot.Interfaces;
using ITCoursesBot.ITCoursesBot.Configuration;
using Telegram.Bot;

namespace ITCoursesBot.ITCoursesBot.Controllers
{
    internal class PassQuizController : BaseController
    {
        private readonly IOpenAIClient _openAi;
        private readonly OpenAISettings _openAISettings;
        private readonly IQuizRepository _quizRepository;

        public PassQuizController(
            ITelegramBotClient bot,
            IMessageService messageService,
            ISessionManager sessionManager,
            IKeyboardBuilder keyboardBuilder,
            IOpenAIClient openAi,
            OpenAISettings openAISettings,
            IQuizRepository quizRepository
        ) : base(bot, messageService, sessionManager, keyboardBuilder)
        {
            _openAi = openAi;
            _openAISettings = openAISettings;
            _quizRepository = quizRepository;
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
            var file = await Bot.GetFile(fileId, cancellationToken: ct);

            // 2) скачиваем в память
            var ms = new MemoryStream();
            await Bot.DownloadFile(
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
                replyMarkup: _keyboardBuilder.Build(),
                cancellationToken: ct
            );
        }

        // --- 2+3. Загрузка списка вопросов ---
        private async Task<bool> EnsureQuestionsLoadedAsync(ChatSession session, string lessonId, CancellationToken ct)
        {
            if (session.QuestionsForQuiz != null && session.QuestionsForQuiz.Any())
                return false;

            // загружаем
            var questions = _quizRepository.GetQuestionsByLessonAsync(lessonId, ct);
            if (questions == null || !questions.Any())
            {
                await _messageService.SendTextAsync(
                    ChatId,
                    "Вопросов для этого урока не найдено. Попробуйте другой номер.",
                    cancellationToken: ct
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
            var question = session.QuestionsForQuiz![session.QuestionIndex];
            var systemInst = _openAISettings.InstructionsQuestions;
            var prompt = $"Вопрос: {question}\nОтвет студента: {answerText}";

            string raw = await _openAi.GetChatResponseAsync(systemInst, prompt);
            var (isCorrect, comment) = ParseAiResponse(raw);

            // комментарий от AI
            await _messageService.SendTextAsync(
                ChatId,
                comment,
                cancellationToken: ct
            );

            if (isCorrect)
            {
                session.QuestionIndex++;
                await SendCorrectAsync(session, ct);
            }
            else
            {
                await _messageService.SendTextAsync(
                    ChatId,
                    "Попробуйте ещё раз — уточните ответ.",
                    cancellationToken: ct
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
                replyMarkup: kb,
                cancellationToken: ct
            );
        }

        // --- Вспомогательный метод не трогаем ---
        private async Task SendNextQuestionAsync(ChatSession session, CancellationToken ct)
        {
            if (session.QuestionsForQuiz == null
                || session.QuestionIndex >= session.QuestionsForQuiz.Count)
            {
                // Конец квиза
                session.Mode = BotMode.None;
                session.QuestionsForQuiz?.Clear();
                session.QuestionIndex = 0;

                await _messageService.SendTextAsync(
                    ChatId,
                    "Вопросы закончились. Нажмите /start, чтобы начать заново.",
                    cancellationToken: ct
                );
            }
            else
            {
                string next = session.QuestionsForQuiz[session.QuestionIndex];
                await _messageService.SendTextAsync(
                    ChatId,
                    $"❓ Вопрос {session.QuestionIndex + 1}/{session.QuestionsForQuiz.Count}:\n{next}",
                    cancellationToken: ct
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
