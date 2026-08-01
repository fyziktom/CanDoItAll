using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

internal static class MemoryAcceptedOperationValidator
{
    private const int MaxStatusPathLength = 2_048;

    public static string? GetFailure(
        MemoryOperationRecord hostOperation,
        MemoryOperationAccepted acceptedOperation,
        DateTimeOffset acceptedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(hostOperation);
        ArgumentNullException.ThrowIfNull(acceptedOperation);
        if (acceptedOperation.OperationId != hostOperation.OperationId)
        {
            return "Memory provider accepted a different operation id than the host operation.";
        }

        if (acceptedOperation.PollAfter <= TimeSpan.Zero)
        {
            return "Memory provider returned a non-positive operation polling interval.";
        }

        if (acceptedOperation.ExpiresAtUtc <= acceptedAtUtc)
        {
            return "Memory provider returned an operation expiry that is not after acceptance.";
        }

        if (string.IsNullOrWhiteSpace(acceptedOperation.StatusPath))
        {
            return "Memory provider returned an empty operation status path.";
        }

        if (acceptedOperation.StatusPath.Length > MaxStatusPathLength ||
            acceptedOperation.StatusPath.Any(char.IsControl) ||
            acceptedOperation.StatusPath != acceptedOperation.StatusPath.Trim())
        {
            return "Memory provider returned an invalid operation status path.";
        }

        if (ContainsUriUserInfo(acceptedOperation.StatusPath))
        {
            return "Memory provider returned an operation status path containing URI user information.";
        }

        return null;
    }

    public static MemoryOperationAccepted CreateHostFacing(
        MemoryOperationAccepted acceptedOperation)
    {
        ArgumentNullException.ThrowIfNull(acceptedOperation);
        return acceptedOperation with
        {
            StatusPath = acceptedOperation.OperationId.Value.ToString("D"),
            CallbackAvailable = false
        };
    }

    private static bool ContainsUriUserInfo(string statusPath)
    {
        if (Uri.TryCreate(statusPath, UriKind.Absolute, out var absoluteUri) &&
            !string.IsNullOrEmpty(absoluteUri.UserInfo))
        {
            return true;
        }

        return statusPath.StartsWith("//", StringComparison.Ordinal) &&
            Uri.TryCreate($"https:{statusPath}", UriKind.Absolute, out var networkPath) &&
            !string.IsNullOrEmpty(networkPath.UserInfo);
    }
}
