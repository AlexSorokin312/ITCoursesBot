public enum BotMode { None, Questions, CodeExplain, MockInterview, Dialog }

public class UserState
{
    public BotMode Mode { get; set; } = BotMode.None;
    public string? LessonId { get; set; }
    public List<string>? Questions { get; set; }
    public int Index { get; set; }

    public bool AwaitLessonNumber => Mode == BotMode.Questions && LessonId is null;
    public bool AwaitAnswer => (Mode == BotMode.Questions || Mode == BotMode.MockInterview)
                                && LessonId is not null && Questions is not null;
}