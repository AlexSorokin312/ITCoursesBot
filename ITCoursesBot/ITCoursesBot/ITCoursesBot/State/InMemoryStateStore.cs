using ITCoursesBot.Interfaces;
using System.Collections.Concurrent;

public class InMemoryStateStore : IUserStateStore
{
    private readonly ConcurrentDictionary<long, UserState> _dict = new();

    public Task<UserState?> GetAsync(long chatId)
        => Task.FromResult(_dict.TryGetValue(chatId, out var s) ? s : null);

    public Task SetAsync(long chatId, UserState state)
    {
        _dict[chatId] = state;
        return Task.CompletedTask;
    }

    public Task ClearAsync(long chatId)
    {
        _dict.TryRemove(chatId, out _);
        return Task.CompletedTask;
    }
}
