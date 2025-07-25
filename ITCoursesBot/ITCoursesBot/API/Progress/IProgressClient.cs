namespace Bot.Ports;
public interface IProgressClient
{
    Task<bool> AddAnswerAsync(AddAnswerDto dto, CancellationToken ct = default);

    Task<IReadOnlyList<CourseProgressDto>> GetAllCourseProgressAsync(
        long telegramId, CancellationToken ct = default);

    Task<IReadOnlyList<QuestionDto>> GetCorrectQuestionsAsync(
        long telegramId, CancellationToken ct = default);

    Task<IReadOnlyList<QuestionDto>> GetIncorrectQuestionsAsync(
        long telegramId, CancellationToken ct = default);

    Task<IReadOnlyList<Question>> GetUnlearnedQuestionsAsync(
        long telegramId, string course, int block, CancellationToken ct = default);

    Task<IReadOnlyList<Question>> GetUnlearnedQuestionsAsync(
        long telegramId, string course, CancellationToken ct = default);

    Task<BlockStatsDto> GetBlockStatsAsync(
        long telegramId, string course, int block, CancellationToken ct = default);

    Task<CourseStatsDto> GetCourseStatsAsync(
        long telegramId, string course, CancellationToken ct = default);

    Task<QuestionStatsDto> GetCourseStatsDetailedAsync(
        long telegramId, string course, CancellationToken ct = default);

    Task<QuestionStatsDto> GetBlockStatsDetailedAsync(
        long telegramId, string course, int block, CancellationToken ct = default);

    Task<IReadOnlyList<LessonErrorsDto>> GetLessonErrorStatsAsync(
        long telegramId, string course, CancellationToken ct = default);
}