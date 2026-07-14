using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace CanDoItAll.FileTools.Integration;

public sealed class FileAccessHandleOptions
{
    public const string SectionName = "FileTools:AccessHandles";

    public int MaximumEntries { get; set; } = 4096;

    public TimeSpan Lifetime { get; set; } = TimeSpan.FromMinutes(5);

    public long MaximumContentBytes { get; set; } = 64L * 1024 * 1024;

    internal static void Validate(FileAccessHandleOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.MaximumEntries is < 16 or > 65_536)
        {
            throw new InvalidOperationException("File access handle capacity must be between 16 and 65536.");
        }

        if (options.Lifetime < TimeSpan.FromSeconds(10) || options.Lifetime > TimeSpan.FromHours(1))
        {
            throw new InvalidOperationException("File access handle lifetime must be between 10 seconds and one hour.");
        }

        if (options.MaximumContentBytes is < 1024 or > 256L * 1024 * 1024)
        {
            throw new InvalidOperationException("File interaction content limit must be between 1 KiB and 256 MiB.");
        }
    }
}

internal readonly record struct FileAccessHandleId
{
    private const int EncodedLength = 43;

    public FileAccessHandleId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != EncodedLength || value.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new FileAccessDeniedException(FileAccessFailureCode.InvalidHandle, "The file access handle is invalid.");
        }

        Value = value;
    }

    public string Value { get; }

    public static FileAccessHandleId Create()
    {
        string value = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return new FileAccessHandleId(value);
    }

    public override string ToString() => Value ?? string.Empty;
}

internal sealed record FileAccessHandleGrant(
    FileAccessHandleId Id,
    FileAccessGrantRequest Request,
    StorageObjectReference Reference,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    long RevocationGeneration,
    bool IsRevoked = false);

internal interface IFileAccessHandleRegistry
{
    FileAccessHandleGrant Issue(FileAccessGrantRequest request, StorageObjectReference reference);

    FileAccessHandleGrant Resolve(
        FileAccessHandleId id,
        FileAccessContext context,
        FileAccessOperation operation);

    void Revoke(FileAccessHandleId id);

    void RevokeAll();
}

internal sealed class FileAccessHandleRegistry : IFileAccessHandleRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<FileAccessHandleId, FileAccessHandleGrant> _grants = [];
    private readonly FileAccessHandleOptions _options;
    private readonly TimeProvider _timeProvider;
    private long _revocationGeneration;

    public FileAccessHandleRegistry(IOptions<FileAccessHandleOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        FileAccessHandleOptions.Validate(_options);
        _timeProvider = timeProvider;
    }

    public FileAccessHandleGrant Issue(FileAccessGrantRequest request, StorageObjectReference reference)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(reference);
        lock (_gate)
        {
            DateTimeOffset now = _timeProvider.GetUtcNow();
            RemoveExpiredAndRevoked(now);
            while (_grants.Count >= _options.MaximumEntries)
            {
                _grants.Remove(FindOldestId());
            }

            FileAccessHandleId id;
            do
            {
                id = FileAccessHandleId.Create();
            }
            while (_grants.ContainsKey(id));

            var grant = new FileAccessHandleGrant(
                id,
                request,
                reference,
                now,
                now + _options.Lifetime,
                _revocationGeneration);
            _grants.Add(id, grant);
            return grant;
        }
    }

    private FileAccessHandleId FindOldestId()
    {
        FileAccessHandleGrant? oldest = null;
        foreach (FileAccessHandleGrant candidate in _grants.Values)
        {
            if (oldest is null ||
                candidate.IssuedAtUtc < oldest.IssuedAtUtc ||
                candidate.IssuedAtUtc == oldest.IssuedAtUtc &&
                string.CompareOrdinal(candidate.Id.Value, oldest.Id.Value) < 0)
            {
                oldest = candidate;
            }
        }

        return oldest?.Id ?? throw new InvalidOperationException("The file access handle registry is empty.");
    }

    public FileAccessHandleGrant Resolve(
        FileAccessHandleId id,
        FileAccessContext context,
        FileAccessOperation operation)
    {
        if (operation is not FileAccessOperation.View and
            not FileAccessOperation.Download and
            not FileAccessOperation.Edit and
            not FileAccessOperation.Overwrite and
            not FileAccessOperation.OpenLocally)
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        lock (_gate)
        {
            if (!_grants.TryGetValue(id, out FileAccessHandleGrant? grant))
            {
                throw Denied(FileAccessFailureCode.InvalidHandle);
            }

            if (grant.IsRevoked || grant.RevocationGeneration != _revocationGeneration)
            {
                throw Denied(FileAccessFailureCode.Revoked);
            }

            if (_timeProvider.GetUtcNow() >= grant.ExpiresAtUtc)
            {
                _grants.Remove(id);
                throw Denied(FileAccessFailureCode.Expired);
            }

            FileAccessContext grantedContext = grant.Request.Context;
            if (grantedContext.ActorId != context.ActorId ||
                grantedContext.SessionId != context.SessionId ||
                grantedContext.RuntimeProfileId != context.RuntimeProfileId ||
                grantedContext.RuntimeGeneration != context.RuntimeGeneration ||
                grantedContext.AuthorizationRevision != context.AuthorizationRevision)
            {
                throw Denied(FileAccessFailureCode.ContextMismatch);
            }

            if (!grant.Request.Operations.HasFlag(operation))
            {
                throw Denied(FileAccessFailureCode.OperationDenied);
            }

            return grant;
        }
    }

    public void Revoke(FileAccessHandleId id)
    {
        lock (_gate)
        {
            if (_grants.TryGetValue(id, out FileAccessHandleGrant? grant))
            {
                _grants[id] = grant with { IsRevoked = true };
            }
        }
    }

    public void RevokeAll()
    {
        lock (_gate)
        {
            _revocationGeneration++;
            _grants.Clear();
        }
    }

    private void RemoveExpiredAndRevoked(DateTimeOffset now)
    {
        FileAccessHandleId[] stale = _grants.Values
            .Where(grant => grant.IsRevoked ||
                            grant.RevocationGeneration != _revocationGeneration ||
                            now >= grant.ExpiresAtUtc)
            .Select(grant => grant.Id)
            .ToArray();
        foreach (FileAccessHandleId id in stale)
        {
            _grants.Remove(id);
        }
    }

    private static FileAccessDeniedException Denied(FileAccessFailureCode code)
        => new(code, "The file access handle is no longer authorized for this operation.");
}

public static class AuthorizedFileReference
{
    public const string SourceId = "candoitall-authorized-file";

    internal static FileReference Create(FileAccessHandleId id, string? revision)
        => new(SourceId, id.Value, revision);

    internal static FileAccessHandleId Parse(FileReference file)
    {
        if (!string.Equals(file.SourceId, SourceId, StringComparison.Ordinal))
        {
            throw new FileAccessDeniedException(FileAccessFailureCode.InvalidHandle, "The file access handle is invalid.");
        }

        return new FileAccessHandleId(file.Value);
    }
}
