using ITCoursesBot.ITCoursesBot.Configuration;
using ITCoursesBot.ITCoursesBot.Models;
using ITCoursesBot.ITCoursesBot.Services.OpenAI;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ITCoursesBot.Services
{
    public class OpenAIClient : IOpenAIClient
    {
        private readonly HttpClient _httpClient;
        private readonly OpenAISettings _openAISettings;

        public OpenAIClient(HttpClient httpClient, OpenAISettings openAISettings)
        {
            _httpClient = httpClient;
            _openAISettings = openAISettings;
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _openAISettings.ApiKey);
        }

        public async Task<string> GetChatResponseAsync(string userMessage)
        {
            var requestBody = new
            {
                model = "gpt-4o", // укажите нужную модель
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        // Используем инструкции из конфигурационного файла
                        content = _openAISettings.ChatInstructions
                    },
                    new
                    {
                        role = "user",
                        content = userMessage
                    }
                }
            };

            string jsonRequest = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
            if (!response.IsSuccessStatusCode)
            {
                string errorResponse = await response.Content.ReadAsStringAsync();
                return $"Ошибка при обращении к OpenAI API: {response.StatusCode}\n{errorResponse}";
            }

            string responseJson = await response.Content.ReadAsStringAsync();
            ChatResponse chatResponse;
            try
            {
                chatResponse = JsonSerializer.Deserialize<ChatResponse>(responseJson);
            }
            catch (Exception ex)
            {
                return $"Ошибка при разборе ответа: {ex.Message}";
            }

            if (chatResponse == null || chatResponse.choices == null || chatResponse.choices.Length == 0)
                return "Не удалось извлечь ответ ассистента.";

            return chatResponse.choices[0].message.content.Trim();
        }
    }
}
