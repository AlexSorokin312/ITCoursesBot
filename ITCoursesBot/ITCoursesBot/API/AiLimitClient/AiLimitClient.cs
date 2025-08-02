using Bot.Ports;
using System.Net.Http.Json;

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

    public async Task<RequestInfo> GetRequestInfoAsync(long telegramId, CancellationToken ct = default)
    {
        var resp = await _http.GetFromJsonAsync<RequestInfo>($"api/ai-requests/{telegramId}/request-info", ct);

        if (resp == null)
            throw new InvalidOperationException("Пустой ответ от /request-info");

        return resp;
    }

    // Новый метод для применения промокода
    public async Task ApplyPromoCodeAsync(long telegramId, string promoCode, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"api/ai-requests/{telegramId}/apply-promo-code", promoCode, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to apply promo code: {errorMessage}");
        }
    }
}
