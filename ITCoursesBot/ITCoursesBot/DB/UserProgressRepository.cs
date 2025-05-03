// -------------------- Репозиторий прогресса --------------------
using ITCoursesBot.DB;
using Microsoft.EntityFrameworkCore;

/// <summary>Информация о вопросе и статистике ответов пользователя.</summary>
public sealed record QuestionProgressDto(
    int QuestionId,
    string Text,
    int CorrectAttempts,
    int WrongAttempts);

/// <summary>Ошибки по уроку.</summary>
public sealed record LessonErrorDto(
    int LessonId,
    string LessonTitle,
    string CourseShort,
    int Major,
    int Minor,
    int WrongAnswers);

/// <summary>Урок, по которому пользователь охватил все вопросы хотя бы раз.</summary>
public sealed record LessonCoveredDto(
    int LessonId,
    string LessonTitle,
    string CourseShort,
    int Major,
    int Minor,
    int TotalQuestions);


public sealed class UserProgressRepository
{
    private readonly BotDbContext _db;
    public UserProgressRepository(BotDbContext db) => _db = db;

    /// 1. Вопросы, на которые ни разу не ответили правильно
    public async Task<List<QuestionProgressDto>> GetNeverAnsweredCorrectlyAsync(long userId)
    {
        var questions = await _db.Questions.AsNoTracking().ToListAsync();
        var allAnswers = await _db.UserAnswers
                                  .Where(a => a.UserId == userId)
                                  .AsNoTracking()
                                  .ToListAsync();

        var result = new List<QuestionProgressDto>();

        foreach (var q in questions)
        {
            var answers = allAnswers.Where(a => a.QuestionId == q.Id).ToList();
            int correct = answers.Count(a => a.IsCorrect);
            int wrong = answers.Count - correct;

            if (correct == 0)
                result.Add(new QuestionProgressDto(q.Id, q.Text, correct, wrong));
        }

        return result;
    }

    /// 2. Вопросы, где неверных ответов больше, чем верных
    public async Task<List<QuestionProgressDto>> GetMoreWrongThanRightAsync(long userId)
    {
        var questions = await _db.Questions.AsNoTracking().ToListAsync();
        var allAnswers = await _db.UserAnswers
                                  .Where(a => a.UserId == userId)
                                  .AsNoTracking()
                                  .ToListAsync();

        var result = new List<QuestionProgressDto>();

        foreach (var q in questions)
        {
            var answers = allAnswers.Where(a => a.QuestionId == q.Id).ToList();
            int correct = answers.Count(a => a.IsCorrect);
            int wrong = answers.Count - correct;

            if (wrong > correct)
                result.Add(new QuestionProgressDto(q.Id, q.Text, correct, wrong));
        }

        return result;
    }

    /// 3. Топ‑N уроков с наибольшим числом ошибок
    public async Task<List<LessonErrorDto>> GetLessonsWithMostErrorsAsync(long userId, int top = 5)
    {
        var userAnswers = await _db.UserAnswers
            .Where(a => a.UserId == userId)
            .Include(a => a.Question)
                .ThenInclude(q => q.Lesson)
                    .ThenInclude(l => l.Course)
            .AsNoTracking()
            .ToListAsync();

        return userAnswers
            .GroupBy(a => a.Question.LessonId)
            .Select(g =>
            {
                var lesson = g.First().Question.Lesson;
                int wrong = g.Count(a => !a.IsCorrect);
                int correct = g.Count(a => a.IsCorrect);

                // теперь каждая ошибка считается за 2 балла
                int netErr = wrong * 2 - correct;

                return new LessonErrorDto(
                    lesson.Id,
                    lesson.Title,
                    lesson.Course.Name,
                    lesson.Major,
                    lesson.Minor,
                    netErr
                );
            })
            .OrderByDescending(dto => dto.WrongAnswers)
            .Take(top)
            .ToList();
    }

    /// 4. Уроки, где студент ответил хотя бы раз на каждый вопрос
    public async Task<List<LessonCoveredDto>> GetFullyCoveredLessonsAsync(long userId)
    {
        var lessons = await _db.Lessons
                                 .Include(l => l.Course)
                                 .Include(l => l.Questions)
                                 .AsNoTracking()
                                 .ToListAsync();

        var answersByQuestion = await _db.UserAnswers
                                         .Where(a => a.UserId == userId)
                                         .Select(a => a.QuestionId)
                                         .Distinct()
                                         .ToListAsync();
        var answeredSet = new HashSet<int>(answersByQuestion);

        var result = new List<LessonCoveredDto>();

        foreach (var l in lessons)
        {
            int totalQ = l.Questions.Count;
            int covered = l.Questions.Count(q => answeredSet.Contains(q.Id));

            if (totalQ > 0 && covered == totalQ)
            {
                result.Add(new LessonCoveredDto(
                    l.Id,
                    l.Title,
                    l.Course.ShortName,
                    l.Major,
                    l.Minor,
                    totalQ));
            }
        }

        return result;
    }
}
