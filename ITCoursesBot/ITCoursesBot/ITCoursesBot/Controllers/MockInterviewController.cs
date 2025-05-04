using ITCoursesBot.Interfaces;
using Telegram.Bot;

namespace ITCoursesBot.ITCoursesBot.Controllers
{
    public class MockInterviewController : BaseController
    {
        public MockInterviewController(ITelegramBotClient bot, IMessageService messageService, ISessionManager sessionManager, IKeyboardBuilder keyboardBuilder) : base(bot, messageService, sessionManager, keyboardBuilder)
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
