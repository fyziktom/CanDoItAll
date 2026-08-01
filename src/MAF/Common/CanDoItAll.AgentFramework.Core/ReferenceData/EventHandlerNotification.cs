using System.Runtime.ExceptionServices;

namespace CanDoItAll.AgentFramework.Core;

internal static class EventHandlerNotification
{
    public static void NotifyAll(EventHandler? handlers, object sender)
    {
        if (handlers is null)
        {
            return;
        }

        List<Exception>? exceptions = null;
        foreach (var subscriber in handlers.GetInvocationList())
        {
            try
            {
                ((EventHandler)subscriber)(sender, EventArgs.Empty);
            }
            catch (Exception exception)
            {
                (exceptions ??= []).Add(exception);
            }
        }

        if (exceptions is null)
        {
            return;
        }

        if (exceptions.Count == 1)
        {
            ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
        }

        throw new AggregateException("One or more invalidation subscribers failed.", exceptions);
    }
}
