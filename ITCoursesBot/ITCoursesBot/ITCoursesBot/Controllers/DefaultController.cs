using ITCoursesBot.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ITCoursesBot.ITCoursesBot.Controllers
{
    public class DefaultController : BaseController
    {

        public DefaultController(ITelegramBotClient bot,
            IMessageService messageService,
            IKeyboardBuilder keyboardBuilder,
            ISessionManager sessionManager) : base(bot, messageService, sessionManager, keyboardBuilder)
        {

        }

        public override bool CanHandle()
        {
            var session = GetCurrentSessionById(ChatId);
            if (session == null)
                return false;

            if (session.Mode != BotMode.None)
                return false;

            return true;
        }

        public override async Task<bool> HandleAsync(CancellationToken ct)
        {
            await _messageService.SendTextAsync(ChatId, "Выберите опцию работы с чатом:", _keyboardBuilder.Build());
            return true;
        }
    }
}
