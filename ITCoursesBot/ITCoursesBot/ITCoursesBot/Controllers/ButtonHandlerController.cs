using ITCoursesBot.Interfaces;
using ITCoursesBot.ITCoursesBot.Configuration;
using System.Text;
using Telegram.Bot;

namespace ITCoursesBot.ITCoursesBot.Controllers
{
    public class ButtonHandlerController : BaseController
    {
        private readonly IOpenAIClient _openAi;
        private readonly OpenAISettings _aiSetting;

        private readonly UserProgressRepository _progressRepo;

        public ButtonHandlerController(ITelegramBotClient bot,
            IMessageService messageService,
            ISessionManager sessionManager,
            OpenAISettings openAISettings,
            IKeyboardBuilder keyboardBuilder,
            //UserProgressRepository progressRepo,
            IOpenAIClient openAi) : base(bot, messageService, sessionManager, keyboardBuilder)
        {
            //_progressRepo = progressRepo;
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

            if (buttonName == KeyboardBuilder.REWORK_BUTTON_NAME)
            {
                /*// 1) Получаем DTO вопросов с ошибками
                var badDtos = await _progressRepo.GetErrorQuestionDtosAsync(ChatId);
                if (badDtos.Count == 0)
                {
                    await _messageService.SendTextAsync(
                        ChatId,
                        "У вас нет вопросов с ошибками — нечего повторять! 🎉",
                        replyMarkup: _keyboardBuilder.Build()
                    );
                    return true;
                }

                // 2) Кладём DTO в сессию и переключаем режим PassQuiz
                session.QuestionsForQuiz = badDtos;
                session.QuestionIndex = 0;
                session.Mode = BotMode.PassQuiz;

                // 3) Отправляем первый вопрос
                var first = badDtos[0];
                await _messageService.SendTextAsync(
                    ChatId,
                    $"❓ Вопрос 1/{badDtos.Count}:\n{first.Text}",
                    replyMarkup: _keyboardBuilder.BuildBackToMenu()
                );
                return true;*/
            }

            if (buttonName == KeyboardBuilder.BACK_TO_MENU_BUTTON_NAME)
            {
                await _messageService.SendTextAsync(ChatId, "Выберите режим работы с чатом:", _keyboardBuilder.Build());
                session.Mode = BotMode.None;
                return true;

            }

            if (buttonName == KeyboardBuilder.USER_PROGRESS_BUTTON_NAME)
            {

                var courses = await _progressRepo.GetCourseProgressDetailedAsync(ChatId);
                var distribution = await _progressRepo.GetErrorDistributionByLessonAsync(ChatId);

                var sb = new StringBuilder();

                foreach (var c in courses)
                {
                    sb
                      .AppendLine($"🏅 **{c.CourseName}**")
                      .AppendLine($"Пройдено **{c.AnsweredQuestions}/{c.TotalQuestions}** вопросов")
                      .AppendLine($"• ✅ {c.CorrectFirstAttempt} — верно с 1-й попытки")
                      .AppendLine($"• ❌ {c.WithErrors} — с ошибками")
                      .AppendLine();

                    // теперь блок ошибок только для этого курса
                    sb.AppendLine("🏆 Распределение ошибок по урокам:");
                    var errs = distribution.Where(e => e.CourseName == c.CourseName).ToList();
                    if (errs.Count == 0)
                    {
                        sb.AppendLine("— нет ошибок в этом курсе —");
                    }
                    else
                    {
                        foreach (var e in errs)
                        {
                            var suffix = e.WrongCount % 10 == 1 && e.WrongCount % 100 != 11 ? "ка" : "ок";
                            sb.AppendLine($"• {e.LessonTitle} — {e.WrongCount} ошиб{suffix}");
                        }
                    }
                    sb.AppendLine();
                    sb.AppendLine(); // разделитель между курсами
                }

                await _messageService.SendTextAsync(ChatId, sb.ToString());

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
                    aiReply,
                    replyMarkup: _keyboardBuilder.Build()
                );
                return true;
            }
            return true;
        }
    }
}
