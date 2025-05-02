using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using ITCoursesBot.Interfaces;
using ITCoursesBot.ITCoursesBot.Configuration;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot;

namespace ITCoursesBot.ITCoursesBot.Controllers
{
    internal class PassQuizController : BaseController
    {
        private readonly IKeyboardBuilder _keyboardBuilder;
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
        ) : base(bot, messageService, sessionManager)
        {
            _keyboardBuilder = keyboardBuilder;
            _openAi = openAi;
            _openAISettings = openAISettings;
            _quizRepository = quizRepository;
        }

        public override async Task<bool> HandleAsync(CancellationToken ct)
        {
            // Берём сессию
            var session = GetCurrentSessionById(ChatId);
            if (session == null || session.Mode != BotMode.PassQuiz)
                return false;

            // 1) Обработка inline-кнопок
            var callback = CurrentUpdate.CallbackQuery?.Data;
            if (callback != null)
            {
                switch (callback)
                {
                    case "next_question":
                        await SendNextQuestionAsync(session, ct);
                        break;

                    case "finish":
                        // Завершаем квиз
                        session.Mode = BotMode.None;
                        session.QuestionsForQuiz?.Clear();
                        session.QuestionIndex = 0;
                        await _messageService.SendTextAsync(
                            ChatId,
                            "Выберите опцию работы с чатом",
                            replyMarkup: _keyboardBuilder.Build(),
                            cancellationToken: ct);
                        _sessionManager.GetOrCreateSession(ChatId);
                        break;
                }
                return true;
            }

            // 2) Обработка текстового ответа пользователя
            var text = CurrentUpdate.Message?.Text;
            if (string.IsNullOrEmpty(text))
                return false;

            // Если не инициализирован пул вопросов — берём из репозитория
            if (session.QuestionsForQuiz == null || !session.QuestionsForQuiz.Any())
            {
                // На всякий случай: если BeginQuizController не сработал
                session.QuestionsForQuiz = _quizRepository.GetQuestionsByLessonAsync(text, ct);
                if (!session.QuestionsForQuiz.Any())
                {
                    await _messageService.SendTextAsync(
                        ChatId,
                        "Вопросов для этого урока не найдено. Попробуйте другой номер.",
                        cancellationToken: ct
                    );
                    return true;
                }

                session.QuestionIndex = 0;
                await SendNextQuestionAsync(session, ct);
                return true;
            }

            // Пользователь отвечает на вопрос
            var currentQuestion = session.QuestionsForQuiz[session.QuestionIndex];
            string systemInst = _openAISettings.InstructionsQuestions;
            var prompt = $"Вопрос: {currentQuestion}\nОтвет студента: {text}";
            string aiReply = await _openAi.GetChatResponseAsync(systemInst, prompt);

            // Определяем, правильно ли
            bool isCorrect = aiReply.Contains("(TRUE_ANSWER)", StringComparison.OrdinalIgnoreCase);
            string cleaned = aiReply
                .Replace("(TRUE_ANSWER)", "", StringComparison.OrdinalIgnoreCase)
                .Replace("(FALSE_ANSWER)", "", StringComparison.OrdinalIgnoreCase)
                .Trim();

            // Отправляем комментарий AI
            await _messageService.SendTextAsync(
                ChatId,
                cleaned,
                cancellationToken: ct
            );

            if (isCorrect)
            {
                session.QuestionIndex++;
                bool hasMore = session.QuestionIndex < session.QuestionsForQuiz.Count;
                InlineKeyboardMarkup kb = _keyboardBuilder.BuildNextFinish(hasMore);

                await _messageService.SendTextAsync(
                    ChatId,
                    hasMore
                        ? "Правильно! Переходим к следующему?"
                        : "Правильно! Это был последний вопрос.",
                    replyMarkup: kb,
                    cancellationToken: ct
                );
            }
            else
            {
                // Оставляем тот же вопрос, даём ещё шанс
                await _messageService.SendTextAsync(
                    ChatId,
                    "Попробуйте ещё раз — уточните ответ.",
                    cancellationToken: ct
                );
            }

            return true;
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

        private async Task SendNextQuestionAsync(ChatSession session, CancellationToken ct)
        {
            if (session.QuestionsForQuiz == null
                || session.QuestionIndex >= session.QuestionsForQuiz.Count)
            {
                // Вопросы кончились
                session.Mode = BotMode.None;
                session.QuestionsForQuiz?.Clear();
                session.QuestionIndex = 0;

                await _messageService.SendTextAsync(
                    ChatId,
                    "Вопросы закончились. Нажмите /start, чтобы начать заново.",
                    cancellationToken: ct
                );
                return;
            }

            // Шлём следующий
            string next = session.QuestionsForQuiz[session.QuestionIndex];
            await _messageService.SendTextAsync(
                ChatId,
                $"❓ Вопрос {session.QuestionIndex + 1}/{session.QuestionsForQuiz.Count}:\n{next}",
                cancellationToken: ct
            );
        }
    }
}
