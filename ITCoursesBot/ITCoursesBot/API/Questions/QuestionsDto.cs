
public record AddQuestionDto(string Course, int Block, int LessonNumber, string Text);
public record AddQuestionsDto(string Course, int Block, int LessonNumber, IEnumerable<string> Texts);

public class Question
{
    public int Id { get; set; }
    public int LessonId { get; set; }
    public string Text { get; set; } = default!;
}