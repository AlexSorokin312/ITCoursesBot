using ITCoursesBot.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ITCoursesBot.ITCoursesBot.Controllers
{
    public class DialogController : BaseController
    {
        public DialogController(ITelegramBotClient bot,
            IMessageService messageService,
            IKeyboardBuilder keyboardBuilder,
            ISessionManager sessionManager) : base(bot, messageService, sessionManager, keyboardBuilder)
        {
        }

        public override bool CanHandle()
        {
            return false;
        }

        public override Task<bool> HandleAsync(CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }
}
