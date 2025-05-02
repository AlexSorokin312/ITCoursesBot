namespace ITCoursesBot.Interfaces
{
    public interface IQuestionRepository
    {
        List<string> GetQuestionsForInterviewBlockAsync(string block, CancellationToken ct = default);
        List<string> GetQuestionsByLessonAsync(string block, CancellationToken ct = default);
    }
}
