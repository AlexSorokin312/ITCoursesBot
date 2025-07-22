using Bot.Ports;
using System.Net.Http.Json;

namespace Bot.Adapters.ApiClients;
public class ProgressClient : IProgressClient
{
    private readonly HttpClient _http;
    public ProgressClient(HttpClient http) => _http = http;

    public async Task<bool> AddAnswerAsync(AddAnswerDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/answers", dto, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<bool>(cancellationToken: ct)!;
    }

    public Task<BlockStatsDto> GetBlockStatsAsync(
        long telegramId, string course, int block, CancellationToken ct = default)
        => _http.GetFromJsonAsync<BlockStatsDto>(
            $"api/answers/blocks/{telegramId}/{course}/{block}", ct)!;

    public Task<CourseStatsDto> GetCourseStatsAsync(
        long telegramId, string course, CancellationToken ct = default)
        => _http.GetFromJsonAsync<CourseStatsDto>(
            $"api/answers/courses/{telegramId}/{course}", ct)!;

    public Task<IReadOnlyList<Question>> GetUnlearnedQuestionsAsync(
        long telegramId, string course, int block, CancellationToken ct = default)
        => _http.GetFromJsonAsync<IReadOnlyList<Question>>(
            $"api/answers/unlearned/{telegramId}/{course}/{block}", ct)!;

    public Task<IReadOnlyList<Question>> GetUnlearnedQuestionsAsync(
        long telegramId, string course, CancellationToken ct = default)
        => _http.GetFromJsonAsync<IReadOnlyList<Question>>(
            $"api/answers/unlearned/{telegramId}/{course}", ct)!;

    public Task<QuestionStatsDto> GetCourseStatsDetailedAsync(
        long telegramId, string course, CancellationToken ct = default)
        => _http.GetFromJsonAsync<QuestionStatsDto>(
            $"api/answers/{telegramId}/{course}", ct)!;

    public Task<QuestionStatsDto> GetBlockStatsDetailedAsync(
        long telegramId, string course, int block, CancellationToken ct = default)
        => _http.GetFromJsonAsync<QuestionStatsDto>(
            $"api/answers/{telegramId}/{course}/{block}", ct)!;
}
