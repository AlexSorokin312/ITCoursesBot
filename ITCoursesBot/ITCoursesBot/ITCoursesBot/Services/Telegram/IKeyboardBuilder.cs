using Telegram.Bot.Types.ReplyMarkups;

public interface IKeyboardBuilder
{
    InlineKeyboardMarkup Build();
}

public class SimpleKeyboardBuilder : IKeyboardBuilder
{
    public InlineKeyboardMarkup Build() =>
        new InlineKeyboardMarkup(new[]
        {
            // Первый ряд
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📝 Вопросы по урокам", "questions"),
                InlineKeyboardButton.WithCallbackData("🎯 Тренажёр собеседований", "mock_interview")
            },
            // Второй ряд
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📊 Прогресс", "progress"),
                InlineKeyboardButton.WithCallbackData("💡 Объяснения кода", "code_explanations")
            },
            // Третий ряд
            new[]
            {
                InlineKeyboardButton.WithCallbackData("💬 Диалог", "dialog"),
                InlineKeyboardButton.WithCallbackData("⚙️ Мои настройки", "settings")
            }
        });
}
