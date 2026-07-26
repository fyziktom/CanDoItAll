namespace CanDoItAll.Processes.Core;

public enum ProcessToolOperationIdempotencyPolicy
{
    Unspecified,
    CurrentRunRepeatable
}

public enum ProcessToolOperationFailureReconciliationPolicy
{
    None,
    AuthoritativeReadbackConvergence
}

public sealed record ProcessToolOperationExecutionPolicy(
    string OperationKey,
    string ToolName,
    ProcessToolOperationIdempotencyPolicy Idempotency,
    ProcessToolOperationFailureReconciliationPolicy FailureReconciliation);

public static class ProcessToolOperationExecutionPolicyKeys
{
    public const string CurrentRunRepeatable = "current-run-repeatable";
    public const string AuthoritativeReadbackConvergence = "authoritative-readback-convergence";

    public static bool TryResolveIdempotency(
        string? key,
        out ProcessToolOperationIdempotencyPolicy policy)
    {
        if (string.Equals(key?.Trim(), CurrentRunRepeatable, StringComparison.OrdinalIgnoreCase))
        {
            policy = ProcessToolOperationIdempotencyPolicy.CurrentRunRepeatable;
            return true;
        }

        policy = ProcessToolOperationIdempotencyPolicy.Unspecified;
        return false;
    }

    public static bool TryResolveFailureReconciliation(
        string? key,
        out ProcessToolOperationFailureReconciliationPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            policy = ProcessToolOperationFailureReconciliationPolicy.None;
            return true;
        }

        if (string.Equals(
                key.Trim(),
                AuthoritativeReadbackConvergence,
                StringComparison.OrdinalIgnoreCase))
        {
            policy = ProcessToolOperationFailureReconciliationPolicy.AuthoritativeReadbackConvergence;
            return true;
        }

        policy = ProcessToolOperationFailureReconciliationPolicy.None;
        return false;
    }
}
