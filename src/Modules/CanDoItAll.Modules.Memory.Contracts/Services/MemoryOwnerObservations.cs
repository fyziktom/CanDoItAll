using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Modules.Memory.Services;

public enum MemoryReadRegion { Operations, Feedback, Events }

public sealed class MemoryActionRefusedException(string message) : InvalidOperationException(message);

public sealed class MemoryDemoInterruptedException(IReadOnlyList<MemoryProviderProfile> saved, Exception inner)
    : Exception("Demo creation stopped. Confirmed profiles remain saved; review the catalog before continuing.", inner) {
    public IReadOnlyList<MemoryProviderProfile> Saved { get; } = saved.ToArray();
}

public static class MemoryDemoProviderIds {
    public const string Business = "provider.business-demo";
    public const string Programming = "provider.programming-demo";
}
