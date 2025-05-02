namespace ITCoursesBot.Interfaces
{
    public interface IUserStateStore
    {
        Task<UserState?> GetAsync(long chatId);
        Task SetAsync(long chatId, UserState state);
        Task ClearAsync(long chatId);
    }
}