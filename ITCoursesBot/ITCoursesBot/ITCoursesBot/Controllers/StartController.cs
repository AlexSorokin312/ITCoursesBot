using ITCoursesBot.Interfaces;
using Telegram.Bot;

namespace ITCoursesBot.ITCoursesBot.Controllers
{
    internal class StartController : BaseController
    {

        public StartController(ITelegramBotClient bot,
            IMessageService messageService,
            IKeyboardBuilder keyboardBuilder,
            ISessionManager sessionManager) : base(bot, messageService, sessionManager, keyboardBuilder)
        {

        }

        public override async Task<bool> HandleAsync(CancellationToken ct)
        {
            var canHandle = CanHandle();
            if (!canHandle)
                return false;

            var msg = CurrentUpdate.Message;

            try
            {
                await _messageService.SendTextAsync(
                    ChatId,
                    "Выберите опцию работы с чатом",
                    replyMarkup: _keyboardBuilder.Build());
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
