public interface IDialogHistoryClient
{
    /// <summary>Сохранить сообщение в историю (вернёт ошибку, если 429/400 и т.д.).</summary>
    Task WriteAsync(CreateDialogRequest dto, CancellationToken ct = default);

    /// <summary>Получить историю пользователя с фильтрами по датам и пагинацией.</summary>
    Task<IReadOnlyList<DialogItem>> GetAsync(
        long telegramId,
        DateTime? from = null,
        DateTime? to = null,
        int skip = 0,
        int take = 50,
        CancellationToken ct = default);

    /// <summary>Удалить историю пользователя (опционально — до даты включительно). Возвращает число удалённых записей.</summary>
    Task<int> DeleteAsync(long telegramId, DateTime? to = null, CancellationToken ct = default);
}
