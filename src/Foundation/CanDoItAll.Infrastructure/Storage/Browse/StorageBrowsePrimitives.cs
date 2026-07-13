namespace CanDoItAll.Infrastructure.Storage;

[Flags]
public enum StorageBrowseCapability
{
    None = 0,
    Browse = 1 << 0,
    Stat = 1 << 1,
    Search = 1 << 2,
    ProviderNativeOrdering = 1 << 3,
    GlobalNameOrdering = 1 << 4,
    ConsistentContinuation = 1 << 5,
    Metadata = 1 << 6,
    ImmutableVersion = 1 << 7
}

[Flags]
public enum StorageBrowseEntryCapability
{
    None = 0,
    Browse = 1 << 0,
    Read = 1 << 1,
    Write = 1 << 2,
    Delete = 1 << 3
}

[Flags]
public enum StorageBrowseMetadataField
{
    None = 0,
    Size = 1 << 0,
    CreatedAtUtc = 1 << 1,
    ModifiedAtUtc = 1 << 2,
    MediaType = 1 << 3
}

public enum StorageBrowseEntryKind
{
    File,
    Container,
    Link
}

public enum StorageBrowseSortField
{
    ProviderNative,
    Name,
    Size,
    ModifiedAtUtc
}

public enum StorageBrowseSortDirection
{
    Ascending,
    Descending
}

public enum StorageBrowseCompleteness
{
    Complete,
    PartialInspectionLimit,
    PartialMetadataLimit,
    PartialTimeLimit
}

public enum StorageBrowseErrorCode
{
    InvalidConfiguration,
    InvalidRequest,
    InvalidCursor,
    DuplicateProviderRegistration,
    ProviderNotRegistered,
    UnsupportedOperation,
    BudgetExceeded,
    SourceChanged,
    ProviderUnavailable,
    AccessDenied
}

public sealed record StorageBrowseError
{
    public StorageBrowseError(StorageBrowseErrorCode code, string message, bool isRetryable = false)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("A storage browse error message is required.", nameof(message));
        }

        Code = code;
        Message = message.Trim();
        IsRetryable = isRetryable;
    }

    public StorageBrowseErrorCode Code { get; }

    public string Message { get; }

    public bool IsRetryable { get; }
}

public sealed class StorageBrowseException : InvalidOperationException
{
    public StorageBrowseException(StorageBrowseError error)
        : base(error?.Message)
    {
        Error = error ?? throw new ArgumentNullException(nameof(error));
    }

    public StorageBrowseException(StorageBrowseError error, Exception innerException)
        : base(error?.Message, innerException)
    {
        Error = error ?? throw new ArgumentNullException(nameof(error));
    }

    public StorageBrowseError Error { get; }
}

public sealed record StorageBrowseContainer
{
    public const int MaximumKeyLength = 4096;

    public static StorageBrowseContainer Root { get; } = new(string.Empty);

    public StorageBrowseContainer(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length > MaximumKeyLength)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.InvalidRequest,
                $"The storage browse container key exceeds {MaximumKeyLength} characters."));
        }

        Key = key;
    }

    public string Key { get; }

    public bool IsRoot => Key.Length == 0;
}

public sealed record StorageBrowseEntryId
{
    public const int MaximumValueLength = 4096;

    public StorageBrowseEntryId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.InvalidRequest,
                "A storage browse entry identifier is required."));
        }

        if (value.Length > MaximumValueLength)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.InvalidRequest,
                $"The storage browse entry identifier exceeds {MaximumValueLength} characters."));
        }

        Value = value;
    }

    public string Value { get; }
}

public sealed record StorageBrowseCursor
{
    public const int MaximumTokenLength = 2048;

    public StorageBrowseCursor(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > MaximumTokenLength)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.InvalidCursor,
                "The storage browse cursor is malformed."));
        }

        Token = token;
    }

    public string Token { get; }
}

public sealed record StorageBrowseConsistencyToken
{
    public const int MaximumValueLength = 512;

    public StorageBrowseConsistencyToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumValueLength)
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.InvalidRequest,
                "The storage browse consistency token is malformed."));
        }

        Value = value;
    }

    public string Value { get; }
}

public sealed record StorageBrowseSort
{
    public static StorageBrowseSort ProviderOrder { get; } = new(
        StorageBrowseSortField.ProviderNative,
        StorageBrowseSortDirection.Ascending);

    public StorageBrowseSort(StorageBrowseSortField field, StorageBrowseSortDirection direction)
    {
        if (!Enum.IsDefined(field) || !Enum.IsDefined(direction))
        {
            throw new StorageBrowseException(new StorageBrowseError(
                StorageBrowseErrorCode.InvalidRequest,
                "The requested storage browse ordering is invalid."));
        }

        Field = field;
        Direction = direction;
    }

    public StorageBrowseSortField Field { get; }

    public StorageBrowseSortDirection Direction { get; }
}
