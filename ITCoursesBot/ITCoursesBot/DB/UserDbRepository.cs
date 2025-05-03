using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using ITCoursesBot.DB;

namespace ITCoursesBot.ITCoursesBot.Services
{
    /// <summary>
    /// Репозиторий для работы с сущностью User:
    /// — при необходимости добавляет нового пользователя,
    /// — обновляет дату последнего использования и счетчик запусков.
    /// </summary>
    public class UserDbRepository
    {
        private readonly BotDbContext _db;

        public UserDbRepository(BotDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Возвращает пользователя с данным TelegramId. 
        /// Если его нет в БД — создаёт нового, заполняет FirstLaunch, LastUse = now, UsageCount = 1.
        /// </summary>
        public async Task<User> AddOrGetAsync(long telegramId, string username)
        {
            // пытаемся найти
            var user = await _db.Users
                                .SingleOrDefaultAsync(u => u.Id == telegramId)
                                .ConfigureAwait(false);

            if (user != null)
                return user;

            // создаём нового
            user = new User
            {
                Id = telegramId,
                Username = username ?? string.Empty,
                FirstLaunch = DateTime.UtcNow,
                LastUse = DateTime.UtcNow,
                UsageCount = 1
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync().ConfigureAwait(false);

            return user;
        }

        /// <summary>
        /// Обновляет для существующего пользователя дату LastUse = now и увеличивает UsageCount на 1.
        /// Если пользователь не найден — ничего не делает.
        /// </summary>
        public async Task UpdateUsageAsync(long telegramId)
        {
            var user = await _db.Users
                                .SingleOrDefaultAsync(u => u.Id == telegramId)
                                .ConfigureAwait(false);
            if (user == null)
                return;

            user.LastUse = DateTime.Now;
            user.UsageCount += 1;

            _db.Users.Update(user);

            await _db.SaveChangesAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Записывает НОВУЮ попытку ответа пользователя (всегда INSERT).
        /// </summary>
        /// <param name="telegramId">Telegram‑ID пользователя</param>
        /// <param name="username">username из Telegram (нужен, если пользователя ещё нет)</param>
        /// <param name="questionId">ID вопроса</param>
        /// <param name="answerText">Текст ответа</param>
        /// <param name="isCorrect">Оценка AI (true / false)</param>
        public async Task AddUserAnswerAsync(
            long telegramId,
            string username,
            int questionId,
            string answerText,
            bool isCorrect)
        {
            // 1) гарантируем, что пользователь есть
            await AddOrGetAsync(telegramId, username);

            // 2) убеждаемся, что вопрос существует (если нужно)
            bool exists = await _db.Questions.AnyAsync(q => q.Id == questionId);
            if (!exists)
                throw new ArgumentException($"Question {questionId} not found", nameof(questionId));

            // 3) создаём новую запись
            var answer = new UserAnswer
            {
                UserId = telegramId,
                QuestionId = questionId,
                AnswerText = answerText,
                IsCorrect = isCorrect,
                AnsweredAt = DateTime.UtcNow
            };

            _db.UserAnswers.Add(answer);

            // 4) фиксируем INSERT
            await _db.SaveChangesAsync();
        }


        /// <summary>
        /// Получить информацию о пользователе (или null, если нет).
        /// </summary>
        public User? Get(long telegramId)
        {
            return _db.Users
                      .AsNoTracking()
                      .SingleOrDefault(u => u.Id == telegramId);
        }
    }
}
