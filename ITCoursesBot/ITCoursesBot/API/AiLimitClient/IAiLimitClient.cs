namespace Bot.Ports
{
    public interface IAiLimitClient
    {
        /// <summary>Зарегистрировать факт одного запроса к ИИ. Без тела.</summary>
        Task RecordRequestAsync(long telegramId, CancellationToken ct = default);

        /// <summary>Узнать, достиг ли пользователь лимита (true/false).</summary>
        Task<bool> IsLimitReachedAsync(long telegramId, CancellationToken ct = default);

        /// <summary>Попытаться сбросить счётчик, если прошло ≥ 12 ч.</summary>
        Task<bool> ResetAsync(long telegramId, CancellationToken ct = default);

        /// <summary>Принудительно сбросить счётчик (нет тела, в ответ — NoContent).</summary>
        Task ForceResetAsync(long telegramId, CancellationToken ct = default);

        /// <summary>
        /// Получить информацию о количестве оставшихся запросов и времени до сброса лимита.
        /// </summary>
        Task<RequestInfo> GetRequestInfoAsync(long telegramId, CancellationToken ct = default);

        /// <summary>
        /// Применить промокод к пользователю.
        /// </summary>
        Task ApplyPromoCodeAsync(long telegramId, string promoCode, CancellationToken ct = default);
    }
}

public record RequestInfo(int RemainingRequests, TimeSpan TimeToReset);

