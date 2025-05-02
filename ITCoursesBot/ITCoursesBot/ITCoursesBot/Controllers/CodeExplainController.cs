using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ITCoursesBot.ITCoursesBot.Controllers
{
    public class CodeExplainController : BaseController
    {
        public CodeExplainController(ITelegramBotClient bot) : base(bot)
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
