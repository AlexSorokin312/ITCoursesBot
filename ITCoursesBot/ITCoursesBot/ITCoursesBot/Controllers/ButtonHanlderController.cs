using ITCoursesBot.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ITCoursesBot.ITCoursesBot.Controllers
{
    public class ButtonHanlderController : BaseController
    {
        private readonly IUserStateStore _store;
        private readonly IOpenAIClient _ai;
        private readonly IQuizRepository _repo;

        public ButtonHanlderController(ITelegramBotClient bot,
            IMessageService messageService,
            ISessionManager sessionManager,
            IKeyboardBuilder keyboardBuilder) : base(bot, messageService, sessionManager, keyboardBuilder)
        {

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
                await _messageService.SendTextAsync(ChatId, "Введите номер урока:", _keyboardBuilder.BuildBackToMenu(), cancellationToken: ct);
                session.Mode = BotMode.BeginQuiz;
                return true;

            }

            if (butttonName == KeyboardBuilder.CODE_EXPLANATION_BUTTON_NAME)
            {
                await _messageService.SendTextAsync(ChatId, "Включен режим «Объяснение кода», пришлите фрагмент — я объясню.", cancellationToken: ct);
                session.Mode = BotMode.CodeExplain;
                return true;

            }

            if (butttonName == KeyboardBuilder.BACK_TO_MENU_BUTTON_NAME)
            {
                await _messageService.SendTextAsync(ChatId, "Выберите режим работы с чатом:", _keyboardBuilder.Build(), cancellationToken: ct);
                session.Mode = BotMode.None;
                return true;

            }


            return false;


          /* if (session.AwaitLessonNumber && CurrentUpdate.Message?.Text is string lessonId)
            {
                var questions = _repo.GetQuestionsByLessonAsync(lessonId, ct);
                if (questions == null || questions.Count == 0)
                {
                    await _msg.SendTextAsync(
                        chatId: ChatId,
                        text: "Вопросов для этого урока не найдено. Попробуйте другой номер.",
                        cancellationToken: ct
                    );
                    // остаёмся в том же режиме и ждём нового ввода
                    await _store.SetAsync(ChatId, state);
                    return true;
                }

                // Инициализируем викторину
                state.LessonId = lessonId;
                state.Questions = questions;
                state.Index = 0;
                await _store.SetAsync(ChatId, state);

                await SendCurrentQuestionAsync(state, ct);
                return true;
            }

            // 3) Обрабатываем ответ на текущий вопрос
            if (state.Mode == BotMode.Questions
                && state.AwaitAnswer
                && (CurrentUpdate.Message?.Text != null || CurrentUpdate.Message?.Voice != null))
            {
                // Если голос — преобразуем в текст. Здесь пример, реальную логику распознавания вставьте сами
                var userText = CurrentUpdate.Message.Text
                               ?? await DownloadVoiceAsTextAsync(CurrentUpdate.Message.Voice!, ct);

                var question = state.Questions![state.Index];
                var eval = await _ai.EvaluateAsync(question, userText, ct);

                // 3.1 Сразу отправляем комментарий от AI
                await _msg.SendTextAsync(
                    chatId: ChatId,
                    text: eval.Comment,
                    cancellationToken: ct
                );

                if (eval.IsCorrect)
                {
                    state.Index++;
                    await _store.SetAsync(ChatId, state);

                    if (state.Index < state.Questions.Count)
                    {
                        await SendCurrentQuestionAsync(state, ct);
                    }
                    else
                    {
                        await _msg.SendTextAsync(
                            chatId: ChatId,
                            text: "Поздравляю! Тест завершён. Для нового теста нажмите /start.",
                            cancellationToken: ct
                        );
                        await _store.ClearAsync(ChatId);
                    }
                }
                else
                {
                    await _msg.SendTextAsync(
                        chatId: ChatId,
                        text: "Неправильно, попробуйте ещё раз.",
                        cancellationToken: ct
                    );
                }

                return true;
            }

            // Если ни одно условие не сработало — пропускаем апдейт дальше
            return false;*/
        }

      /*  private Task SendCurrentQuestionAsync(UserState state, CancellationToken ct)
            => _msg.SendTextAsync(
                chatId: ChatId,
                text: $"❓ Вопрос {state.Index + 1}/{state.Questions!.Count}:\n{state.Questions[state.Index]}",
                cancellationToken: ct
            );*/

        private async Task<string> DownloadVoiceAsTextAsync(Voice voice, CancellationToken ct)
        {
            // 1. Получаем информацию о файле
            var file = await Bot.GetFile(voice.FileId, ct);
            // :contentReference[oaicite:0]{index=0}

            // 2. Скачиваем файл в память
            await using var ms = new MemoryStream();
            await Bot.DownloadFile(file.FilePath!, ms, cancellationToken: ct);
            ms.Position = 0;

            // 3. Здесь можно передать ms в вашу систему распознавания речи
            //    например, в Google Speech, Azure Speech или иной движок.

            return "[распознанный текст]";
        }
    }
}
