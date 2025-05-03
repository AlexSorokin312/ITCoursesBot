using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ITCoursesBot.DB;

namespace ITCoursesBot.ITCoursesBot.Services
{
    /// <summary>
    /// Репозиторий, который загружает из БД все уроки и их вопросы
    /// и хранит их в памяти в виде словаря:
    /// ключ = Course.ShortName + Major + Minor,
    /// значение = список чистых текстов вопросов.
    /// </summary>
    public class DBRepository
    {
        private readonly BotDbContext _db;

        /// <summary>
        /// Ключ: $"{ShortName}{Major}{Minor}", например "База21"
        /// Значение: список **сырого** текста вопросов.
        /// </summary>
        private IReadOnlyDictionary<string, List<string>> QuestionsMap { get; }

        public DBRepository(BotDbContext db)
        {
            _db = db;
            QuestionsMap = LoadQuestions();
        }

        /// <summary>
        /// Загружает из БД все уроки и сохраняет чистые тексты вопросов.
        /// </summary>
        private Dictionary<string, List<string>> LoadQuestions()
        {
            var lessons = _db.Lessons
                .AsNoTracking()
                .Include(l => l.Course)
                .Include(l => l.Questions)
                .ToList();

            var dict = new Dictionary<string, List<string>>();

            foreach (var lesson in lessons)
            {
                var key = $"{lesson.Course.ShortName}{lesson.Major}{lesson.Minor}";

                // Сохраняем только сырой текст вопроса
                var rawTexts = lesson.Questions
                                     .Select(q => q.Text.Trim())
                                     .ToList();

                dict[key] = rawTexts;
            }

            return dict;
        }

        /// <summary>
        /// Возвращает отформатированный список вопросов по ключу.
        /// Если ключ не найден — возвращает пустой список.
        /// </summary>
        public List<string> GetQuestions(string combinedKey)
        {
            if (!QuestionsMap.TryGetValue(combinedKey, out var rawList))
                return new List<string>();

            int total = rawList.Count;
            return rawList
                .Select((text, i) =>
                    // Добавляем форматирование только здесь
                    $"❓ Вопрос {i + 1}/{total}:\n{text}"
                )
                .ToList();
        }
    }
}
