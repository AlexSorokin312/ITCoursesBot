namespace ITCoursesBot.ITCoursesBot.Services.Telegram
{
    public interface ITelegramBotService
    {
        Task StartAsync(CancellationToken cancellationToken);
    }
}
