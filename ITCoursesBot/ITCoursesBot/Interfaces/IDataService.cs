using ITCoursesBot.DB;

public interface IDataService
{
    // Пользователи
    Task<User> AddOrUpdateUserAsync(long userId, string username);
    Task<User?> GetUserAsync(long userId);
    Task<List<User>> GetAllUsersAsync();

    // Курсы / Уроки / Вопросы
    Task<Course> AddCourseAsync(string name, string shortName);
    Task<List<Course>> GetAllCoursesAsync();

    Task<Lesson> AddLessonAsync(int courseId, string lessonName);
    Task<List<Lesson>> GetLessonsByCourseAsync(int courseId);

    Task<Question> AddQuestionAsync(int lessonId, string text);
    Task<List<Question>> GetQuestionsByLessonAsync(int lessonId);
    Task<List<Question>> GetQuestionsByCourseAsync(int courseId);

    // Результаты пользователей
    Task<UserAnswer> AddUserAnswerAsync(long userId, int questionId, string answerText, bool isCorrect);
    Task<List<UserAnswer>> GetUserAnswersAsync(long userId);

    // Лимиты OpenAI
    Task<AIUserRequests> IncrementAiRequestAsync(long userId, DateTime forDate);
    Task<AIUserRequests?> GetAiRequestsAsync(long userId, DateTime forDate);
}
