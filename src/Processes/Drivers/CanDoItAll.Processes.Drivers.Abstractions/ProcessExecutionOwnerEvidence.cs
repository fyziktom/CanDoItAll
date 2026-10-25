namespace CanDoItAll.Processes.Drivers.Abstractions;

public abstract class ProcessExecutionOwnerEvidence {
    private protected ProcessExecutionOwnerEvidence() { }
}

public sealed class ProcessExecutionOwnerEvidence<T>(T value) : ProcessExecutionOwnerEvidence where T : class {
    public T Value { get; } = value ?? throw new ArgumentNullException(nameof(value));
}
