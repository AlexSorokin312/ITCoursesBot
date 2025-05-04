using ITCoursesBot.Interfaces;
using ITCoursesBot.ITCoursesBot.Configuration;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ITCoursesBot.ITCoursesBot.Controllers
{
    public class ButtonHanlderController : BaseController
    {
        private readonly IUserStateStore _store;
        private readonly IOpenAIClient _openAi;
        private readonly OpenAISettings _aiSetting;

        private readonly UserProgressRepository _progressRepo;
        private readonly IQuizRepository _repo;

        public ButtonHanlderController(ITelegramBotClient bot,
            IMessageService messageService,
            ISessionManager sessionManager,
            OpenAISettings openAISettings,
            IKeyboardBuilder keyboardBuilder,
            UserProgressRepository progressRepo,
            IOpenAIClient openAi) : base(bot, messageService, sessionManager, keyboardBuilder)
        {
            _progressRepo = progressRepo;
            _openAi = openAi;
            _aiSetting = openAISettings;
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
            var buttonName = CurrentUpdate?.CallbackQuery?.Data;

            if (buttonName == KeyboardBuilder.BEGIN_QUIZ_BUTTON_NAME)
            {
                await _messageService.SendTextAsync(ChatId, "Введите номер урока:", _keyboardBuilder.BuildBackToMenu());
                session.Mode = BotMode.BeginQuiz;
                return true;

            }

            if (buttonName == KeyboardBuilder.CODE_EXPLANATION_BUTTON_NAME)
            {
                await _messageService.SendTextAsync(ChatId, "Включен режим «Объяснение кода», пришлите фрагмент — я объясню.");
                session.Mode = BotMode.CodeExplain;
                return true;

            }

            if (buttonName == KeyboardBuilder.BACK_TO_MENU_BUTTON_NAME)
            {
                await _messageService.SendTextAsync(ChatId, "Выберите режим работы с чатом:", _keyboardBuilder.Build());
                session.Mode = BotMode.None;
                return true;

            }

           if (buttonName == KeyboardBuilder.USER_PROGRESS_BUTTON_NAME)
            {

                // 1) Получаем всю статистику
                var summary = await _progressRepo.GetProgressSummaryAsync(ChatId);
                var distribution = await _progressRepo.GetErrorDistributionByLessonAsync(ChatId);

                // 2) Формируем сообщение
                var sb = new StringBuilder()
                    .AppendLine("🏅 Пройдено " +
                                $"{summary.AnsweredQuestions}/{summary.TotalQuestions} вопросов")
                    .AppendLine($"Из {summary.AnsweredQuestions} вопросов:")
                    .AppendLine($"• {summary.CorrectFirstAttempt} — решены верно с первой попытки")
                    .AppendLine($"• {summary.WithErrors} — решены с ошибками")
                    .AppendLine()
                    .AppendLine("🏆 Распределение ошибок по урокам:");

                if (distribution.Count == 0)
                {
                    sb.AppendLine("— пока нет ошибок —");
                }
                else
                {
                    foreach (var e in distribution)
                    {
                        // склонение слова «ошибка»
                        var suffix = e.WrongCount % 10 == 1 && e.WrongCount % 100 != 11
                                     ? "ка"
                                     : "ок";
                        sb.AppendLine($"• {e.LessonTitle} — {e.WrongCount} ошиб{suffix}");
                    }
                }

                // 3) Отправляем и завершаем
                await _messageService.SendTextAsync(
                    ChatId,
                    sb.ToString()
                );

                // 1) Получаем списки вопросов
                var okQs = await _progressRepo.GetQuestionsAnsweredWithoutErrorsAsync(ChatId);
                var badQs = await _progressRepo.GetQuestionsWithErrorsAsync(ChatId);

                // 2) Собираем prompt для OpenAI
                var sbPrompt = new StringBuilder()
                    .AppendLine("Пользователь на текущий момент ответил без ошибок на такие вопросы:")
                    .AppendLine(string.Join("\n", okQs.Select((t, i) => $"{i + 1}. {t}")))
                    .AppendLine()
                    .AppendLine("В этих вопросах совершил ошибки:")
                    .AppendLine(string.Join("\n", badQs.Select((t, i) => $"{i + 1}. {t}")));

                string userMessage = sbPrompt.ToString();

                // 3) Зовём OpenAI с системной инструкцией из appsettings.json
                string aiReply = await _openAi.GetChatResponseAsync(
                    _aiSetting.InstructionsProgressSummary,
                    userMessage
                );

                // 4) Отправляем результат отдельно
                await _messageService.SendTextAsync(
                    ChatId,
                    aiReply
                );
                return true;
            }
            return true;
        }
    }
}
