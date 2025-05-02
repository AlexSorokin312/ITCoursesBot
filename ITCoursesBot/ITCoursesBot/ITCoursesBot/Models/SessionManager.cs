using System.Collections.Concurrent;
using ITCoursesBot.Interfaces;

namespace ITCoursesBot.ITCoursesBot.Models
{
    public class SessionManager : ISessionManager
    {
        private readonly ConcurrentDictionary<long, ChatSession> _sessions
            = new ConcurrentDictionary<long, ChatSession>();

        public bool TryAddSession(ChatSession session)
            => _sessions.TryAdd(session.ChatId, session);

        public bool TryRemoveSession(ChatSession session)
            => _sessions.TryRemove(session.ChatId, out _);

        public bool TryRemoveSession(long chatId)
            => _sessions.TryRemove(chatId, out _);

        public ChatSession TryGetSession(long chatId)
        {
            ChatSession session;
            _sessions.TryGetValue(chatId, out session);
            return session;
        }

        public ChatSession GetOrCreateSession(long chatId)
            => _sessions.GetOrAdd(chatId, id => new ChatSession(id));
    }
}
