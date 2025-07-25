using Bot.Ports;
using ITCoursesBot.Interfaces;
using ITCoursesBot.ITCoursesBot.Configuration;
using System.Text;
using Telegram.Bot;

namespace ITCoursesBot.ITCoursesBot.Controllers
{
    internal class PassQuizController : BaseController
    {
        private readonly IOpenAIClient _openAi;
        private readonly OpenAISettings _openAISettings;
        private readonly IProgressClient _progressClient;
        private readonly IAiLimitClient _aiLimitClient;

        public PassQuizController(
            ITelegramBotClient bot,
            IMessageService messageService,
            ISessionManager sessionManager,
            IKeyboardBuilder keyboardBuilder,
            IOpenAIClient openAi,
            OpenAISettings openAISettings,
            IProgressClient progressClient,
            IAiLimitClient aiLimitClient
        ) : base(bot, messageService, sessionManager, keyboardBuilder)
        {
            _openAi = openAi;
            _openAISettings = openAISettings;
            _progressClient = progressClient;
            _aiLimitClient = aiLimitClient;
        }

        public override async Task<bool> HandleAsync(CancellationToken ct)
        {
            var session = GetCurrentSessionById(ChatId);

            if (session?.Mode != BotMode.PassQuiz)
                return false;

            if (await HandleInlineCallbackAsync(session, ct))
                return true;

            var msg = CurrentUpdate.Message;
            if (msg == null)
                return false;

            string? answer = msg.Text;
            if (string.IsNullOrWhiteSpace(answer) && msg.Voice != null)
            {
                using var audio = await DownloadVoiceAsync(msg.Voice.FileId, ct);
                answer = await _openAi.TranscribeAudioAsync(audio, $"{msg.Voice.FileUniqueId}.ogg");
            }

            if (string.IsNullOrWhiteSpace(answer))
                return false;

            if (await _aiLimitClient.IsLimitReachedAsync(ChatId, ct))
            {
                await _messageService.SendTextAsync(
                    ChatId,
                    "❗️ Квота запросов к ИИ исчерпана. Попробуйте позже.",
                    cancellationToken: ct);
                return true;
            }

            // 4) Если вопросов нет — просим начать заново
            var questions = session.QuestionsForQuiz;
            if (questions == null || questions.Count == 0)
            {
                await _messageService.SendTextAsync(
                    ChatId,
                    "❗️ Вопросы не загружены. Введите команду /beginquiz, чтобы начать тест.",
                    cancellationToken: ct);
                return true;
            }

            // 5) Обрабатываем ответ на текущий вопрос
            await ProcessAnswerAsync(session, answer, ct);

            await _aiLimitClient.RecordRequestAsync(ChatId, ct);

            return true;
        }

        private async Task ProcessAnswerAsync(ChatSession session, string answer, CancellationToken ct)
        {
            var dto = session.QuestionsForQuiz![session.QuestionIndex];

            // Формируем запрос к OpenAI
            var sys = _openAISettings.InstructionsQuestions;
            var prompt = $"Вопрос: {dto.Text}\nОтвет студента: {answer}";
            var raw = await _openAi.GetChatResponseAsync(sys, prompt);

            // Парсим ответ AI
            var (isCorrect, comment) = ParseAiResponse(raw);

            // Сохраняем попытку через API
            await _progressClient.AddAnswerAsync(
                new AddAnswerDto(
                    TelegramId: ChatId,
                    QuestionId: dto.Id,
                    AnswerText: answer,
                    IsCorrect: isCorrect
                ), ct);

            // Отправляем комментарий AI
            await _messageService.SendTextAsync(ChatId, comment, cancellationToken: ct);

            if (isCorrect)
            {
                session.QuestionIndex++;
                await SendCorrectOrNextAsync(session, ct);
            }
            else
            {
                await _messageService.SendTextAsync(
                    ChatId,
                    "❌ Неправильно. Попробуйте ещё раз.",
                    cancellationToken: ct);
            }
        }

        private (bool IsCorrect, string Comment) ParseAiResponse(string aiReply)
        {
            const string TrueTag = "(TRUE_ANSWER)";
            const string FalseTag = "(FALSE_ANSWER)";
            bool ok = aiReply.Contains(TrueTag, StringComparison.OrdinalIgnoreCase);
            var cleaned = aiReply
                .Replace(TrueTag, "", StringComparison.OrdinalIgnoreCase)
                .Replace(FalseTag, "", StringComparison.OrdinalIgnoreCase)
                .Trim();
            return (ok, cleaned);
        }

        private async Task SendCorrectOrNextAsync(ChatSession session, CancellationToken ct)
        {
            if (session.QuestionIndex >= session.QuestionsForQuiz!.Count)
            {
                await _messageService.SendTextAsync(
                    ChatId,
                    "✅ Все вопросы пройдены! Выберите действие.",
                    replyMarkup: _keyboardBuilder.Build(),
                    cancellationToken: ct);
                session.Mode = BotMode.None;
                return;
            }

            // Предлагаем перейти к следующему вопросу
            await _messageService.SendTextAsync(
                ChatId,
                "✅ Правильно! Перейти к следующему?",
                replyMarkup: _keyboardBuilder.BuildNextFinish(true),
                cancellationToken: ct);
        }

        private async Task<Stream> DownloadVoiceAsync(string fileId, CancellationToken ct)
        {
            var fileInfo = await _bot.GetFile(fileId, cancellationToken: ct);
            var ms = new MemoryStream();
            await _bot.DownloadFile(
                filePath: fileInfo.FilePath!,
                destination: ms,
                cancellationToken: ct);
            ms.Position = 0;
            return ms;
        }

        private async Task<bool> HandleInlineCallbackAsync(ChatSession session, CancellationToken ct)
        {
            var data = CurrentUpdate.CallbackQuery?.Data;
            if (string.IsNullOrEmpty(data))
                return false;

            switch (data)
            {
                case "next_question":
                    await SendNextQuestionAsync(session, ct);
                    break;
                case "finish":
                    await FinishQuizAsync(session, ct);
                    break;
                case "show_all":
                    await ShowAllQuestionsAsync(session, ct);
                    break;
                default:
                    return false;
            }
            return true;
        }

        private async Task ShowAllQuestionsAsync(ChatSession session, CancellationToken ct)
        {
            var questions = session.QuestionsForQuiz!;
            var sb = new StringBuilder();
            for (int i = session.QuestionIndex; i < questions.Count; i++)
                sb.AppendLine($"{i + 1}. {questions[i].Text}");

            await _messageService.SendTextAsync(
                ChatId,
                sb.Length > 0 ? sb.ToString() : "❗️ Нет оставшихся вопросов.",
                replyMarkup: _keyboardBuilder.BuildBackToMenu(),
                    cancellationToken: ct);
        }

        private async Task SendNextQuestionAsync(ChatSession session, CancellationToken ct)
        {
            var dto = session.QuestionsForQuiz![session.QuestionIndex];
            await _messageService.SendTextAsync(
                ChatId,
                $"❓ Вопрос {session.QuestionIndex + 1}/{session.QuestionsForQuiz.Count}:\n{dto.Text}",
                replyMarkup: _keyboardBuilder.BuildNextFinish(true),
                cancellationToken: ct);
        }

        private async Task FinishQuizAsync(ChatSession session, CancellationToken ct)
        {
            session.Mode = BotMode.None;
            session.QuestionsForQuiz?.Clear();
            session.QuestionIndex = 0;
            await _messageService.SendTextAsync(
                ChatId,
                "🔚 Тест завершён. Выберите опцию.",
                replyMarkup: _keyboardBuilder.Build(),
                cancellationToken: ct);
        }

        public override bool CanHandle() =>
            GetCurrentSessionById(ChatId)?.Mode == BotMode.PassQuiz;
    }
}
