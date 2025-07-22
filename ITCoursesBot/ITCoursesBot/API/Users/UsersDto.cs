public sealed record UserDto(long TelegramId, string Name, DateTime RegisteredAt);
public sealed record CreateUserDto(long TelegramId, string Name);