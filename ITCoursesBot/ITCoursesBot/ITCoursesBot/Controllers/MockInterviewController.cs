
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ITCoursesBot.ITCoursesBot.Controllers
{
    public class MockInterviewController : BaseController
    {
        public MockInterviewController(ITelegramBotClient bot) : base(bot)
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
