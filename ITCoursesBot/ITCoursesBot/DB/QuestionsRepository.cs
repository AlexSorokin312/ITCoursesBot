using ITCoursesBot.DB;
using Microsoft.EntityFrameworkCore;

namespace ITCoursesBot.ITCoursesBot.Services
{
    public sealed record QuestionDto(int Id, string Text);


    public class QuestionsRepository
    {
        private readonly BotDbContext _db;

        private IReadOnlyDictionary<string, List<QuestionDto>> QuestionsMap { get; }

        public QuestionsRepository(BotDbContext db)
        {
            _db = db;
            QuestionsMap = LoadQuestions();
        }

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
        public List<QuestionDto> GetQuestions(string combinedKey) =>
            QuestionsMap.TryGetValue(combinedKey, out var list) ? list : new();
    }
}
