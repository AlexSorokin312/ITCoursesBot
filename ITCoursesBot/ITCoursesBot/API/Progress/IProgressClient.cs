// Bot/Ports/IProgressClient.cs
namespace Bot.Ports;
public interface IProgressClient
{
    /// <summary>Добавить ответ и получить статус «выучено/не выучено»</summary>
    Task<bool> AddAnswerAsync(AddAnswerDto dto, CancellationToken ct = default);

    /// <summary>Статистика по блоку</summary>
    Task<BlockStatsDto> GetBlockStatsAsync(
        long telegramId, string course, int block, CancellationToken ct = default);

    /// <summary>Статистика по курсу (короткая)</summary>
    Task<CourseStatsDto> GetCourseStatsAsync(
        long telegramId, string course, CancellationToken ct = default);

    /// <summary>Список невыученных в блоке</summary>
    Task<IReadOnlyList<Question>> GetUnlearnedQuestionsAsync(
        long telegramId, string course, int block, CancellationToken ct = default);

    /// <summary>Список невыученных по курсу</summary>
    Task<IReadOnlyList<Question>> GetUnlearnedQuestionsAsync(
        long telegramId, string course, CancellationToken ct = default);

    /// <summary>Детальная статистика по курсу</summary>
    Task<QuestionStatsDto> GetCourseStatsDetailedAsync(
        long telegramId, string course, CancellationToken ct = default);

    /// <summary>Детальная статистика по блоку</summary>
    Task<QuestionStatsDto> GetBlockStatsDetailedAsync(
        long telegramId, string course, int block, CancellationToken ct = default);
}
