// Bot/Ports/ILessonClient.cs
namespace Bot.Ports;
public interface ILessonClient
{
    /// <summary>Вставить урок в середину блока.</summary>
    Task<LessonDto> InsertLessonAsync(NewLessonDto dto, CancellationToken ct = default);

    /// <summary>Добавить урок в конец блока.</summary>
    Task<LessonDto> AddLessonToEndAsync(NewLessonEndDto dto, CancellationToken ct = default);

    /// <summary>Получить урок по его ID.</summary>
    Task<LessonDto?> GetLessonAsync(int lessonId, CancellationToken ct = default);
}
