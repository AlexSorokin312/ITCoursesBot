using ITCoursesBot.Interfaces;
using ITCoursesBot.ITCoursesBot.Services;
using System.Text.RegularExpressions;
using Telegram.Bot;

namespace ITCoursesBot.ITCoursesBot.Controllers
{
    internal class BeginQuizController : BaseController
    {
        private readonly IQuestionsClient _questionsClient;

        public BeginQuizController(
            ITelegramBotClient bot,
            IMessageService messageService,
            ISessionManager sessionManager,
            IKeyboardBuilder keyboardBuilder,
            IQuestionsClient questionsClient   
        ) : base(bot, messageService, sessionManager, keyboardBuilder)
        {
            _questionsClient = questionsClient;
        }

        public override async Task<bool> HandleAsync(CancellationToken ct)
        {
            if (!CanHandle()) return false;

            var text = CurrentUpdate.Message?.Text;
            if (string.IsNullOrWhiteSpace(text)) return false;

            // Парсим строку вида "База21", "Продв32"
            if (!CourseRefParser.TryParse(text, out var @ref))
                return false;

            var course = @ref.Course;
            var block = @ref.Block;
            var lesson = @ref.Lesson;

            // Важно: передаём токен отмены
            var questions = await _questionsClient
                .GetLessonQuestionsAsync(course, block, lesson);

            if (questions.Count == 0)
            {
                await _messageService.SendTextAsync(
                    ChatId,
                    $"❗️ В курсе «{course}» блок {block}, урок {lesson} вопросов не найдено.",
                    ct: ct
                );
                return true;
            }

            var session = GetCurrentSessionById(ChatId)!;
            session.QuestionsForQuiz = questions
                .Select(q => new Question
                {
                    Id = q.Id,
                    LessonId = q.LessonId,
                    Text = q.Text
                })
                .ToList();
            session.Mode = BotMode.PassQuiz;

            await _messageService.SendTextAsync(
                ChatId,
                $"❓ Вопрос 1/{session.QuestionsForQuiz.Count}:\n" +
                session.QuestionsForQuiz[0].Text,
                replyMarkup: _keyboardBuilder.BuildBackToMenu(),
                ct: ct
            );

            return true;
        }

        public override bool CanHandle()
        {
            var session = GetCurrentSessionById(ChatId);
            return session != null
                && session.Mode == BotMode.BeginQuiz
                && !string.IsNullOrEmpty(CurrentUpdate?.Message?.Text);
        }
    }
}