using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Maf;

public static class MafWorkflowFailureDetails
{
    public static string CreateDetailedMessage(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (IsReflectionWrapper(current))
            {
                continue;
            }

            var message = WorkflowExecutorRedaction.RedactText(current.Message);
            if (string.IsNullOrWhiteSpace(message))
            {
                continue;
            }

            if (messages.Count > 0 && messages[^1].Contains(message, StringComparison.Ordinal))
            {
                continue;
            }

            if (messages.Count > 0 && message.Contains(messages[^1], StringComparison.Ordinal))
            {
                messages[^1] = message;
                continue;
            }

            messages.Add(message);
        }

        return messages.Count == 0
            ? WorkflowExecutorRedaction.RedactText(exception.Message)
            : string.Join(" ", messages);
    }

    public static Exception ResolveRootException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var root = exception;
        while (IsReflectionWrapper(root) && root.InnerException is { } inner)
        {
            root = inner;
        }

        return root;
    }

    public static bool TryResolveDiagnostic(
        Exception exception,
        [NotNullWhen(true)] out WorkflowFailureDiagnosticEnvelope? diagnostic)
    {
        ArgumentNullException.ThrowIfNull(exception);

        diagnostic = WorkflowExecutorFailureDiagnosticMapper.GetDiagnostics(exception).FirstOrDefault();
        return diagnostic is not null;
    }

    private static bool IsReflectionWrapper(Exception exception)
        => exception is TargetInvocationException or AggregateException;
}
