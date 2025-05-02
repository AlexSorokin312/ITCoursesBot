using ITCoursesBot.Interfaces;
using Telegram.Bot;

namespace ITCoursesBot.ITCoursesBot.Controllers
{
    internal class StartController : BaseController
    {
        private IKeyboardBuilder _keyboardBuilder;
        public StartController(ITelegramBotClient bot,
            IMessageService messageService,
            IKeyboardBuilder keyboardBuilder,
            ISessionManager sessionManager) : base(bot, messageService, sessionManager)
        {
            _keyboardBuilder = keyboardBuilder;
        }

        public override async Task<bool> HandleAsync(CancellationToken ct)
        {
            var canHandle = CanHandle();
            if (!canHandle)
                return false;

            var msg = CurrentUpdate.Message;

            await _messageService.SendTextAsync(
                ChatId,
                "Выберите опцию работы с чатом",
                replyMarkup: _keyboardBuilder.Build(),
                cancellationToken: ct);
            _sessionManager.GetOrCreateSession(ChatId);
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
