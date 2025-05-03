// ITCoursesBot/ITCoursesBot/Data/DataSeeder.cs
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ITCoursesBot.DB;
using ITCoursesBot.ITCoursesBot.Data;

namespace ITCoursesBot.ITCoursesBot.Data
{
    public static class DataSeeder
    {
        /// <summary>
        /// Однократно заполняет базу данными из quizData.json.
        /// Для каждого курса создаёт Course (с ShortName из JSON),
        /// затем для каждого элемента в Lessons — Lesson с Major/Minor/Title и связанные Question.
        /// </summary>
        public static void Seed(BotDbContext db)
        {
            // 1) Прочитать весь JSON
            var json = File.ReadAllText("quizData.json");
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement.GetProperty("QuizData");

            // 2) Пройтись по каждому курсу в QuizData
            foreach (var courseProp in root.EnumerateObject())
            {
                var courseName = courseProp.Name;
                var courseObj = courseProp.Value;

                // Должно быть объектом с полями ShortName и Lessons
                if (courseObj.ValueKind != JsonValueKind.Object)
                    continue;

                // Если курс уже есть — пропустить
                if (db.Courses.Any(c => c.Name == courseName))
                    continue;

                // Извлечь короткое имя из JSON
                var shortName = courseObj
                    .GetProperty("ShortName")
                    .GetString()!
                    .Trim();

                // Создать новый курс с ShortName из JSON
                var course = new Course
                {
                    Name = courseName,
                    ShortName = shortName
                };
                db.Courses.Add(course);

                // 3) Получить массив уроков
                if (!courseObj.TryGetProperty("Lessons", out var lessonsElem)
                    || lessonsElem.ValueKind != JsonValueKind.Array)
                    continue;

                // 4) Пройтись по каждому уроку
                foreach (var lessonElem in lessonsElem.EnumerateArray())
                {
                    if (lessonElem.ValueKind != JsonValueKind.Object)
                        continue;

                    // Извлечь Major и Minor
                    int major = lessonElem.GetProperty("Major").GetInt32();
                    int minor = lessonElem.GetProperty("Minor").GetInt32();

                    // Извлечь заголовок
                    string title = lessonElem
                        .GetProperty("Title")
                        .GetString()!
                        .Trim();

                    // Создать Lesson
                    var lesson = new Lesson
                    {
                        Major = major,
                        Minor = minor,
                        Title = title,
                        Course = course
                    };

                    // 5) Добавить вопросы
                    if (lessonElem.TryGetProperty("Questions", out var questionsElem)
                        && questionsElem.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var q in questionsElem.EnumerateArray())
                        {
                            if (q.ValueKind == JsonValueKind.String)
                            {
                                lesson.Questions.Add(new Question
                                {
                                    Text = q.GetString()!.Trim()
                                });
                            }
                        }
                    }

                    db.Lessons.Add(lesson);
                }
            }

            // 6) Сохранить изменения
            db.SaveChanges();
        }
    }
}
