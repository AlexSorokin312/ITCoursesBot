// Interfaces/ISessionManager.cs
namespace ITCoursesBot.Interfaces
{
    /// <summary>
    /// Менеджер сессий пользователей бота.
    /// </summary>
    public interface ISessionManager
    {
        /// <summary>
        /// Пытается добавить новую сессию.
        /// Возвращает false, если сессия с таким chatId уже существует.
        /// </summary>
        bool TryAddSession(ChatSession session);

        /// <summary>
        /// Пытается удалить сессию по объекту.
        /// Возвращает false, если сессии с таким chatId не было.
        /// </summary>
        bool TryRemoveSession(ChatSession session);

        /// <summary>
        /// Пытается удалить сессию по chatId.
        /// Возвращает false, если сессии с таким chatId не было.
        /// </summary>
        bool TryRemoveSession(long chatId);

        /// <summary>
        /// Пытается получить сессию по chatId.
        /// </summary>
        ChatSession TryGetSession(long chatId);

        /// <summary>
        /// Возвращает текущую сессию или, если её нет, создаёт новую и добавляет.
        /// </summary>
        ChatSession GetOrCreateSession(long chatId);
    }
}
