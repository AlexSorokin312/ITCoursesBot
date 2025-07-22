using Bot.Ports;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

public class AiLimitClient : IAiLimitClient
{
    private readonly HttpClient _http;
    public AiLimitClient(HttpClient http) => _http = http;

    public async Task RecordRequestAsync(long telegramId, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"api/ai-requests/{telegramId}", null, ct);
        resp.EnsureSuccessStatusCode();
    }

    public Task<bool> IsLimitReachedAsync(long telegramId, CancellationToken ct = default)
    {
        return _http.GetFromJsonAsync<bool>($"api/ai-requests/{telegramId}/limit", ct)!;
    }

    public async Task<bool> ResetAsync(long telegramId, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"api/ai-requests/{telegramId}/reset", null, ct);
        resp.EnsureSuccessStatusCode();

        bool? maybe = await resp.Content
                                .ReadFromJsonAsync<bool?>(cancellationToken: ct);
        if (!maybe.HasValue)
            throw new InvalidOperationException("Пустой ответ от /reset");

        return maybe.Value;
    }

    public async Task ForceResetAsync(long telegramId, CancellationToken ct = default)
    {
        var resp = await _http.PostAsync($"api/ai-requests/{telegramId}/reset-force", null, ct);
        resp.EnsureSuccessStatusCode();
    }
}
