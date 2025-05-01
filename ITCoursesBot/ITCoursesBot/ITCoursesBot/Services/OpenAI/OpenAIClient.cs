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
                model = "gpt-4.1", // укажите нужную модель
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        // Используем инструкции из конфигурационного файла
                        content = _openAISettings.InstructionsDialog
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


        public async Task<string> GetChatResponseAsync(string systemInstructions, string userMessage)
        {
            var requestBody = new
            {
                model = "gpt-4.1",
                messages = new object[]
                {
                new { role = "system", content = systemInstructions ?? throw new ArgumentNullException(nameof(systemInstructions)) },
                new { role = "user",   content = userMessage        ?? string.Empty }
                }
            };

            var jsonRequest = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                return $"Ошибка OpenAI: {response.StatusCode}\n{err}";
            }

            var respJson = await response.Content.ReadAsStringAsync();
            var chatResp = JsonSerializer.Deserialize<ChatResponse>(respJson)
                           ?? throw new Exception("Пустой ответ от ChatGPT");
            return chatResp.choices[0].message.content.Trim();
        }
        public async Task<string> TranscribeAudioAsync(Stream audioStream, string fileName)
        {
            // Whisper  принимает multipart/form-data
            using var form = new MultipartFormDataContent();
            // Важно: позиционируемся в начало, иначе будет пустой поток
            if (audioStream.CanSeek) audioStream.Position = 0;

            form.Add(new StreamContent(audioStream), "file", fileName);
            form.Add(new StringContent("whisper-1"), "model");
            // при желании можно добавить  form.Add(new StringContent("ru"), "language");


            HttpResponseMessage response =
                await _httpClient.PostAsync("https://api.openai.com/v1/audio/transcriptions", form);

            if (!response.IsSuccessStatusCode)
                return $"Ошибка Whisper API: {response.StatusCode}\n{await response.Content.ReadAsStringAsync()}";

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            // в /v1/audio/transcriptions поле "text" – сама расшифровка
            return doc.RootElement.GetProperty("text").GetString() ?? string.Empty;
        }

        public async Task<Stream> GenerateSpeechAsync(string text, string model = "tts-1", string voice = "alloy", string format = "opus")
        {
            // Формируем тело запроса
            var payload = new
            {
                model = model,
                input = text,
                voice = voice,
                response_format = format    // по умолчанию opus для Telegram Voice
            };
            string json = JsonSerializer.Serialize(payload);

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            HttpResponseMessage response =
                await _httpClient.PostAsync("https://api.openai.com/v1/audio/speech", content);

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"Ошибка TTS API: {response.StatusCode}\n{await response.Content.ReadAsStringAsync()}");

            // Читаем бинарный ответ в поток
            var ms = new MemoryStream();
            await response.Content.CopyToAsync(ms);
            ms.Position = 0;
            return ms;
        }
    }
}