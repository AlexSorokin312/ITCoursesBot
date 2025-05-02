namespace ITCoursesBot.Interfaces
{
    public interface IQuizRepository
    {
        List<string> GetQuestionsForInterviewBlockAsync(string block, CancellationToken ct = default);
        List<string> GetQuestionsByLessonAsync(string block, CancellationToken ct = default);
    }
}
