// Controllers/QuizController.cs
using ITCoursesBot.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ITCoursesBot.ITCoursesBot.Controllers
{
    public class QuizController : BaseController
    {
        private readonly IUserStateStore _store;
        private readonly IMessageService _msg;
        private readonly IOpenAIClient _ai;
        private readonly IQuestionRepository _repo;

        public QuizController(
            ITelegramBotClient bot,
            IUserStateStore store,
            IMessageService msg,
            IOpenAIClient ai,
            IQuestionRepository repo
        ) : base(bot)
        {
            _store = store;
            _msg = msg;
            _ai = ai;
            _repo = repo;
        }

        public override async Task<bool> HandleAsync(CancellationToken ct)
        {
            // 0. Загружаем или создаём состояние пользователя
            var state = await _store.GetAsync(ChatId) ?? new UserState();

            // 1) Пользователь нажал кнопку "Вопросы к урокам"
            if (Update.CallbackQuery?.Data == "questions")
            {
                state.Mode = BotMode.Questions;
                await _msg.SendTextAsync(
                    chatId: ChatId,
                    text: "Введите номер урока:",
                    cancellationToken: ct
                );
                await _store.SetAsync(ChatId, state);
                return true;
            }

            // 2) Ждём номер урока
            if (state.Mode == BotMode.Questions
                && state.AwaitLessonNumber
                && Update.Message?.Text is string lessonId)
            {
                var questions =  _repo.GetQuestionsByLessonAsync(lessonId, ct);
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
                && (Update.Message?.Text != null || Update.Message?.Voice != null))
            {
                // Если голос — преобразуем в текст. Здесь пример, реальную логику распознавания вставьте сами
                var userText = Update.Message.Text
                               ?? await DownloadVoiceAsTextAsync(Update.Message.Voice!, ct);

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
            return false;
        }

        private Task SendCurrentQuestionAsync(UserState state, CancellationToken ct)
            => _msg.SendTextAsync(
                chatId: ChatId,
                text: $"❓ Вопрос {state.Index + 1}/{state.Questions!.Count}:\n{state.Questions[state.Index]}",
                cancellationToken: ct
            );

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
