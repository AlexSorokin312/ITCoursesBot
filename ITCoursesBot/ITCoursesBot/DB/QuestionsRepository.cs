using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ITCoursesBot.DB;

namespace ITCoursesBot.ITCoursesBot.Services
{

    /// <summary>Мини‑объект, который содержит Id вопроса и его чистый текст.</summary>
    public sealed record QuestionDto(int Id, string Text);

    /// <summary>
    /// Репозиторий, который загружает из БД все уроки и их вопросы
    /// и хранит их в памяти в виде словаря:
    /// ключ = Course.ShortName + Major + Minor,
    /// значение = список чистых текстов вопросов.
    /// </summary>
    public class QuestionsRepository
    {
        private readonly BotDbContext _db;

        /// <summary>
        /// Ключ: $"{ShortName}{Major}{Minor}", например "База21"
        /// Значение: список **сырого** текста вопросов.
        /// </summary>
        private IReadOnlyDictionary<string, List<QuestionDto>> QuestionsMap { get; }

        public QuestionsRepository(BotDbContext db)
        {
            _db = db;
            QuestionsMap = LoadQuestions();
        }

        /// <summary>
        /// Загружает из БД все уроки и сохраняет чистые тексты вопросов.
        /// </summary>
        private Dictionary<string, List<QuestionDto>> LoadQuestions()
        {
            var lessons = _db.Lessons
                             .AsNoTracking()
                             .Include(l => l.Course)
                             .Include(l => l.Questions)
                             .ToList();

            var dict = new Dictionary<string, List<QuestionDto>>();

            foreach (var lesson in lessons)
            {
                var key = $"{lesson.Course.ShortName}{lesson.Major}{lesson.Minor}";
                var questions = lesson.Questions
                                      .Select(q => new QuestionDto(q.Id, q.Text.Trim()))
                                      .ToList();
                dict[key] = questions;
            }

            return dict;
        }

        /// <summary>
        /// Возвращает список DTO‑вопросов для нужного ключа; если ключа нет — пустой список.
        /// </summary>
        public List<QuestionDto> GetQuestions(string combinedKey) =>
            QuestionsMap.TryGetValue(combinedKey, out var list) ? list : new();
    }
}
