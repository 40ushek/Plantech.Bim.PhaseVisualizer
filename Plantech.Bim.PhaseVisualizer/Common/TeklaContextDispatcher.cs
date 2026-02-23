using Serilog;
using System;
using System.Threading;

namespace Plantech.Bim.PhaseVisualizer.Common;

/// <summary>
/// Dispatches a delegate to the Tekla SynchronizationContext when required,
/// with a direct-call fallback on context absence or dispatch failure.
/// </summary>
internal static class TeklaContextDispatcher
{
    /// <summary>
    /// Runs <paramref name="func"/> on <paramref name="teklaContext"/> when it differs from the
    /// current context. Falls back to a direct call if context is null or <c>Send</c> throws.
    /// Returns <c>default</c> (null / Nullable without value) if <paramref name="func"/> itself throws.
    /// </summary>
    public static T? Run<T>(
        SynchronizationContext? teklaContext,
        Func<T> func,
        ILogger? log = null,
        string? noContextWarning = null,
        string? sendFailedWarning = null)
    {
        if (teklaContext == null)
        {
            if (noContextWarning != null)
                log?.Warning(noContextWarning);
            return SafeExecute(func);
        }

        if (SynchronizationContext.Current == teklaContext)
            return SafeExecute(func);

        T? result = default;
        try
        {
            teklaContext.Send(_ => { result = SafeExecute(func); }, null);
        }
        catch (Exception ex)
        {
            if (sendFailedWarning != null)
                log?.Warning(ex, sendFailedWarning);
            return SafeExecute(func);
        }

        return result;
    }

    private static T? SafeExecute<T>(Func<T> func)
    {
        try
        {
            return func();
        }
        catch
        {
            return default;
        }
    }
}
