namespace CanDoItAll.AgentFramework.Memory;

public static class MemoryAgentContextContributionTraceKeys
{
    public const string Reason = "reason";
    public const string Status = "status";
    public const string RequiredCapability = "requiredCapability";
    public const string ProviderInstanceId = "providerInstanceId";
    public const string ProviderCount = "providerCount";
    public const string FailedProviders = "failedProviders";
    public const string OperationId = "operationId";
    public const string StatusPath = "statusPath";
    public const string DispatchAttempted = "dispatchAttempted";
    public const string Diagnostic = "diagnostic";
}

public static class MemoryAgentContextContributionTraceReasons
{
    public const string Disabled = "memory-context-disabled";
    public const string DirectiveRequired = "memory-directive-required";
    public const string InvalidDirective = "invalid-memory-directive";
    public const string EmptyContextPack = "empty-context-pack";
    public const string AsyncAccepted = "async-operation-accepted";
    public const string NoProviderConfigured = "no-provider-configured";
    public const string NoEnabledProvider = "no-enabled-provider";
    public const string ProviderNotFound = "provider-not-found";
    public const string ProviderDisabled = "provider-disabled";
    public const string ProviderDenied = "provider-denied";
    public const string CapabilityDenied = "capability-denied";
    public const string CapabilityUnavailable = "capability-unavailable";
    public const string CapabilityMismatch = "capability-mismatch";
    public const string DriverUnavailable = "driver-unavailable";
    public const string TimedOut = "timed-out";
    public const string Failed = "memory-context-failed";
    public const string Completed = "memory-context-completed";
}
