using ITCoursesBot.Interfaces;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ITCoursesBot.ITCoursesBot.Controllers
{
    public class ButtonHanlderController : BaseController
    {
        private readonly IUserStateStore _store;
        private readonly IOpenAIClient _ai;
        private readonly UserProgressRepository _progressRepo;
        private readonly IQuizRepository _repo;

        public ButtonHanlderController(ITelegramBotClient bot,
            IMessageService messageService,
            ISessionManager sessionManager,
            IKeyboardBuilder keyboardBuilder,
            UserProgressRepository progressRepo) : base(bot, messageService, sessionManager, keyboardBuilder)
        {
            _progressRepo = progressRepo;
        }

        public override bool CanHandle()
        {
            var session = GetCurrentSessionById(ChatId);
            if (session == null)
                return false;

            if (CurrentUpdate == null)
                return false;

            var butttonName = CurrentUpdate?.CallbackQuery?.Data;

            if (string.IsNullOrEmpty(butttonName))
                return false;   

            return true;
        }

        public override async Task<bool> HandleAsync(CancellationToken ct)
        {
            var session = GetCurrentSessionById(ChatId);
            var butttonName = CurrentUpdate?.CallbackQuery?.Data;

            if (butttonName == KeyboardBuilder.BEGIN_QUIZ_BUTTON_NAME)
            {
                await _messageService.SendTextAsync(ChatId, "Введите номер урока:", _keyboardBuilder.BuildBackToMenu());
                session.Mode = BotMode.BeginQuiz;
                return true;

            }

            if (butttonName == KeyboardBuilder.CODE_EXPLANATION_BUTTON_NAME)
            {
                await _messageService.SendTextAsync(ChatId, "Включен режим «Объяснение кода», пришлите фрагмент — я объясню.");
                session.Mode = BotMode.CodeExplain;
                return true;

            }

            if (butttonName == KeyboardBuilder.BACK_TO_MENU_BUTTON_NAME)
            {
                await _messageService.SendTextAsync(ChatId, "Выберите режим работы с чатом:", _keyboardBuilder.Build());
                session.Mode = BotMode.None;
                return true;

            }

            if (butttonName == KeyboardBuilder.USER_PROGRESS_BUTTON_NAME)
            {
                if (!CanHandle())
                    return false;

                long userId = ChatId;

                var never = await _progressRepo.GetNeverAnsweredCorrectlyAsync(userId);
                var tricky = await _progressRepo.GetMoreWrongThanRightAsync(userId);
                var worst = await _progressRepo.GetLessonsWithMostErrorsAsync(userId, 3);
                var covered = await _progressRepo.GetFullyCoveredLessonsAsync(userId);

                var sb = new StringBuilder("📊 *Ваш прогресс*\n\n")
                    .AppendLine($"❌ Вопросов без правильного ответа: *{never.Count}*")
                    .AppendLine($"⚖️  Вопросов, где ошибок больше, чем удачных ответов: *{tricky.Count}*")
                    .AppendLine("\n🏆 Топ уроков по количеству ошибок:");

                foreach (var l in worst)
                    sb.AppendLine($"• {l.LessonTitle} — {l.WrongAnswers} (ошибки − правильные)");

                sb.AppendLine("\n✅ Уроки, где вы ответили хотя бы раз на все вопросы:");
                if (covered.Any())
                {
                    foreach (var l in covered)
                        sb.AppendLine($"• {l.LessonTitle} ({l.TotalQuestions} вопросов)");
                }
                else
                {
                    sb.AppendLine("— пока нет —");
                }

                // Отправляем пользователю и возвращаем true
                await _messageService.SendTextAsync(ChatId, sb.ToString());
                return true;
            }
            return true;
        }
    }
}
