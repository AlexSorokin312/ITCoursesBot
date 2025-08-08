public class UpdateMiddlewarePipeline
{
    private readonly IList<IUpdateMiddleware> _middlwWare;
    public UpdateMiddlewarePipeline(IEnumerable<IUpdateMiddleware> seq)
        => _middlwWare = seq.ToList();

    public Task ProcessAsync(UpdateContext ctx)
        => Invoke(0, ctx);

    private Task Invoke(int i, UpdateContext ctx)
        => i >= _middlwWare.Count
           ? Task.CompletedTask
           : _middlwWare[i].InvokeAsync(ctx, () => Invoke(i + 1, ctx));
}