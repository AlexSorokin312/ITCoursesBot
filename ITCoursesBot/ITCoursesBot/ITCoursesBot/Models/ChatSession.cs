public class ChatSession
{
    public long ChatId { get; }
    public ChatSession(long chatId)
    {
        ChatId = chatId;
    }

    public BotMode Mode { get; set; } = BotMode.None;

    public string? LessonIdentifier { get; set; }
    public List<string>? QuestionPool { get; set; }
    public int QuestionIndex { get; set; }

    public List<string> QuestionsForQuiz { get; set; }
    public bool WaitingForLessonNumber => Mode is BotMode.BeginQuiz && LessonIdentifier is null;
    public bool WaitingForAnswer => Mode is BotMode.BeginQuiz or BotMode.MockInterview
                                                 && LessonIdentifier is not null
                                                 && QuestionPool is not null;


}