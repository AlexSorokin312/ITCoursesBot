using System.Net.Http.Json;
using System.Text;

public class DialogHistoryClient : IDialogHistoryClient
{
    private readonly HttpClient _http;

    public DialogHistoryClient(HttpClient httpClient)
    {
        _http = httpClient;
    }

    public async Task WriteAsync(CreateDialogRequest dto, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/dialogs", dto, ct);
        resp.EnsureSuccessStatusCode(); // ожидаем 204 NoContent; 429/400 кинут исключение
    }

    public async Task<IReadOnlyList<DialogItem>> GetAsync(
        long telegramId,
        DateTime? from = null,
        DateTime? to = null,
        int skip = 0,
        int take = 50,
        CancellationToken ct = default)
    {
        var sb = new StringBuilder($"api/dialogs/{telegramId}");
        var hasQuery = false;

        void add(string name, string value)
        {
            sb.Append(hasQuery ? '&' : '?');
            sb.Append(name).Append('=').Append(Uri.EscapeDataString(value));
            hasQuery = true;
        }

        if (from.HasValue) add("from", from.Value.ToString("O"));
        if (to.HasValue) add("to", to.Value.ToString("O"));
        if (skip != 0) add("skip", skip.ToString());
        if (take != 50) add("take", take.ToString());

        var url = sb.ToString();
        var list = await _http.GetFromJsonAsync<IReadOnlyList<DialogItem>>(url, ct);
        return list ?? Array.Empty<DialogItem>();
    }

    public async Task<int> DeleteAsync(long telegramId, DateTime? to = null, CancellationToken ct = default)
    {
        var url = $"api/dialogs/{telegramId}";
        if (to.HasValue)
        {
            url += "?to=" + Uri.EscapeDataString(to.Value.ToString("O"));
        }

        var resp = await _http.DeleteAsync(url, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            return 0;

        resp.EnsureSuccessStatusCode();

        var payload = await resp.Content.ReadFromJsonAsync<DeleteDialogResponse>(cancellationToken: ct);
        return payload?.Deleted ?? 0;
    }
}
