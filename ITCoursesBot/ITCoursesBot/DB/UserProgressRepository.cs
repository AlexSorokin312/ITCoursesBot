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


    /// <summary>Общая статистика по прохождению.</summary>
    public sealed record ProgressSummaryDto(
        int TotalQuestions,
        int AnsweredQuestions,
        int CorrectFirstAttempt,
        int WithErrors);

    /// <summary>Число ошибок по одному уроку.</summary>
    public sealed record ErrorByLessonDto(
        string LessonTitle,
        int WrongCount);


public sealed class UserProgressRepository
{
    private readonly BotDbContext _db;
    public UserProgressRepository(BotDbContext db) => _db = db;

    /// <summary>
    /// Общая сводка:
    /// - всего вопросов в базе;
    /// - сколько вопросов пользователь трогал;
    /// - сколько сразу решил верно;
    /// - сколько с ошибками.
    /// </summary>
    public async Task<ProgressSummaryDto> GetProgressSummaryAsync(long userId)
    {
        // всего вопросов в курсе
        int totalQuestions = await _db.Questions.CountAsync();

        // все ответы пользователя, упорядоченные по времени
        var allAnswers = await _db.UserAnswers
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.AnsweredAt)
            .AsNoTracking()
            .ToListAsync();

        // distinct вопросов, которые пользователь трогал
        var distinctByQuestion = allAnswers
            .GroupBy(a => a.QuestionId)
            .Select(g => g.ToList())
            .ToList();

        int answeredQuestions = distinctByQuestion.Count;

        // первых попыток по каждому вопросу
        int correctFirst = distinctByQuestion.Count(g => g.First().IsCorrect);

        // отвеченных с ошибками (есть хотя бы одна первая попытка неверная)
        int withErrors = answeredQuestions - correctFirst;

        return new ProgressSummaryDto(
            totalQuestions,
            answeredQuestions,
            correctFirst,
            withErrors);
    }

    /// <summary>
    /// Распределение ошибок по урокам:
    /// подсчитываем только отрицательные попытки (IsCorrect==false).
    /// </summary>
    public async Task<List<ErrorByLessonDto>> GetErrorDistributionByLessonAsync(long userId)
    {
        // 1) Получаем все неверные ответы пользователя вместе с уроками
        var wrongAnswers = await _db.UserAnswers
            .Where(a => a.UserId == userId && !a.IsCorrect)
            .Include(a => a.Question)
                .ThenInclude(q => q.Lesson)
            .AsNoTracking()
            .ToListAsync();

        // 2) Берём уникальные пары (LessonTitle, QuestionId)
        var distinctQuestions = wrongAnswers
            .Select(a => new
            {
                LessonTitle = a.Question.Lesson.Title,
                QuestionId = a.QuestionId
            })
            .Distinct();

        // 3) Группируем по названию урока и считаем уникальные вопросы
        var distribution = distinctQuestions
            .GroupBy(x => x.LessonTitle)
            .Select(g => new ErrorByLessonDto(
                LessonTitle: g.Key,
                WrongCount: g.Count()  // число уникальных вопросов с ошибками
            ))
            .OrderByDescending(dto => dto.WrongCount)
            .ToList();

        return distribution;
    }
}