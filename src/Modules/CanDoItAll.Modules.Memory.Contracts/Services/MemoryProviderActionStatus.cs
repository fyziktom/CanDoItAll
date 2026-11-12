namespace CanDoItAll.Modules.Memory.Services;

public enum MemoryProviderActionStatus
{
    Completed = 0,
    Accepted = 1,
    NoProviderConfigured = 2,
    NoEnabledProvider = 3,
    ProviderNotFound = 4,
    ProviderDisabled = 5,
    CapabilityUnavailable = 6,
    CapabilityDenied = 7,
    CapabilityMismatch = 8,
    DriverUnavailable = 9,
    SourceCaptureFailed = 10,
    NotFound = 11,
    Cancelled = 12,
    Failed = 13,
    TimedOut = 14,
    UnsupportedOperation = 15,
    ProviderDenied = 16,
    ProviderSelectionRequired = 17,
    AccessDenied = 18,
    ProviderConfigurationFailed = 19,
    DriverFailed = 20
}
