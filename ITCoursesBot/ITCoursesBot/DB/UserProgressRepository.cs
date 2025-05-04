// -------------------- Репозиторий прогресса --------------------
using ITCoursesBot.DB;
using ITCoursesBot.ITCoursesBot.Services;
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

public sealed record CourseProgressDetailDto(
    string CourseName,
    int TotalQuestions,
    int AnsweredQuestions,
    int CorrectFirstAttempt,
    int WithErrors);


public sealed record ErrorByLessonDto(
    string CourseName,
    string LessonTitle,
    int WrongCount);

public sealed record CourseProgressDto(
    string CourseShort,
    int AnsweredQuestions,
    int TotalQuestions);



public sealed class UserProgressRepository
{
    private readonly BotDbContext _db;
    public UserProgressRepository(BotDbContext db) => _db = db;

    /// <summary>
    /// Распределение ошибок по урокам:
    /// подсчитываем только отрицательные попытки (IsCorrect==false).
    /// </summary>
    public async Task<List<ErrorByLessonDto>> GetErrorDistributionByLessonAsync(long userId)
    {
        var wrongAnswers = await _db.UserAnswers
            .Where(a => a.UserId == userId && !a.IsCorrect)
            .Include(a => a.Question)
                .ThenInclude(q => q.Lesson)
                    .ThenInclude(l => l.Course)
            .AsNoTracking()
            .ToListAsync();

        // уникальные пары (CourseName, LessonTitle, QuestionId)
        var distinct = wrongAnswers
            .Select(a => new {
                CourseName = a.Question.Lesson.Course.Name,
                LessonTitle = a.Question.Lesson.Title,
                QuestionId = a.QuestionId
            })
            .Distinct();

        // группируем по курсу и уроку
        return distinct
            .GroupBy(x => (x.CourseName, x.LessonTitle))
            .Select(g => new ErrorByLessonDto(
                CourseName: g.Key.CourseName,
                LessonTitle: g.Key.LessonTitle,
                WrongCount: g.Count()
            ))
            .OrderByDescending(dto => dto.WrongCount)
            .ToList();
    }

    // Возвращает тексты вопросов, где НЕ было ни одной ошибки
    public async Task<List<string>> GetQuestionsAnsweredWithoutErrorsAsync(long userId)
    {
        var allQ = await _db.Questions.AsNoTracking().ToListAsync();
        var allA = await _db.UserAnswers
                           .Where(a => a.UserId == userId)
                           .AsNoTracking()
                           .ToListAsync();
        var ok = new List<string>();
        foreach (var q in allQ)
        {
            var ans = allA.Where(a => a.QuestionId == q.Id).ToList();
            if (ans.Count > 0 && ans.All(a => a.IsCorrect))
                ok.Add(q.Text.Trim());
        }
        return ok;
    }

    // Возвращает тексты вопросов, в которых была хотя бы одна ошибка
    public async Task<List<string>> GetQuestionsWithErrorsAsync(long userId)
    {
        var allQ = await _db.Questions.AsNoTracking().ToListAsync();
        var allA = await _db.UserAnswers
                           .Where(a => a.UserId == userId)
                           .AsNoTracking()
                           .ToListAsync();
        var bad = new List<string>();
        foreach (var q in allQ)
        {
            var ans = allA.Where(a => a.QuestionId == q.Id).ToList();
            if (ans.Any(a => !a.IsCorrect))
                bad.Add(q.Text.Trim());
        }
        return bad;
    }

    public async Task<List<QuestionDto>> GetErrorQuestionDtosAsync(long userId)
    {
        // 1) сгруппировать все ответы пользователя по вопросу
        var answerGroups = await _db.UserAnswers
            .Where(a => a.UserId == userId)
            .AsNoTracking()
            .ToListAsync();

        var errorOnlyQuestionIds = answerGroups
            .GroupBy(a => a.QuestionId)
            // есть хотя бы одна ошибка...
            .Where(g => g.Any(a => !a.IsCorrect)
                // ...и нет ни одной правильной попытки
                && g.All(a => !a.IsCorrect))
            .Select(g => g.Key)
            .ToList();

        if (!errorOnlyQuestionIds.Any())
            return new List<QuestionDto>();

        // 2) выгружаем эти вопросы из базы как DTO
        return await _db.Questions
            .AsNoTracking()
            .Where(q => errorOnlyQuestionIds.Contains(q.Id))
            .Select(q => new QuestionDto(q.Id, q.Text.Trim()))
            .ToListAsync();
    }

public async Task<List<CourseProgressDetailDto>> GetCourseProgressDetailedAsync(long userId)
    {
        // вытащили все вопросы с полным именем курса
        var allQuestions = await _db.Questions
            .AsNoTracking()
            .Include(q => q.Lesson).ThenInclude(l => l.Course)
            .Select(q => new { q.Id, CourseName = q.Lesson.Course.Name })
            .ToListAsync();

        var userAnswers = await _db.UserAnswers
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.AnsweredAt)
            .AsNoTracking()
            .ToListAsync();

        var byQuestion = userAnswers
            .GroupBy(a => a.QuestionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = allQuestions
            .GroupBy(x => x.CourseName)
            .Select(g =>
            {
                var questions = g.ToList();
                int total = questions.Count;
                int answered = 0;
                int correctFirst = 0;
                int withErrors = 0;

                foreach (var q in questions)
                {
                    if (byQuestion.TryGetValue(q.Id, out var answers))
                    {
                        answered++;
                        if (answers.First().IsCorrect) correctFirst++;
                        else withErrors++;
                    }
                }

                return new CourseProgressDetailDto(
                    CourseName: g.Key,
                    TotalQuestions: total,
                    AnsweredQuestions: answered,
                    CorrectFirstAttempt: correctFirst,
                    WithErrors: withErrors
                );
            })
            .OrderBy(dto => dto.CourseName)
            .ToList();

        return result;

    }
}