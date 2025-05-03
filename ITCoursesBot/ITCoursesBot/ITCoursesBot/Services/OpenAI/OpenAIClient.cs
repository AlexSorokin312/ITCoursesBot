using ITCoursesBot.Interfaces;
using ITCoursesBot.ITCoursesBot.Configuration;
using System.Text;
using System.Text.Json;

namespace ITCoursesBot.ITCoursesBot.Services.OpenAI
{
    public class OpenAIClient : IOpenAIClient
    {
        private readonly HttpClient _httpClient;
        private readonly OpenAISettings _settings;

        public OpenAIClient(HttpClient httpClient, OpenAISettings settings)
        {
            _httpClient = httpClient;
            _settings = settings;
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.ApiKey);
        }

        public async Task<string> GetChatResponseAsync(string userMessage)
            => await GetChatResponseAsync(_settings.InstructionsDialog, userMessage);

        public async Task<string> GetChatResponseAsync(string systemInstructions, string userMessage)
        {
            var body = new
            {
                model = "gpt-4.1",
                messages = new object[]
                {
                    new { role = "system", content = systemInstructions },
                    new { role = "user",   content = userMessage }
                }
            };
            var content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json"
            );

            using var resp = await _httpClient.PostAsync(
                "https://api.openai.com/v1/chat/completions",
                content
            );
            resp.EnsureSuccessStatusCode();
            using var stream = await resp.Content.ReadAsStreamAsync();
            var doc = await JsonDocument.ParseAsync(stream);
            var msg = doc.RootElement
                         .GetProperty("choices")[0]
                         .GetProperty("message")
                         .GetProperty("content")
                         .GetString()
                         ?.Trim() ?? String.Empty;
            return msg;
        }

        public async Task<AnswerResult> EvaluateAsync(string questionText,
                                                      string userAnswer,
                                                      CancellationToken cancellationToken)
        {
            // Формируем промпт для оценки
            var prompt = $@"
Вопрос: {questionText}
Правильный ответ: (вставьте здесь, если он у вас есть в модели вопроса)
Ответ студента: {userAnswer}

Пожалуйста, верни JSON:
{{
  ""isCorrect"": true/false,
  ""comment"": ""короткий комментарий к ответу""
}}";

            // Запрашиваем OpenAI
            var raw = await GetChatResponseAsync(_settings.InstructionsQuestions, prompt);

            // Парсим JSON-ответ
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                return new AnswerResult
                {
                    IsCorrect = root.GetProperty("isCorrect").GetBoolean(),
                    Comment = root.GetProperty("comment").GetString() ?? String.Empty
                };
            }
            catch (JsonException)
            {
                // Если не JSON, просто возвращаем весь текст как комментарий,
                // считая, что ответ некорректен
                return new AnswerResult
                {
                    IsCorrect = false,
                    Comment = raw
                };
            }
        }

        public async Task<string> TranscribeAudioAsync(Stream audioStream, string fileName)
        {
            using var form = new MultipartFormDataContent();
            audioStream.Position = 0;
            form.Add(new StreamContent(audioStream), "file", fileName);
            form.Add(new StringContent("gpt-4o-transcribe"), "model");

            var resp = await _httpClient.PostAsync(
                "https://api.openai.com/v1/audio/transcriptions",
                form
            );
            resp.EnsureSuccessStatusCode();

            using var stream = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            return doc.RootElement.GetProperty("text").GetString() ?? String.Empty;
        }

        public async Task<Stream> GenerateSpeechAsync(string text,
                                                      string model = "tts-1",
                                                      string voice = "alloy",
                                                      string format = "opus")
        {
            var payload = new
            {
                model = model,
                input = text,
                voice = voice,
                response_format = format
            };
            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            using var resp = await _httpClient.PostAsync(
                "https://api.openai.com/v1/audio/speech",
                content
            );
            resp.EnsureSuccessStatusCode();

            var ms = new MemoryStream();
            await resp.Content.CopyToAsync(ms);
            ms.Position = 0;
            return ms;
        }
    }
}
