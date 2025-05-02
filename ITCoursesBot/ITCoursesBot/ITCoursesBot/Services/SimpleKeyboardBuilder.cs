using ITCoursesBot.Interfaces;
using Telegram.Bot.Types.ReplyMarkups;

public class SimpleKeyboardBuilder : IKeyboardBuilder
{
    public InlineKeyboardMarkup Build() =>
        new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📝 Вопросы по урокам",      "questions"),
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
        // Если есть следующий вопрос — показываем «Следующий вопрос», иначе только «Завершить»
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
