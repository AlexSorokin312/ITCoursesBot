using Telegram.Bot.Types.ReplyMarkups;

public interface IKeyboardBuilder
{
    InlineKeyboardMarkup Build();
}

public class SimpleKeyboardBuilder : IKeyboardBuilder
{
    public InlineKeyboardMarkup Build() =>
        new(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Кнопка 1", "btn1"),
                InlineKeyboardButton.WithCallbackData("Кнопка 2", "btn2")
            }
        });
}