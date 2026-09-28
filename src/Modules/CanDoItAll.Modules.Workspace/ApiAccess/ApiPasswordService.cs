using System.Buffers.Binary;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Workspace.ApiAccess;

public sealed class ApiPasswordService {
    public const int IterationCount = 220000;
    private static readonly object HashSubject = new();
    private readonly PasswordHasher<object> hasher = new(Options.Create(new PasswordHasherOptions {
        CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
        IterationCount = IterationCount
    }));

    public string Hash(string password) {
        ApiIdentityRules.ValidatePassword(password);
        return hasher.HashPassword(HashSubject, password);
    }

    public PasswordVerificationResult Verify(string hash, string password) {
        if (!IsSupportedHash(hash)) {
            throw new InvalidDataException("The API password hash format is unsupported.");
        }
        return hasher.VerifyHashedPassword(HashSubject, hash, password);
    }

    public static bool IsSupportedHash(string? hash, bool requireCurrentWorkFactor = false) {
        if (hash is null || hash.Length > 512) {
            return false;
        }
        Span<byte> bytes = stackalloc byte[384];
        if (!Convert.TryFromBase64String(hash, bytes, out var count) || count < 61 || bytes[0] != 1) {
            return false;
        }
        var prf = BinaryPrimitives.ReadUInt32BigEndian(bytes[1..5]);
        var iterations = BinaryPrimitives.ReadUInt32BigEndian(bytes[5..9]);
        var saltLength = BinaryPrimitives.ReadUInt32BigEndian(bytes[9..13]);
        return prf == (uint)KeyDerivationPrf.HMACSHA512 &&
            iterations >= (requireCurrentWorkFactor ? IterationCount : 100000) && iterations <= 1000000 &&
            saltLength is >= 16 and <= 128 && count - 13 - saltLength is >= 32 and <= 128;
    }
}
