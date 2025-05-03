using ITCoursesBot.Interfaces;
using Microsoft.Extensions.Configuration;

public class QuizRepository : IQuizRepository
{
    public QuizRepository(IConfiguration config)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("quizData.json", optional: false, reloadOnChange: false);
        IConfiguration cfg = builder.Build();

        _course = cfg
            .GetSection("QuizData")
            .Get<Dictionary<string, List<string>>>()
            ?? throw new InvalidOperationException("Секция QuizData не найдена в quizData.json");
    }

    private readonly Dictionary<string, List<string>> _course;


    public List<string> GetQuestionsByLessonAsync(string block, CancellationToken ct = default)
    {
        var questions = _course[block];
        return questions;

    }

    public List<string> GetQuestionsForInterviewBlockAsync(string block, CancellationToken ct = default)
    {
        throw new NotFiniteNumberException();
    }
}