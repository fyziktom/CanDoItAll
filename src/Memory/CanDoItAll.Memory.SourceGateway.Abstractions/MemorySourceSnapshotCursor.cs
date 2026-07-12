using System.Text;
using System.Text.Json;

namespace CanDoItAll.Memory.SourceGateway;
public readonly record struct MemorySourceSnapshotCursor
{
    public MemorySourceSnapshotCursor(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public static MemorySourceSnapshotCursor Create(
        MemorySourceKind sourceKind,
        Guid scopeId,
        string providerVersion,
        int position,
        MemorySourceItemId lastItemId,
        string snapshotAnchor = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerVersion);
        if (position <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Cursor position must be greater than zero.");
        }

        var payload = new MemorySourceSnapshotCursorPayload(
            sourceKind.ToString(),
            scopeId,
            providerVersion.Trim(),
            position,
            lastItemId.Value,
            snapshotAnchor.Trim());
        var json = JsonSerializer.Serialize(payload);
        return new MemorySourceSnapshotCursor(Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));
    }

    public static MemorySourceSnapshotCursorDescriptor? ReadDescriptorOrThrow(
        MemorySourceSnapshotCursor? cursor,
        MemorySourceKind expectedSourceKind,
        Guid expectedScopeId,
        string expectedProviderVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedProviderVersion);
        if (!cursor.HasValue)
        {
            return null;
        }

        var payload = ReadPayloadOrThrow(
            cursor.Value,
            expectedSourceKind,
            expectedScopeId,
            expectedProviderVersion);
        if (!Enum.TryParse(payload.SourceKind, ignoreCase: false, out MemorySourceKind sourceKind))
        {
            throw CreateException(
                MemorySourceSnapshotCursorFailureReason.InvalidFormat,
                cursor.Value,
                expectedSourceKind,
                expectedScopeId,
                expectedProviderVersion,
                $"Memory source snapshot cursor has unsupported source kind '{payload.SourceKind}'.");
        }

        ValidateContext(
            payload,
            sourceKind,
            cursor.Value,
            expectedSourceKind,
            expectedScopeId,
            expectedProviderVersion);
        var lastItemId = ReadLastItemIdOrThrow(
            payload.LastItemId,
            cursor.Value,
            expectedSourceKind,
            expectedScopeId,
            expectedProviderVersion);
        if (!MemorySourceItemId.TryParse(lastItemId, out var lastItemKey) ||
            lastItemKey.SourceKind != expectedSourceKind)
        {
            throw CreateException(
                MemorySourceSnapshotCursorFailureReason.InvalidFormat,
                cursor.Value,
                expectedSourceKind,
                expectedScopeId,
                expectedProviderVersion,
                "Memory source snapshot cursor last item anchor is not a supported source item id.");
        }

        return new MemorySourceSnapshotCursorDescriptor(
            sourceKind,
            payload.ScopeId,
            payload.ProviderVersion,
            payload.Position,
            lastItemId,
            payload.SnapshotAnchor ?? string.Empty);
    }

    public static void ThrowStaleAnchor(
        MemorySourceSnapshotCursor cursor,
        MemorySourceKind expectedSourceKind,
        Guid expectedScopeId,
        string expectedProviderVersion,
        string message)
        => throw CreateException(
            MemorySourceSnapshotCursorFailureReason.StaleAnchor,
            cursor,
            expectedSourceKind,
            expectedScopeId,
            expectedProviderVersion,
            message);

    public override string ToString() => Value;

    private static MemorySourceSnapshotCursorPayload ReadPayloadOrThrow(
        MemorySourceSnapshotCursor cursor,
        MemorySourceKind expectedSourceKind,
        Guid expectedScopeId,
        string expectedProviderVersion)
    {
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(cursor.Value));
            return JsonSerializer.Deserialize<MemorySourceSnapshotCursorPayload>(json)
                ?? throw new JsonException("Cursor payload is empty.");
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentException)
        {
            throw CreateException(
                MemorySourceSnapshotCursorFailureReason.InvalidFormat,
                cursor,
                expectedSourceKind,
                expectedScopeId,
                expectedProviderVersion,
                "Memory source snapshot cursor is not a supported cursor payload.",
                exception);
        }
    }

    private static void ValidateContext(
        MemorySourceSnapshotCursorPayload payload,
        MemorySourceKind sourceKind,
        MemorySourceSnapshotCursor cursor,
        MemorySourceKind expectedSourceKind,
        Guid expectedScopeId,
        string expectedProviderVersion)
    {
        var failure = payload.ProviderVersion != expectedProviderVersion
            ? (MemorySourceSnapshotCursorFailureReason.ProviderVersionMismatch, $"Memory source snapshot cursor provider version '{payload.ProviderVersion}' does not match expected version '{expectedProviderVersion}'.")
            : sourceKind != expectedSourceKind
                ? (MemorySourceSnapshotCursorFailureReason.SourceKindMismatch, $"Memory source snapshot cursor source kind '{sourceKind}' does not match expected kind '{expectedSourceKind}'.")
                : payload.ScopeId != expectedScopeId
                    ? (MemorySourceSnapshotCursorFailureReason.ScopeMismatch, $"Memory source snapshot cursor scope '{payload.ScopeId:D}' does not match expected scope '{expectedScopeId:D}'.")
                    : payload.Position <= 0
                        ? (MemorySourceSnapshotCursorFailureReason.InvalidFormat, "Memory source snapshot cursor position must be greater than zero.")
                        : ((MemorySourceSnapshotCursorFailureReason Reason, string Message)?)null;
        if (failure is { } invalid)
        {
            throw CreateException(
                invalid.Reason,
                cursor,
                expectedSourceKind,
                expectedScopeId,
                expectedProviderVersion,
                invalid.Message);
        }
    }

    private static MemorySourceItemId ReadLastItemIdOrThrow(
        string value,
        MemorySourceSnapshotCursor cursor,
        MemorySourceKind expectedSourceKind,
        Guid expectedScopeId,
        string expectedProviderVersion)
    {
        try
        {
            return new MemorySourceItemId(value);
        }
        catch (ArgumentException exception)
        {
            throw CreateException(
                MemorySourceSnapshotCursorFailureReason.InvalidFormat,
                cursor,
                expectedSourceKind,
                expectedScopeId,
                expectedProviderVersion,
                "Memory source snapshot cursor last item anchor is empty or unsupported.",
                exception);
        }
    }

    private static MemorySourceSnapshotCursorException CreateException(
        MemorySourceSnapshotCursorFailureReason reason,
        MemorySourceSnapshotCursor cursor,
        MemorySourceKind expectedSourceKind,
        Guid expectedScopeId,
        string expectedProviderVersion,
        string message,
        Exception? innerException = null)
        => new(
            reason,
            expectedSourceKind,
            expectedScopeId,
            expectedProviderVersion,
            cursor,
            message,
            innerException);

    private sealed record MemorySourceSnapshotCursorPayload(
        string SourceKind,
        Guid ScopeId,
        string ProviderVersion,
        int Position,
        string LastItemId,
        string? SnapshotAnchor);
}
