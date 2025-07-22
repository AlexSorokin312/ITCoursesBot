using System.Net.Http.Json;

public class QuestionsClient : IQuestionsClient
{
    private readonly HttpClient _http;

    public QuestionsClient(HttpClient httpClient)
    {
        _http = httpClient;
    }
    public async Task<IReadOnlyList<Question>> GetLessonQuestionsAsync(string course, int block, int lessonNumber)
    {
        var url = $"api/questions/{Uri.EscapeDataString(course)}/{block}/{lessonNumber}";
        var response = await _http.GetFromJsonAsync<IReadOnlyList<Question>>(url);
        return response ?? Array.Empty<Question>();
    }

    public async Task<IReadOnlyList<Question>> GetBlockQuestionsAsync(string course, int block)
    {
        var url = $"api/questions/{Uri.EscapeDataString(course)}/{block}";
        var response = await _http.GetFromJsonAsync<IReadOnlyList<Question>>(url);
        return response ?? Array.Empty<Question>();
    }

    public async Task<Question> AddQuestionAsync(AddQuestionDto dto)
    {
        var response = await _http.PostAsJsonAsync("api/questions", dto);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Question>()!;
    }

    public async Task<IReadOnlyList<Question>> AddQuestionsAsync(AddQuestionsDto dto)
    {
        var response = await _http.PostAsJsonAsync("api/questions/AddQuestions", dto);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<Question>>()!;
    }
}