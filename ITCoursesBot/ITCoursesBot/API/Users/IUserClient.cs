using Refit;

public interface IUserClient
{
    Task<UserDto?> GetAsync(long telegramId, CancellationToken ct = default);
    Task<UserDto?> GetOrCreateAsync([Body] CreateUserDto dto, CancellationToken ct = default);
    Task CreateAsync(CreateUserDto dto, CancellationToken ct = default);
    Task DeleteAsync(long telegramId, CancellationToken ct = default);
}
