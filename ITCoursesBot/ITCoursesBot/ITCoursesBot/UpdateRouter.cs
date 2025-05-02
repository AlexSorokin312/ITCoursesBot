using Telegram.Bot.Types;

public class UpdateRouter
{
    private readonly IEnumerable<BaseController> _controllers;

    public UpdateRouter(IEnumerable<BaseController> controllers) =>
        _controllers = controllers;

    public async Task RouteAsync(Update update, CancellationToken ct)
    {
        Console.WriteLine($"[DEBUG] Controllers found: {_controllers.Count()}");
        foreach (var c in _controllers)
            Console.WriteLine($"[DEBUG] → {c.GetType().Name}");

        foreach (var ctrl in _controllers)
        {
            // каждый контроллер внутри конструктора проверяет Update
            ctrl.SetUpdate(update);
            if (await ctrl.HandleAsync(ct))
                break; // если контроллер «забрал» апдейт — выходим
        }
    }
}