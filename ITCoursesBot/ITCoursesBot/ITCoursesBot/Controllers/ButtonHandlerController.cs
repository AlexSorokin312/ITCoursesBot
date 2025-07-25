using Bot.Ports;
using ITCoursesBot.Interfaces;
using ITCoursesBot.ITCoursesBot.Configuration;
using System.Text;
using Telegram.Bot;

namespace ITCoursesBot.ITCoursesBot.Controllers
{
    public class ButtonHandlerController : BaseController
    {
        private readonly IOpenAIClient _openAi;
        private readonly OpenAISettings _openAISettings;

        private readonly IProgressClient _progressClient;

        public ButtonHandlerController(ITelegramBotClient bot,
            IMessageService messageService,
            ISessionManager sessionManager,
            OpenAISettings openAISettings,
            IKeyboardBuilder keyboardBuilder,
            IProgressClient userProgress,
            IOpenAIClient openAi) : base(bot, messageService, sessionManager, keyboardBuilder)
        {
            _progressClient = userProgress;
            _openAi = openAi;
            _openAISettings = openAISettings;
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
                // 1) Получаем DTO «провальных» вопросов
                var badQuestionDtos = await _progressClient
                    .GetIncorrectQuestionsAsync(ChatId, ct);

                // 2) Если ни одного — говорим, что нечего повторять
                if (badQuestionDtos.Count == 0)
                {
                    await _messageService.SendTextAsync(
                        ChatId,
                        "У вас нет вопросов с ошибками — нечего повторять! 🎉",
                        replyMarkup: _keyboardBuilder.Build()
                    );
                    return true;
                }

                // 3) Мапим QuestionDto → Question (для сессии)
                var badQuestions = badQuestionDtos
                    .Select(dto => new Question
                    {
                        Id = dto.Id,
                        LessonId = dto.LessonId,
                        Text = dto.Text
                    })
                    .ToList();

                // 4) Кладём их в сессию и переключаем режим на PassQuiz
                session = _sessionManager.GetOrCreateSession(ChatId);
                session.QuestionsForQuiz = badQuestions;
                session.QuestionIndex = 0;
                session.Mode = BotMode.PassQuiz;

                // 5) Отправляем первый вопрос
                await _messageService.SendTextAsync(
                    ChatId,
                    $"❓ Вопрос 1/{badQuestions.Count}:\n{badQuestions[0].Text}",
                    replyMarkup: _keyboardBuilder.BuildBackToMenu()
                );

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
                // 1) Получаем сводную статистику по курсам
                var courses = await _progressClient
                    .GetAllCourseProgressAsync(ChatId, ct);

                var sb = new StringBuilder();

                foreach (var c in courses)
                {
                    int totalDone = c.CorrectCount + c.IncorrectCount;
                    int total = totalDone + c.UnansweredCount;

                    sb
                      .AppendLine($"🏅 **{c.Course}**")
                      .AppendLine($"Пройдено **{totalDone}/{total}** вопросов")
                      .AppendLine($"• ✅ {c.CorrectCount} — верно")
                      .AppendLine($"• ❌ {c.IncorrectCount} — с ошибками")
                      .AppendLine($"• 💤 **{c.UnansweredCount}** — не отвечено")
                      .AppendLine();

                    // Распределение ошибок по урокам
                    var errors = await _progressClient
                        .GetLessonErrorStatsAsync(ChatId, c.Course, ct);

                    sb.AppendLine("🏆 Распределение ошибок по урокам:");
                    if (errors.Count == 0)
                    {
                        sb.AppendLine("— нет ошибок в этом курсе —");
                    }
                    else
                    {
                        foreach (var e in errors)
                        {
                            var suffix = e.WrongCount % 10 == 1 && e.WrongCount % 100 != 11
                                         ? "ка"
                                         : "ок";
                            sb.AppendLine($"• {e.LessonName} — {e.WrongCount} ошиб{suffix}");
                        }
                    }

                    sb.AppendLine();
                }

                // 2) Списки вопросов для OpenAI‑саммари
                // (требуются два новых метода в IProgressClient)
                var okQs = await _progressClient.GetCorrectQuestionsAsync(ChatId, ct);
                var badQs = await _progressClient.GetIncorrectQuestionsAsync(ChatId, ct);

                // 3) Формируем prompt
                var sbPrompt = new StringBuilder()
                    .AppendLine("Пользователь без ошибок ответил на вопросы:")
                    .AppendLine(string.Join("\n", okQs.Select((t, i) => $"{i + 1}. {t}")))
                    .AppendLine()
                    .AppendLine("Пользователь с ошибками ответил на вопросы:")
                    .AppendLine(string.Join("\n", badQs.Select((t, i) => $"{i + 1}. {t}")));

                // 3) Отправляем первое сообщение с прогрессом
                await _messageService.SendTextAsync(
                    ChatId,
                    sb.ToString()
                );

                // 4) Запрашиваем саммари у OpenAI
                var aiReply = await _openAi.GetChatResponseAsync(
                    _openAISettings.InstructionsProgressSummary,
                    sbPrompt.ToString());

                // 5) Отправляем ответ
                await _messageService.SendTextAsync(
                    ChatId,
                    aiReply,
                    replyMarkup: _keyboardBuilder.Build()
                );

                return true;
            }
            return false;
        }
    }
}
