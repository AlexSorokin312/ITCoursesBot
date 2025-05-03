public enum BotMode { None, BeginQuiz, PassQuiz, CodeExplain, MockInterview, Dialog,
    Progress
}

public class UserState
{
    public BotMode Mode { get; set; } = BotMode.None;
    public string? LessonId { get; set; }
    public List<string>? Questions { get; set; }
    public int Index { get; set; }

    public bool AwaitLessonNumber => Mode == BotMode.BeginQuiz && LessonId is null;
    public bool AwaitAnswer => (Mode == BotMode.BeginQuiz || Mode == BotMode.MockInterview)
                                && LessonId is not null && Questions is not null;

    public void SetDefaultMode() => Mode = BotMode.None;
    
}