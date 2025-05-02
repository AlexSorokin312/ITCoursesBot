namespace ITCoursesBot.Interfaces
{
    public interface ITelegramBotService
    {
        Task StartAsync(CancellationToken cancellationToken);
    }
}
