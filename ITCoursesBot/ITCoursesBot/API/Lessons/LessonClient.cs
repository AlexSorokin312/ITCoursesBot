using Bot.Ports;
using System.Net.Http.Json;

namespace Bot.Adapters.ApiClients;
public class LessonClient : ILessonClient
{
    private readonly HttpClient _http;
    public LessonClient(HttpClient http) => _http = http;

    public async Task<LessonDto> InsertLessonAsync(NewLessonDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/lessons", dto, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<LessonDto>(cancellationToken: ct)!;
    }

    public async Task<LessonDto> AddLessonToEndAsync(NewLessonEndDto dto, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/lessons/end", dto, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<LessonDto>(cancellationToken: ct)!;
    }

    public Task<LessonDto?> GetLessonAsync(int lessonId, CancellationToken ct = default)
    {
        return _http.GetFromJsonAsync<LessonDto?>($"api/lessons/{lessonId}", ct)!;
    }
}
