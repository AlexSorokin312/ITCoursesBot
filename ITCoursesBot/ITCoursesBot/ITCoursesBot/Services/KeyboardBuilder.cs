using ITCoursesBot.Interfaces;
using Telegram.Bot.Types.ReplyMarkups;

public class KeyboardBuilder : IKeyboardBuilder
{
    public const string BEGIN_QUIZ_BUTTON_NAME = "begin_quiz";
    public const string BACK_TO_MENU_BUTTON_NAME = "back_to_menu";
    public const string DIALOG_BUTTON_NAME = "dialog";

    public const string NEXT_QUESTION_BUTTON_NAME = "next_question";
    public const string FINISH_BUTTON_NAME = "finish";
    public const string SHOW_ALL_BUTTON_NAME = "show_all";

    public const string CODE_EXPLANATION_BUTTON_NAME = "code_explanations";
    public const string USER_PROGRESS_BUTTON_NAME = "progress";
    public const string REWORK_BUTTON_NAME = "rework";
    public const string MOCK_INTERVIEW_BUTTON_NAME = "mock_interview";
    public const string PROMO_CODES_BUTTON_NAME = "promo_codes"; // Новая кнопка

    public InlineKeyboardMarkup Build() =>
        new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📝 Вопросы по урокам",      BEGIN_QUIZ_BUTTON_NAME),
                InlineKeyboardButton.WithCallbackData("🎯 Тренажёр собеседований", MOCK_INTERVIEW_BUTTON_NAME)
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📊 Прогресс",             USER_PROGRESS_BUTTON_NAME),
                InlineKeyboardButton.WithCallbackData("💡 Объяснения кода",      CODE_EXPLANATION_BUTTON_NAME)
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("💬 Диалог",               DIALOG_BUTTON_NAME),
                InlineKeyboardButton.WithCallbackData("📝 Работа над ошибками",   REWORK_BUTTON_NAME)
            },
            new[] // Новый ряд для промокодов
            {
                InlineKeyboardButton.WithCallbackData("🎟️ Промокоды",            PROMO_CODES_BUTTON_NAME)
            }
        });

    public InlineKeyboardMarkup BuildNextFinish(bool more)
    {
        var buttons = new List<InlineKeyboardButton>();
        if (more)
            buttons.Add(InlineKeyboardButton.WithCallbackData("➡️ Следующий вопрос", NEXT_QUESTION_BUTTON_NAME));

        buttons.Add(InlineKeyboardButton.WithCallbackData("🏁 Завершить", FINISH_BUTTON_NAME));

        return new InlineKeyboardMarkup(new[] { buttons.ToArray() });
    }

    public InlineKeyboardMarkup BuildBackToMenu()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🏠 В главное меню", BACK_TO_MENU_BUTTON_NAME)
            }
        });
    }
}