using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Modules.Memory.Services;

public readonly record struct MemoryProviderRevision(string Value) {
    public static MemoryProviderRevision Capture(MemoryProviderManagementProfile profile) =>
        new(Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(profile))));
}
