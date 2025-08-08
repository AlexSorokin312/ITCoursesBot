using Bot.Ports;
using ITCoursesBot.Interfaces;
using ITCoursesBot.ITCoursesBot.Configuration;
using Microsoft.Extensions.Options;
using Serilog;
using System.Text;
using Telegram.Bot.Types.ReplyMarkups;

public class ProgressSummaryMiddleware : IUpdateMiddleware
{
    private readonly IMessageService _messageService;
    private readonly IKeyboardBuilder _keyboardBuilder;
    private readonly IProgressClient _progressClient;
    private readonly IOpenAIClient _openAi;
    private readonly OpenAISettings _aiSettings;

    public ProgressSummaryMiddleware(
        IMessageService msg,
        IKeyboardBuilder kbd,
        IProgressClient progress,
        IOpenAIClient openAi,
        IOptions<OpenAISettings> settings)
    {
        _messageService = msg;
        _keyboardBuilder = kbd;
        _progressClient = progress;
        _openAi = openAi;
        _aiSettings = settings.Value;
    }

    public async Task InvokeAsync(UpdateContext ctx, Func<Task> next)
    {
        var cbData = ctx.Update.CallbackQuery?.Data;
        var chatId = ctx.Update.CallbackQuery?.Message.Chat.Id ?? ctx.GetChatId();
        var userId = ctx.Update.CallbackQuery?.From.Id ?? ctx.GetUserId();

        if (cbData != KeyboardBuilder.USER_PROGRESS_BUTTON_NAME)
        {
            await next();
            return;
        }

        try
        {
            LoggerService.LogInfo($"Пользователь {chatId} запросил сводку прогресса");

            // 1) Текстовая сводка
            string summary;
            try
            {
                summary = await BuildCourseSummaryAsync(userId, ctx.CancellationToken);
            }
            catch (Exception ex)
            {
                LoggerService.LogError(ex.ToString(), $"Не удалось сформировать текстовую сводку для пользователя {chatId}");
                await SafeSendAsync(chatId, "❌ Не удалось получить сводку прогресса, попробуйте позже.", ctx.CancellationToken);
                return;
            }

            await SafeSendAsync(chatId, summary, ctx.CancellationToken);

            // 2) AI-саммари
            string prompt;
            try
            {
                prompt = await BuildAiPromptAsync(userId, ctx.CancellationToken);
            }
            catch (Exception ex)
            {
                LoggerService.LogError(ex.ToString() , $"Не удалось сформировать запрос к ИИ для пользователя {chatId}");
                await SafeSendAsync(chatId, "❌ Не удалось сформировать запрос к ИИ.", _keyboardBuilder.Build(), ctx.CancellationToken);
                return;
            }

            string aiReply = null;
            try
            {
                aiReply = await _openAi.GetChatResponseAsync(_aiSettings.InstructionsProgressSummary, prompt);
            }
            catch (Exception ex)
            {
                LoggerService.LogError(ex.ToString(), $"Запрос к OpenAI не удался для пользователя {chatId}");
            }

            // 3) Вывод результата
            if (!string.IsNullOrEmpty(aiReply))
            {
                await SafeSendAsync(chatId, aiReply, _keyboardBuilder.Build(), ctx.CancellationToken);
            }
            else
            {
                LoggerService.LogWarning($"ИИ вернул пустой ответ для пользователя {chatId}");
                await SafeSendAsync(chatId, "🏠 Главное меню:\nВыберите опцию:", _keyboardBuilder.Build(), ctx.CancellationToken);
            }
        }
        catch (Exception ex)
        {
            LoggerService.LogError(ex.ToString(), $"Необработанная ошибка в ProgressSummaryMiddleware для пользователя {chatId}");
        }
    }

    private async Task<string> BuildCourseSummaryAsync(long userId, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var courses = await _progressClient.GetAllCourseProgressAsync(userId, ct);
        foreach (var c in courses)
        {
            int done = c.CorrectCount + c.IncorrectCount;
            int total = done + c.UnansweredCount;
            sb.AppendLine($"🏅 **{c.Course}**")
              .AppendLine($"Пройдено **{done}/{total}** вопросов")
              .AppendLine($"• ✅ {c.CorrectCount}")
              .AppendLine($"• ❌ {c.IncorrectCount}")
              .AppendLine($"• 💤 {c.UnansweredCount}")
              .AppendLine();
            await AppendLessonErrorsAsync(sb, userId, c.Course, ct);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private async Task AppendLessonErrorsAsync(StringBuilder sb, long userId, string course, CancellationToken ct)
    {
        var errors = await _progressClient.GetLessonErrorStatsAsync(userId, course, ct);
        sb.AppendLine("🏆 Распределение ошибок по урокам:");
        if (!errors.Any())
        {
            sb.AppendLine("— нет ошибок в этом курсе —");
        }
        else
        {
            foreach (var e in errors)
            {
                var suffix = e.WrongCount % 10 == 1 && e.WrongCount % 100 != 11 ? "ка" : "ок";
                sb.AppendLine($"• {e.LessonName} — {e.WrongCount} ошиб{suffix}");
            }
        }
    }

    private async Task<string> BuildAiPromptAsync(long userId, CancellationToken ct)
    {
        var okQs = await _progressClient.GetCorrectQuestionsAsync(userId, ct);
        var badQs = await _progressClient.GetIncorrectQuestionsAsync(userId, ct);

        var sb = new StringBuilder()
            .AppendLine("Пользователь без ошибок ответил на вопросы:")
            .AppendLine(string.Join("\n", okQs.Select((t, i) => $"{i + 1}. {t}")))
            .AppendLine()
            .AppendLine("Пользователь с ошибками ответил на вопросы:")
            .AppendLine(string.Join("\n", badQs.Select((t, i) => $"{i + 1}. {t}")));
        return sb.ToString();
    }

    #region SafeSendAsync

    private async Task SafeSendAsync(long chatId, string text, CancellationToken ct)
    {
        try
        {
            await _messageService.SendTextAsync(chatId, text, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            LoggerService.LogError(ex.ToString(), $"Не удалось отправить сообщение в чат {chatId}: {text}");
        }
    }

    private async Task SafeSendAsync(long chatId, string text, InlineKeyboardMarkup markup, CancellationToken ct)
    {
        try
        {
            await _messageService.SendTextAsync(chatId, text, replyMarkup: markup, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            LoggerService.LogError(ex.ToString(), $"Не удалось отправить клавиатуру в чат {chatId}: {text}");
        }
    }

    #endregion
}