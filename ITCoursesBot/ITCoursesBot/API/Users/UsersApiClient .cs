public sealed class UsersApiClient : IUserClient
{
    private readonly IUsersApi _api;
    public UsersApiClient(IUsersApi api) => _api = api;

    public Task<UserDto?> GetAsync(long id, CancellationToken ct = default) =>
        _api.GetAsync(id, ct);

    public Task CreateAsync(CreateUserDto dto, CancellationToken ct = default) =>
        _api.CreateAsync(dto, ct);

    public Task DeleteAsync(long id, CancellationToken ct = default) =>
        _api.DeleteAsync(id, ct);

    public Task<UserDto> GetOrCreateAsync(CreateUserDto dto, CancellationToken ct = default) =>
    _api.GetOrCreateAsync(dto, ct);
}