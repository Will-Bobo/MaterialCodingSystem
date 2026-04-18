namespace MaterialCodingSystem.Application.Logging;

/// <summary>同一异常链 stack trace 仅记录一次（Infrastructure 优先）。</summary>
public static class McsExceptionMarker
{
    private const string DataKey = "Mcs.StackLogged";

    public static bool IsLogged(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException!)
        {
            if (e.Data.Contains(DataKey) && e.Data[DataKey] is true)
                return true;
        }

        return false;
    }

    public static void MarkLogged(Exception ex)
    {
        try
        {
            ex.Data[DataKey] = true;
        }
        catch
        {
            // ignore Data mutability failures
        }
    }
}
