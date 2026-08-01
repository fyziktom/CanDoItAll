using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal static class AgentChatContextDigest
{
    private const byte DigestSchemaVersion = 1;

    public static string Compute(AgentRuntimeTransientContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendByte(hash, DigestSchemaVersion);
        AppendString(hash, context.Content);
        AppendInt32(hash, context.Attachments.Length);

        foreach (var attachment in context.Attachments)
        {
            AppendGuid(hash, attachment.ScopeId.Value);
            AppendString(hash, attachment.Source.Kind.Value);
            AppendString(hash, attachment.Source.Id.Value);
            AppendWorkspaceScope(hash, attachment.WorkspaceScope);
            AppendString(hash, attachment.ContributorId.Value);
            AppendString(hash, attachment.Kind.Value);
            AppendInt64(hash, attachment.PublicationRevision.Value);
            AppendString(hash, attachment.ContentFingerprint.Value);
            AppendString(hash, attachment.CoverageFingerprint.Value);
            AppendInt64(hash, attachment.DatabaseProfileGeneration.Value);
            AppendString(hash, attachment.FreshnessFingerprint.Value);
            AppendDateTimeOffset(hash, attachment.CapturedAtUtc);
            AppendNullableDateTimeOffset(hash, attachment.FreshUntilUtc);
        }

        return Convert.ToHexString(hash.GetHashAndReset())
            .ToLowerInvariant();
    }

    public static string Compute(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        return Compute(new AgentRuntimeTransientContext(content));
    }

    private static void AppendWorkspaceScope(
        IncrementalHash hash,
        WorkspaceScopeDescriptor? workspaceScope)
    {
        if (workspaceScope is null)
        {
            AppendByte(hash, 0);
            return;
        }

        AppendByte(hash, 1);
        AppendInt32(hash, (int)workspaceScope.Kind);
        AppendString(hash, workspaceScope.Key);
    }

    private static void AppendNullableDateTimeOffset(
        IncrementalHash hash,
        DateTimeOffset? value)
    {
        if (!value.HasValue)
        {
            AppendByte(hash, 0);
            return;
        }

        AppendByte(hash, 1);
        AppendDateTimeOffset(hash, value.Value);
    }

    private static void AppendDateTimeOffset(
        IncrementalHash hash,
        DateTimeOffset value)
        => AppendInt64(hash, value.UtcTicks);

    private static void AppendGuid(IncrementalHash hash, Guid value)
    {
        Span<byte> bytes = stackalloc byte[16];
        value.TryWriteBytes(bytes);
        hash.AppendData(bytes);
    }

    private static void AppendString(IncrementalHash hash, string value)
    {
        var byteCount = Encoding.UTF8.GetByteCount(value);
        AppendInt32(hash, byteCount);
        if (byteCount == 0)
        {
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(value);
        hash.AppendData(bytes);
    }

    private static void AppendInt32(IncrementalHash hash, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static void AppendInt64(IncrementalHash hash, long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        hash.AppendData(bytes);
    }

    private static void AppendByte(IncrementalHash hash, byte value)
    {
        Span<byte> bytes = stackalloc byte[1];
        bytes[0] = value;
        hash.AppendData(bytes);
    }
}
