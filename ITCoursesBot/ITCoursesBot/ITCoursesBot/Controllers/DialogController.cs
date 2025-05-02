using Telegram.Bot;

namespace ITCoursesBot.ITCoursesBot.Controllers
{
    public class DialogController : BaseController
    {
        public DialogController(ITelegramBotClient bot) : base(bot)
        {
        }

        public override Task<bool> HandleAsync(CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }
}
