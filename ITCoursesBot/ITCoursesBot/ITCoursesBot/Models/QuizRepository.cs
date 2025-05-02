using ITCoursesBot.Interfaces;

public class QuizRepository : IQuizRepository
{
    public Dictionary<string, List<string>> course { get; } = new()
    {
        ["База21"] = new() { 
            "Что такое переменная?",
            "Что такое инициализация переменной и чем отличается от объявления переменной?"
        },

        ["База22"] = new() {
            "Что такое тип переменных?",
            "Назовите хотя бы один тип для хранения: текста, целых чисел и дробных чисел",
            "Чем стоит руководствоваться при выборе типа переменной?"

        },

    };

    public List<string> GetQuestionsByLessonAsync(string block, CancellationToken ct = default)
    {
        var questions = course[block];
        return questions;

    }

    public List<string> GetQuestionsForInterviewBlockAsync(string block, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}