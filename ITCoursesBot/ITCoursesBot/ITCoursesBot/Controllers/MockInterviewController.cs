
using Telegram.Bot;

namespace ITCoursesBot.ITCoursesBot.Controllers
{
    public class MockInterviewController : BaseController
    {
        public MockInterviewController(ITelegramBotClient bot) : base(bot)
        {
        }

        public override Task<bool> HandleAsync(CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }
}
