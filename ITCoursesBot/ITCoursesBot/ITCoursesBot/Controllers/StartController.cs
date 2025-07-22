using ITCoursesBot.Interfaces;
using Telegram.Bot;

namespace ITCoursesBot.ITCoursesBot.Controllers
{
    internal class StartController : BaseController
    {
        private readonly IUserClient _usersClient;
        private readonly IMessageService _messageService;
        private readonly ISessionManager _sessionManager;
        private readonly IKeyboardBuilder _keyboardBuilder;

        public StartController(
            ITelegramBotClient bot,
            IMessageService messageService,
            IKeyboardBuilder keyboardBuilder,
            ISessionManager sessionManager,
            IUserClient usersClient
        ) : base(bot, messageService, sessionManager, keyboardBuilder)
        {
            _usersClient = usersClient;
            _messageService = messageService;
            _sessionManager = sessionManager;
            _keyboardBuilder = keyboardBuilder;
        }

        public override async Task<bool> HandleAsync(CancellationToken ct)
        {
            if (!CanHandle())
                return false;

            var msg = CurrentUpdate.Message!;
            var from = msg.From!;

            // 1) Создать или получить пользователя через REST‑API
            UserDto user;
            try
            {
                user = await _usersClient.GetOrCreateAsync(
                    new CreateUserDto(
                        TelegramId: from.Id,
                        Name: from.Username ?? from.FirstName ?? "Guest"
                    ),
                    ct
                );
            }
            catch (Exception ex)
            {
                // если API недоступно или другая ошибка, сообщаем и выходим
                await _messageService.SendTextAsync(
                    ChatId,
                    "❌ Не удалось зарегистрировать пользователя. Попробуйте позже.",
                    ct: ct
                );
                Console.WriteLine($"[StartController] Не удалось создать/получить user: {ex}");
                return true;
            }

            // 2) Отправляем главное меню
            await _messageService.SendTextAsync(
                ChatId,
                $"👋 Привет, {user.Name}! Выберите опцию работы с чатом:",
                replyMarkup: _keyboardBuilder.Build(),
                ct: ct
            );

            // 3) Создаём новую сессию или возвращаем существующую
            _sessionManager.GetOrCreateSession(ChatId);

            return true;
        }

        public override bool CanHandle()
        {
            if (CurrentUpdate?.Message?.Text is string txt)
            {
                return txt.Trim().Equals("/start", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
    }
}
