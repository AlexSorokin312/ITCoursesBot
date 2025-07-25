using Bot.Ports;
using System.Net.Http.Json;

namespace Bot.Adapters.ApiClients;
public class ProgressClient : IProgressClient
{
    private readonly HttpClient _http;
    public ProgressClient(HttpClient http) => _http = http;

    public async Task<bool> AddAnswerAsync(AddAnswerDto dto, CancellationToken ct = default)
    {
        // POST /api/progress
        var resp = await _http.PostAsJsonAsync("api/progress", dto, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<bool>(cancellationToken: ct);
    }

    public Task<IReadOnlyList<CourseProgressDto>> GetAllCourseProgressAsync(
        long telegramId, CancellationToken ct = default)
    {
        // GET /api/progress/{telegramId}/courses
        return _http.GetFromJsonAsync<IReadOnlyList<CourseProgressDto>>(
            $"api/progress/{telegramId}/courses", ct)!;
    }

    public Task<IReadOnlyList<QuestionDto>> GetCorrectQuestionsAsync(
        long telegramId, CancellationToken ct = default)
    {
        // GET /api/progress/{telegramId}/questions/correct
        return _http.GetFromJsonAsync<IReadOnlyList<QuestionDto>>(
            $"api/progress/{telegramId}/questions/correct", ct)!;
    }

    public Task<IReadOnlyList<QuestionDto>> GetIncorrectQuestionsAsync(
        long telegramId, CancellationToken ct = default)
    {
        // GET /api/progress/{telegramId}/questions/incorrect
        return _http.GetFromJsonAsync<IReadOnlyList<QuestionDto>>(
            $"api/progress/{telegramId}/questions/incorrect", ct)!;
    }

    public Task<IReadOnlyList<Question>> GetUnlearnedQuestionsAsync(
        long telegramId, string course, int block, CancellationToken ct = default)
    {
        // GET /api/progress/unlearned/{telegramId}/{course}/{block}
        return _http.GetFromJsonAsync<IReadOnlyList<Question>>(
            $"api/progress/unlearned/{telegramId}/{Uri.EscapeDataString(course)}/{block}", ct)!;
    }

    public Task<IReadOnlyList<Question>> GetUnlearnedQuestionsAsync(
        long telegramId, string course, CancellationToken ct = default)
    {
        // GET /api/progress/unlearned/{telegramId}/{course}
        return _http.GetFromJsonAsync<IReadOnlyList<Question>>(
            $"api/progress/unlearned/{telegramId}/{Uri.EscapeDataString(course)}", ct)!;
    }

    public Task<BlockStatsDto> GetBlockStatsAsync(
        long telegramId, string course, int block, CancellationToken ct = default)
    {
        // GET /api/progress/blocks/{telegramId}/{course}/{block}
        return _http.GetFromJsonAsync<BlockStatsDto>(
            $"api/progress/blocks/{telegramId}/{Uri.EscapeDataString(course)}/{block}", ct)!;
    }

    public Task<CourseStatsDto> GetCourseStatsAsync(
        long telegramId, string course, CancellationToken ct = default)
    {
        // GET /api/progress/courses/{telegramId}/{course}
        return _http.GetFromJsonAsync<CourseStatsDto>(
            $"api/progress/courses/{telegramId}/{Uri.EscapeDataString(course)}", ct)!;
    }

    public Task<QuestionStatsDto> GetCourseStatsDetailedAsync(
        long telegramId, string course, CancellationToken ct = default)
    {
        // GET /api/progress/{telegramId}/{course}
        return _http.GetFromJsonAsync<QuestionStatsDto>(
            $"api/progress/{telegramId}/{Uri.EscapeDataString(course)}", ct)!;
    }

    public Task<QuestionStatsDto> GetBlockStatsDetailedAsync(
        long telegramId, string course, int block, CancellationToken ct = default)
    {
        // GET /api/progress/{telegramId}/{course}/{block}
        return _http.GetFromJsonAsync<QuestionStatsDto>(
            $"api/progress/{telegramId}/{Uri.EscapeDataString(course)}/{block}", ct)!;
    }

    public Task<IReadOnlyList<LessonErrorsDto>> GetLessonErrorStatsAsync(
        long telegramId, string course, CancellationToken ct = default)
    {
        // GET /api/progress/{telegramId}/courses/{course}/errors
        var enc = Uri.EscapeDataString(course);
        return _http.GetFromJsonAsync<IReadOnlyList<LessonErrorsDto>>(
            $"api/progress/{telegramId}/courses/{enc}/errors", ct)!;
    }
}
