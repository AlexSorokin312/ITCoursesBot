using ITCoursesBot.Interfaces;
using Telegram.Bot.Types.ReplyMarkups;

public class KeyboardBuilder : IKeyboardBuilder
{
    public const string BEGIN_QUIZ_BUTTON_NAME = "begin_quiz";
    public const string BACK_TO_MENU_BUTTON_NAME = "back_to_menu";

    public const string CODE_EXPLANATION_BUTTON_NAME = "code_explanations";
    public const string USER_PROGRESS_BUTTON_NAME = "progress";
    public InlineKeyboardMarkup Build() =>
        new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📝 Вопросы по урокам",      BEGIN_QUIZ_BUTTON_NAME),
                InlineKeyboardButton.WithCallbackData("🎯 Тренажёр собеседований","mock_interview")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📊 Прогресс",             USER_PROGRESS_BUTTON_NAME),
                InlineKeyboardButton.WithCallbackData("💡 Объяснения кода",      CODE_EXPLANATION_BUTTON_NAME)
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("💬 Диалог",              "dialog"),
                InlineKeyboardButton.WithCallbackData("⚙️ Мои настройки",       "settings")
            }
        });

    public InlineKeyboardMarkup BuildNextFinish(bool more)
    {
        var buttons = new List<InlineKeyboardButton>();
        if (more)
        {
            buttons.Add(InlineKeyboardButton.WithCallbackData("➡️ Следующий вопрос", "next_question"));
        }
        buttons.Add(InlineKeyboardButton.WithCallbackData("🏁 Завершить", "finish"));

        return new InlineKeyboardMarkup(new[]
        {
            buttons.ToArray()
        });
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
