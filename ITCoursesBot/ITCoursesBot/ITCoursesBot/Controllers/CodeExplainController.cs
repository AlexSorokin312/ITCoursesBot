using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;

namespace ITCoursesBot.ITCoursesBot.Controllers
{
    public class CodeExplainController : BaseController
    {
        public CodeExplainController(ITelegramBotClient bot) : base(bot)
        {
        }

        public override Task<bool> HandleAsync(CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }
}
