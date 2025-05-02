using Telegram.Bot.Types.ReplyMarkups;

namespace ITCoursesBot.Interfaces
{
    public interface IKeyboardBuilder
    {
        InlineKeyboardMarkup Build();

        /// <summary>
        /// Кнопки «Следующий вопрос» (если more=true) и «Завершить»
        /// </summary>
        InlineKeyboardMarkup BuildNextFinish(bool more);

        /// <summary>
        /// Кнопка «В главное меню»
        /// </summary>
        InlineKeyboardMarkup BuildBackToMenu();
    }
}

