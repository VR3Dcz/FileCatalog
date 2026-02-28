using System;
using System.Threading.Tasks;

namespace FileCatalog.Utils;

public static class TaskExtensions
{
    /// <summary>
    /// Safely executes an asynchronous task without waiting for its completion.
    /// If an exception occurs during execution, it is caught and routed to the provided error handler,
    /// preventing the application's main thread from crashing.
    /// </summary>
    public static void SafeFireAndForget(this Task task, Action<Exception>? onException = null)
    {
        task.ContinueWith(t =>
        {
            var ex = t.Exception?.GetBaseException();
            if (ex != null)
            {
                onException?.Invoke(ex);
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }
}