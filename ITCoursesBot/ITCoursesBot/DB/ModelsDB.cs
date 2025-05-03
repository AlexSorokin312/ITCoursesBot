namespace ITCoursesBot.DB
{
    /// <summary>
    /// Пользователь бота
    /// </summary>
    public class User
    {
        public long Id { get; set; }  // Telegram UserId
        public string Username { get; set; } = "";
        public DateTime FirstLaunch { get; set; }  // когда впервые запустил бота
        public DateTime LastUse { get; set; }  // когда последний раз использовал
        public int UsageCount { get; set; }  // сколько раз запускал за всё время

        public ICollection<UserAnswer> Answers { get; set; } = new List<UserAnswer>();
        public ICollection<AIUserRequests> AIRequests { get; set; } = new List<AIUserRequests>();
    }

    /// <summary>
    /// Курс: содержит набор уроков
    /// </summary>
    public class Course
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string ShortName { get; set; } = "";


        public ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
    }


    public class Lesson
    {
        public int Id { get; set; }
        public int Major { get; set; }
        public int Minor { get; set; }
        public string Title { get; set; } = "";
        public int CourseId { get; set; }
        public Course Course { get; set; } = null!;
        public ICollection<Question> Questions { get; set; } = new List<Question>();
    }

    /// <summary>
    /// Вопрос к уроку
    /// </summary>
    public class Question
    {
        public int Id { get; set; }
        public int LessonId { get; set; }
        public Lesson Lesson { get; set; } = null!;

        public string Text { get; set; } = "";

        public ICollection<UserAnswer> UserAnswers { get; set; } = new List<UserAnswer>();
    }

    /// <summary>
    /// Результат ответа пользователя на конкретный вопрос
    /// </summary>
    public class UserAnswer
    {
        public int Id { get; set; }
        public string AnswerText { get; set; } = "";
        public bool IsCorrect { get; set; }
        public DateTime AnsweredAt { get; set; }
        public int QuestionId { get; set; }
        public long UserId { get; set; }

        public Question Question { get; set; } = null!;
        public User User { get; set; } = null!;


    }

    /// <summary>
    /// Учет запросов пользователя к OpenAI — для лимитов
    /// </summary>
    public class AIUserRequests
    {
        public int Id { get; set; }
        public long UserId { get; set; }
        public User User { get; set; } = null!;

        public DateTime Date { get; set; }  // дата (с точностью до дня)
        public int Count { get; set; }  // сколько запросов сделал в этот день
    }
}
