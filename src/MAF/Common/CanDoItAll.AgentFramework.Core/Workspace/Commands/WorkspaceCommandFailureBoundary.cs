namespace CanDoItAll.AgentFramework.Core;

internal sealed class WorkspaceCommandInputException : InvalidOperationException, IAgentToolFailure
{
    private WorkspaceCommandInputException(
        string diagnosticMessage,
        string safeMessage,
        Exception? innerException,
        bool prohibited)
        : base(NormalizeMessage(diagnosticMessage, nameof(diagnosticMessage)), innerException)
    {
        SafeMessage = NormalizeMessage(safeMessage, nameof(safeMessage));
        Prohibited = prohibited;
    }

    public string ErrorCode => AgentToolInputValidationException.FailureCode;

    public string SafeMessage { get; }

    public bool IsSafeToExpose => true;

    public bool CanRetryWithCorrectedInput => true;

    /// <summary>
    /// The recipe never permits the requested operation on this target, so only a different approach can proceed.
    /// </summary>
    public bool Prohibited { get; }

    public static WorkspaceCommandInputException Create(
        string diagnosticMessage,
        string safeMessage,
        Exception? innerException = null)
        => new(diagnosticMessage, safeMessage, innerException, prohibited: false);

    public static WorkspaceCommandInputException CreateProhibited(
        string diagnosticMessage,
        string safeMessage)
        => new(diagnosticMessage, safeMessage, innerException: null, prohibited: true);

    private static string NormalizeMessage(string message, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message, parameterName);
        return message.Trim();
    }
}

internal static class WorkspaceCommandFailureBoundary
{
    public const string CleanupLoadFailureMessage =
        "Durable workspace process leases could not be loaded. The leases were retained for a later cleanup attempt.";

    public const string CleanupAttemptFailureMessage =
        "The workspace process lease cleanup attempt failed unexpectedly. The durable lease was retained for a later cleanup attempt.";

    public static bool TryGetSafeMessage(Exception exception, out string safeMessage)
    {
        ArgumentNullException.ThrowIfNull(exception);

        safeMessage = exception switch
        {
            WorkspacePathResolutionException pathFailure =>
                $"{pathFailure.SafeMessage} Correct the path and retry.",
            IAgentToolFailure { IsSafeToExpose: true } toolFailure => toolFailure.SafeMessage,
            _ => string.Empty
        };
        return !string.IsNullOrWhiteSpace(safeMessage);
    }
}
