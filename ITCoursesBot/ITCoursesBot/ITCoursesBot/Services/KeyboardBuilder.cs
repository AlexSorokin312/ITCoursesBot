using ITCoursesBot.Interfaces;
using Telegram.Bot.Types.ReplyMarkups;

public class KeyboardBuilder : IKeyboardBuilder
{
    public const string BEGIN_QUIZ_BUTTON_NAME = "begin_quiz";
    public InlineKeyboardMarkup Build() =>
        new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📝 Вопросы по урокам",      "begin_quiz"),
                InlineKeyboardButton.WithCallbackData("🎯 Тренажёр собеседований","mock_interview")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📊 Прогресс",            "progress"),
                InlineKeyboardButton.WithCallbackData("💡 Объяснения кода",      "code_explanations")
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
                InlineKeyboardButton.WithCallbackData("🏠 В главное меню", "start_over")
            }
        });
    }
}
