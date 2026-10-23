using System.Buffers.Binary;

namespace CanDoItAll.Processes.Persistence;

internal static class ProcessRootAdvisoryKey {
    internal static long From(Guid rootRunId) {
        Span<byte> bytes = stackalloc byte[16];
        rootRunId.TryWriteBytes(bytes);
        return BinaryPrimitives.ReadInt64LittleEndian(bytes[..8]) ^ BinaryPrimitives.ReadInt64LittleEndian(bytes[8..]);
    }
}
