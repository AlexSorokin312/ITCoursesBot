using ITCoursesBot.Interfaces;
using ITCoursesBot.ITCoursesBot.Configuration;
using Telegram.Bot;

namespace ITCoursesBot.ITCoursesBot.Controllers
{
    public class CodeExplainController : BaseController
    {
        private readonly OpenAISettings _openAISettings;
        private readonly IOpenAIClient _openAi;
        public CodeExplainController(ITelegramBotClient bot,
            OpenAISettings openAISettings,
            IOpenAIClient openAi,
            IMessageService messageService, ISessionManager sessionManager, IKeyboardBuilder keyboardBuilder)
            : base(bot, messageService, sessionManager, keyboardBuilder)
        {
            _openAISettings = openAISettings;
            _openAi = openAi;
        }

        public override bool CanHandle()
        {
            var session = GetCurrentSessionById(ChatId);
            if (session == null)
                return false;

            if (session.Mode != BotMode.CodeExplain)
                return false;

            return true;
        }

        public override async Task<bool> HandleAsync(CancellationToken ct)
        {
            var session = _sessionManager.GetOrCreateSession(ChatId);

            if (session == null)
                return false;

            var text = CurrentUpdate?.Message?.Text;
            if (string.IsNullOrEmpty(text))
            {
                session.SetDefaultState();
                return false;
            }

            string explanation = await _openAi.GetChatResponseAsync(_openAISettings.InstructionsCodeExplain, text);

           await _messageService.SendTextAsync(
                ChatId,
                explanation,
                replyMarkup: _keyboardBuilder.BuildBackToMenu(), 
                asMarkdown: true,
                cancellationToken: ct);

            return true;
        }
    }
}
