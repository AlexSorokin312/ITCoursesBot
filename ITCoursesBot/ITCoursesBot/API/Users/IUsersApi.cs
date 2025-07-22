using Refit;

public interface IUsersApi : IUserClient
{
    [Get("/api/users/{telegramId}")]
    new Task<UserDto?> GetAsync(long telegramId, CancellationToken ct);

    [Put("/api/users")]                            
    Task<UserDto> GetOrCreateAsync([Body] CreateUserDto dto,
                               CancellationToken ct = default);
    [Post("/api/users")]
    new Task CreateAsync([Body] CreateUserDto dto, CancellationToken ct);

    [Delete("/api/users/{telegramId}")]
    new Task DeleteAsync(long telegramId, CancellationToken ct);
}