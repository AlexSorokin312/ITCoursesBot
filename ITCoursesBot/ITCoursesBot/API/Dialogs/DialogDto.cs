using System.ComponentModel.DataAnnotations;

public class CreateDialogRequest
{
    [Required]
    public long TelegramId { get; set; }

    [Required]
    public string Message { get; set; } = null!;

    /// <summary>Учитывать расход лимита (по умолчанию — да)</summary>
    public bool CountAsRequest { get; set; } = true;
}

public class DialogItem
{
    public int Id { get; set; }
    public long TelegramId { get; set; }
    public string Message { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

public class DeleteDialogResponse
{
    public int Deleted { get; set; }
}