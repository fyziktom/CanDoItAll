using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.Security;

public static class SecretMutationScopeKeys
{
    private const string SecretRecordPrefix = "security:secret-record:";

    public static string ForSecretRecord(Guid secretRecordId)
    {
        if (secretRecordId == Guid.Empty)
        {
            throw new ArgumentException(
                "The secret record id cannot be empty.",
                nameof(secretRecordId));
        }

        return $"{SecretRecordPrefix}{secretRecordId:D}";
    }

    public static IReadOnlyList<string> ForSecretRecords(
        params Guid?[] secretRecordIds)
    {
        ArgumentNullException.ThrowIfNull(secretRecordIds);
        return Array.AsReadOnly(secretRecordIds
            .Where(secretRecordId =>
                secretRecordId.HasValue &&
                secretRecordId.Value != Guid.Empty)
            .Select(secretRecordId =>
                ForSecretRecord(secretRecordId!.Value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray());
    }
}

public sealed record SecretDeletionReference
{
    public SecretDeletionReference(string sanitizedReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sanitizedReason);
        SanitizedReason = sanitizedReason.Trim();
    }

    public string SanitizedReason { get; }
}

public interface ISecretDeletionReferencePolicy
{
    Task<SecretDeletionReference?> FindReferenceAsync(
        AppDbContext dbContext,
        Guid secretRecordId,
        CancellationToken cancellationToken);
}

public sealed class SecretDeletionBlockedException(
    Guid secretRecordId,
    IReadOnlyList<SecretDeletionReference> references)
    : InvalidOperationException(BuildMessage(secretRecordId, references))
{
    public Guid SecretRecordId { get; } = secretRecordId;

    public IReadOnlyList<SecretDeletionReference> References { get; } =
        references;

    private static string BuildMessage(
        Guid secretRecordId,
        IReadOnlyList<SecretDeletionReference> references)
    {
        ArgumentNullException.ThrowIfNull(references);
        if (secretRecordId == Guid.Empty)
        {
            throw new ArgumentException(
                "The secret record id cannot be empty.",
                nameof(secretRecordId));
        }

        if (references.Count == 0)
        {
            throw new ArgumentException(
                "At least one blocking secret reference is required.",
                nameof(references));
        }

        return $"The secret record cannot be deleted. {string.Join(" ", references.Select(reference => reference.SanitizedReason))}";
    }
}
