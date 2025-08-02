
public static class UpdateContextExtensions
{
    public static long GetUserId(this UpdateContext ctx)
        => ctx.Update.CallbackQuery?.From.Id
         ?? ctx.Update.Message?.From?.Id
         ?? throw new InvalidOperationException("Нет поля From");

    public static long GetChatId(this UpdateContext ctx)
        => ctx.Update.CallbackQuery?.Message.Chat.Id
         ?? ctx.Update.Message.Chat.Id;
}
