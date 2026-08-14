using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.SharedKernel;

public sealed record PortablePhysicalFileName(string PhysicalName, string DisplayName);

public static class PortablePhysicalFileNamePolicy
{
    public const int DefaultMaximumUtf8Bytes = 180;

    private static readonly SearchValues<char> ForbiddenCharacters = SearchValues.Create(
        "<>:\"/\\|?*");
    private static readonly HashSet<string> ReservedDeviceNames = CreateReservedDeviceNames();

    public static PortablePhysicalFileName Encode(
        string? displayName,
        int maximumUtf8Bytes = DefaultMaximumUtf8Bytes)
    {
        PortablePhysicalFileNameEncoding encoding = EncodeCore(displayName, maximumUtf8Bytes);
        return new PortablePhysicalFileName(encoding.PhysicalName, encoding.DisplayName);
    }

    public static PortablePhysicalFileName Allocate(
        string? displayName,
        IEnumerable<string> existingPhysicalNames,
        StringComparer? physicalNameComparer = null,
        int maximumUtf8Bytes = DefaultMaximumUtf8Bytes)
    {
        ArgumentNullException.ThrowIfNull(existingPhysicalNames);
        PortablePhysicalFileNameEncoding encoding = EncodeCore(displayName, maximumUtf8Bytes);
        physicalNameComparer ??= StringComparer.Ordinal;
        string[] existing = existingPhysicalNames.ToArray();
        if (!IsOccupied(encoding.PhysicalName, existing, physicalNameComparer))
        {
            return new PortablePhysicalFileName(encoding.PhysicalName, encoding.DisplayName);
        }

        int firstCollisionSequence = encoding.HasIdentitySuffix ? 2 : 1;
        for (int sequence = firstCollisionSequence; sequence <= existing.Length + 1; sequence++)
        {
            string collisionSuffix = sequence == 1
                ? encoding.HashSuffix
                : encoding.HashSuffix + "-" + sequence.ToString(CultureInfo.InvariantCulture);
            string allocatedCandidate = EnsureSuffix(
                encoding.CollisionStem,
                collisionSuffix,
                maximumUtf8Bytes);
            if (!IsOccupied(allocatedCandidate, existing, physicalNameComparer))
            {
                return new PortablePhysicalFileName(allocatedCandidate, encoding.DisplayName);
            }
        }

        throw new InvalidOperationException("A unique portable physical filename could not be allocated.");
    }

    public static bool IsPortable(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return string.Equals(Encode(value).PhysicalName, value, StringComparison.Ordinal);
    }

    private static PortablePhysicalFileNameEncoding EncodeCore(
        string? displayName,
        int maximumUtf8Bytes)
    {
        if (maximumUtf8Bytes < 32)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumUtf8Bytes),
                "The portable filename budget must be at least 32 UTF-8 bytes.");
        }

        string original = string.IsNullOrWhiteSpace(displayName)
            ? "artifact.bin"
            : displayName;
        string normalized = original.Normalize(NormalizationForm.FormC);
        var builder = new StringBuilder(normalized.Length);
        foreach (char character in normalized)
        {
            builder.Append(character < ' ' || character == '' || ForbiddenCharacters.Contains(character)
                ? '-'
                : character);
        }

        string candidate = builder.ToString().TrimEnd(' ', '.');
        if (candidate is "" or "." or "..")
        {
            candidate = "artifact";
        }

        bool changed = !string.Equals(candidate, original, StringComparison.Ordinal) ||
                       !string.Equals(normalized, original, StringComparison.Ordinal);
        string baseName = candidate.Split('.', 2)[0];
        if (ReservedDeviceNames.Contains(baseName))
        {
            candidate = "_" + candidate;
            changed = true;
        }

        string hashSuffix = "~" + CreateHashSuffix(original);
        if (Encoding.UTF8.GetByteCount(candidate) > maximumUtf8Bytes)
        {
            candidate = TruncateUtf8(candidate, maximumUtf8Bytes - Encoding.UTF8.GetByteCount(hashSuffix));
            changed = true;
        }

        string candidateStem = candidate;
        if (changed)
        {
            candidate = EnsureSuffix(candidate, hashSuffix, maximumUtf8Bytes);
        }

        return new PortablePhysicalFileNameEncoding(
            candidate,
            original,
            candidateStem,
            hashSuffix,
            changed);
    }

    private static string EnsureSuffix(string candidate, string suffix, int maximumUtf8Bytes)
    {
        if (candidate.EndsWith(suffix, StringComparison.Ordinal))
        {
            return candidate;
        }

        int prefixBudget = maximumUtf8Bytes - Encoding.UTF8.GetByteCount(suffix);
        string prefix = TruncateUtf8(candidate, prefixBudget).TrimEnd(' ', '.');
        if (prefix.Length == 0)
        {
            prefix = "artifact";
        }

        return prefix + suffix;
    }

    private static bool IsOccupied(
        string candidate,
        IReadOnlyList<string> existingPhysicalNames,
        StringComparer physicalNameComparer)
        => existingPhysicalNames.Any(name => physicalNameComparer.Equals(name, candidate));

    private static string TruncateUtf8(string value, int maximumUtf8Bytes)
    {
        var builder = new StringBuilder(value.Length);
        var byteCount = 0;
        foreach (Rune rune in value.EnumerateRunes())
        {
            int runeBytes = rune.Utf8SequenceLength;
            if (byteCount + runeBytes > maximumUtf8Bytes)
            {
                break;
            }

            builder.Append(rune);
            byteCount += runeBytes;
        }

        return builder.ToString();
    }

    private static string CreateHashSuffix(string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(hash.AsSpan(0, 6));
    }

    private static HashSet<string> CreateReservedDeviceNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON",
            "PRN",
            "AUX",
            "NUL"
        };
        for (var index = 1; index <= 9; index++)
        {
            names.Add($"COM{index}");
            names.Add($"LPT{index}");
        }

        return names;
    }

    private readonly record struct PortablePhysicalFileNameEncoding(
        string PhysicalName,
        string DisplayName,
        string CollisionStem,
        string HashSuffix,
        bool HasIdentitySuffix);
}
