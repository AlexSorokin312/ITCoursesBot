using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ITCoursesBot.Services
{
    public class LessonService
    {
        private readonly Dictionary<string, string[]> _questions;

        public LessonService(string jsonPath)
        {
            if (!File.Exists(jsonPath))
                throw new FileNotFoundException($"Файл не найден: {jsonPath}");

            string text = File.ReadAllText(jsonPath);
            _questions = JsonSerializer.Deserialize<Dictionary<string, string[]>>(text)
                         ?? new Dictionary<string, string[]>();
        }

        public bool TryGetQuestions(string lessonKey, out string[] questions)
        {
            return _questions.TryGetValue(lessonKey, out questions!);
        }
    }
}
