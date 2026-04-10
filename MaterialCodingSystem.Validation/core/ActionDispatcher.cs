namespace MaterialCodingSystem.Validation.core;

public class ActionDispatcher
{
    private readonly Dictionary<string, Func<Context, object>> _map;

    public ActionDispatcher()
    {
        _map = new Dictionary<string, Func<Context, object>>();
    }

    public ActionDispatcher(Dictionary<string, Func<Context, object>> map)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
    }

    public object Dispatch(string action, Context context)
    {
        if (!_map.TryGetValue(action, out var fn))
            throw new ValidationException("ACTION_NOT_FOUND", $"ACTION_NOT_FOUND: {action}");

        try
        {
            return fn(context);
        }
        catch (Exception ex)
        {
            return ex; // 关键：异常作为结果返回
        }
    }

    // no built-in actions: injected by caller
}