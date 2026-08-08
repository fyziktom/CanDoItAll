using CanDoItAll.AgentFramework.Core;

using CanDoItAll.AgentFramework.Runtime.Abstractions;
namespace CanDoItAll.AgentFramework.Maf;

internal static class MafRuntimeFailureOriginClassifier
{
    public static int CountCompletedFailedTools(
        IReadOnlyList<AgentToolInvocationTrace> traces)
        => traces.Count(IsCompletedFailure);

    public static AgentRuntimeFailureOrigin ResolveProviderAdvanceFailure(
        int failedToolCountBeforeAdvance,
        IReadOnlyList<AgentToolInvocationTrace> tracesAfterAdvance,
        Exception exception)
    {
        if (IsToolInvocationBoundaryFailure(exception))
        {
            return AgentRuntimeFailureOrigin.Tool;
        }

        if (IsProviderConfigurationFailure(exception))
        {
            return AgentRuntimeFailureOrigin.ProviderConfiguration;
        }

        if (IsProviderTransportFailure(exception))
        {
            return AgentRuntimeFailureOrigin.Provider;
        }

        return CountCompletedFailedTools(tracesAfterAdvance) > failedToolCountBeforeAdvance
            ? AgentRuntimeFailureOrigin.Tool
            : AgentRuntimeFailureOrigin.Runtime;
    }

    public static AgentRuntimeFailureOrigin ResolveOutsideProviderBoundary(
        Exception exception,
        AgentRuntimeFailureOrigin defaultOrigin = AgentRuntimeFailureOrigin.Runtime)
    {
        if (exception is AgentRuntimeUsageException usageException)
        {
            return usageException.FailureOrigin;
        }

        if (IsToolInvocationBoundaryFailure(exception))
        {
            return AgentRuntimeFailureOrigin.Tool;
        }

        if (IsProviderConfigurationFailure(exception))
        {
            return AgentRuntimeFailureOrigin.ProviderConfiguration;
        }

        return IsProviderTransportFailure(exception)
            ? AgentRuntimeFailureOrigin.Provider
            : defaultOrigin;
    }

    public static AgentRuntimeProviderFailureIdentity? ResolveProviderFailureIdentity(
        Exception exception,
        AgentRuntimeFailureOrigin failureOrigin)
    {
        if (failureOrigin is not (AgentRuntimeFailureOrigin.Provider or AgentRuntimeFailureOrigin.ProviderConfiguration))
        {
            return null;
        }

        if (exception is AgentRuntimeUsageException { ProviderFailureIdentity: { } identity })
        {
            return identity;
        }

        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is MafProviderConfigurationException configurationException)
            {
                return configurationException.ProviderIdentity;
            }

            if (current is MafProviderTransportException
                {
                    ProviderKind: { } providerKind,
                    Transport: { } transport
                } transportException)
            {
                return new AgentRuntimeProviderFailureIdentity(
                    transportException.ProviderProfileId,
                    transportException.ProviderName,
                    providerKind,
                    transport,
                    transportException.Model);
            }

            if (current is AggregateException)
            {
                return null;
            }
        }

        return null;
    }

    private static bool IsCompletedFailure(AgentToolInvocationTrace trace)
        => trace.CompletedAtUtc.HasValue && !trace.Succeeded;

    private static bool IsProviderConfigurationFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is MafProviderConfigurationException)
            {
                return true;
            }

            if (current is AggregateException)
            {
                return false;
            }
        }

        return false;
    }

    private static bool IsProviderTransportFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is MafProviderTransportException)
            {
                return true;
            }

            if (current is AggregateException)
            {
                return false;
            }
        }

        return false;
    }

    private static bool IsToolInvocationBoundaryFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is MafToolInvocationBoundaryException)
            {
                return true;
            }

            if (current is AggregateException)
            {
                return false;
            }
        }

        return false;
    }
}
