
using ITCoursesBot.Interfaces;
using Telegram.Bot;

namespace ITCoursesBot.ITCoursesBot.Controllers
{
    internal class BeginQuizController : BaseController
    {
        private IQuizRepository _quizRepository;
        public BeginQuizController(ITelegramBotClient bot, IMessageService messageService,
            ISessionManager sessionManager, IQuizRepository quizRepository,
            IKeyboardBuilder keyboardBuilder) : base(bot, messageService, sessionManager, keyboardBuilder)
        {
            _quizRepository = quizRepository;
        }

        public override async Task<bool> HandleAsync(CancellationToken ct)
        {
            var canHandle = CanHandle();
            if (!canHandle)
                return false;

            string lessonId = CurrentUpdate?.Message?.Text;
            if (string.IsNullOrEmpty(lessonId))
                return false;
            var questions = _quizRepository.GetQuestionsByLessonAsync(lessonId);
            var session = GetCurrentSessionById(ChatId);

            session.QuestionsForQuiz = new List<string>(questions);

            if (questions != null || questions.Count != 0)
            {
                session.Mode = BotMode.PassQuiz;
                _messageService.SendTextAsync(ChatId, questions[0], _keyboardBuilder.BuildBackToMenu());
            }

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
