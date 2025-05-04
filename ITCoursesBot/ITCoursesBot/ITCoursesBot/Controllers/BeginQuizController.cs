using ITCoursesBot.Interfaces;
using ITCoursesBot.ITCoursesBot.Services;
using Telegram.Bot;

namespace ITCoursesBot.ITCoursesBot.Controllers
{
    internal class BeginQuizController : BaseController
    {
        private IQuizRepository _quizRepository;
        private readonly QuestionsRepository _repository;

        public BeginQuizController(ITelegramBotClient bot, IMessageService messageService,
            ISessionManager sessionManager, IQuizRepository quizRepository,
            IKeyboardBuilder keyboardBuilder, QuestionsRepository repository) : base(bot, messageService, sessionManager, keyboardBuilder)
        {
            _quizRepository = quizRepository;
            _repository = repository;
        }

        public override async Task<bool> HandleAsync(CancellationToken ct)
        {
            if (!CanHandle()) return false;

            var lessonId = CurrentUpdate.Message?.Text;
            if (string.IsNullOrWhiteSpace(lessonId)) return false;

            // получаем объекты
            var questions = _repository.GetQuestions(lessonId);
            if (questions.Count == 0) return true;   // ничего нет — гасим апдейт

            var session = GetCurrentSessionById(ChatId);
            session.QuestionsForQuiz = new List<QuestionDto>(questions);   // <‑‑ теперь DTO, а не строки
            session.Mode = BotMode.PassQuiz;

            // показываем первый вопрос
            await _messageService.SendTextAsync(
                ChatId,
                $"❓ Вопрос 1/{questions.Count}:\n{questions[0].Text}",
                replyMarkup: _keyboardBuilder.BuildBackToMenu()
            );

            return true;
        }

        public override bool CanHandle()
        {
            var session = GetCurrentSessionById(ChatId);
            if (session == null)
                return false;

            if (session.Mode != BotMode.BeginQuiz)
                return false;

            if (CurrentUpdate == null)
                return false;

            var content = CurrentUpdate?.Message?.Text;

            if (string.IsNullOrEmpty(content))
                return false;

            return true;
        }
    }
}
