using System.Security.Cryptography;
using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.AgentFramework.Models;

public static class ProviderBatchHistoryContext {
    public static HistoryInvocationContext Create(Guid jobId, Guid inputId) {
        Span<byte> identity = stackalloc byte[32];
        jobId.TryWriteBytes(identity);
        inputId.TryWriteBytes(identity[16..]);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(identity, hash);
        return new(new(new Guid(hash[..16])), HistoryWorkload.Batch, new(HistoryAuthenticationKind.Unknown));
    }
}
