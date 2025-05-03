using ITCoursesBot.Interfaces;
using ITCoursesBot.ITCoursesBot.Services;
using Telegram.Bot;

namespace ITCoursesBot.ITCoursesBot.Controllers
{
    internal class StartController : BaseController
    {
        public UserDbRepository _userDbRepository;
        public StartController(ITelegramBotClient bot,
            IMessageService messageService,
            IKeyboardBuilder keyboardBuilder,
            ISessionManager sessionManager,
            UserDbRepository userDbRepository) : base(bot, messageService, sessionManager, keyboardBuilder)
        {
            _userDbRepository = userDbRepository;
        }

        public override async Task<bool> HandleAsync(CancellationToken ct)
        {
            if (!CanHandle()) return false;

            var msg = CurrentUpdate.Message!;        
            var from = msg.From!;                   

            try
            {
                // передаём в репозиторий нужные аргументы
                var user = await _userDbRepository.AddOrGetAsync(
                    telegramId: from.Id,
                    username: from.Username ?? string.Empty
                );

                await _messageService.SendTextAsync(
                    ChatId,
                    "Выберите опцию работы с чатом",
                    replyMarkup: _keyboardBuilder.Build()
                );

                _sessionManager.GetOrCreateSession(ChatId);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return true;
        }

        public override bool CanHandle()
        {
            if (CurrentUpdate == null)
                return false;

            if (CurrentUpdate.Message == null)
                return false;

            var content = CurrentUpdate.Message.Text;

            if (content != "/start")
                return false;

            return true;
        }
    }
}
