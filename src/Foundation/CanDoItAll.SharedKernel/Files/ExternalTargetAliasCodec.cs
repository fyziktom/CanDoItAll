using System.Text;

namespace CanDoItAll.SharedKernel;

public static class ExternalTargetAliasCodec
{
    public const string AliasRoot = "external-target";
    public const string CurrentVersion = "v1";
    public const int RootIdLength = 24;

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static IEqualityComparer<string> EqualityComparer { get; } = new ExternalTargetAliasEqualityComparer();

    public static string? NormalizeVersionedAlias(string? alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return null;
        }

        return TryParseVersionedAlias(alias, out var rootId, out var segments, out _)
            ? BuildAlias(rootId, segments)
            : null;
    }

    public static string BuildAliasRoot(string rootId)
    {
        if (!IsValidRootId(rootId))
        {
            throw new ArgumentException("External-target root identity must contain 24 hexadecimal characters.", nameof(rootId));
        }

        return $"{AliasRoot}/{CurrentVersion}/{rootId.ToLowerInvariant()}";
    }

    public static string BuildAlias(string rootId, IReadOnlyList<string> physicalSegments)
    {
        ArgumentNullException.ThrowIfNull(physicalSegments);
        var root = BuildAliasRoot(rootId);
        return physicalSegments.Count == 0
            ? root
            : $"{root}/{string.Join('/', physicalSegments.Select(EncodePhysicalSegment))}";
    }

    public static bool TryParseVersionedAlias(
        string alias,
        out string rootId,
        out IReadOnlyList<string> physicalSegments,
        out string validationMessage)
    {
        rootId = string.Empty;
        physicalSegments = [];
        validationMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(alias))
        {
            return false;
        }

        var normalizedSeparators = alias.Trim().Replace('\\', '/');
        var prefix = $"{AliasRoot}/{CurrentVersion}/";
        if (!normalizedSeparators.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var suffix = normalizedSeparators[prefix.Length..];
        var encodedSegments = suffix.Split('/', StringSplitOptions.None);
        if (encodedSegments.Length == 0 || !IsValidRootId(encodedSegments[0]))
        {
            validationMessage = "The external-target alias has an invalid versioned root identity.";
            return false;
        }

        rootId = encodedSegments[0].ToLowerInvariant();
        if (encodedSegments.Skip(1).Any(string.IsNullOrEmpty))
        {
            validationMessage = "The external-target alias contains an empty physical path segment.";
            return false;
        }

        var decodedSegments = new List<string>(Math.Max(0, encodedSegments.Length - 1));
        foreach (var encodedSegment in encodedSegments.Skip(1))
        {
            if (!TryDecodePhysicalSegment(encodedSegment, out var segment))
            {
                validationMessage = "The external-target alias contains an invalid encoded physical path segment.";
                return false;
            }

            decodedSegments.Add(segment);
        }

        physicalSegments = decodedSegments;
        return true;
    }

    public static bool TryNormalizeLegacyAlias(string? alias, out string normalizedAlias)
    {
        normalizedAlias = string.Empty;
        if (string.IsNullOrWhiteSpace(alias))
        {
            return false;
        }

        var normalizedSeparators = alias.Trim().Replace('\\', '/');
        var segments = normalizedSeparators.Split('/', StringSplitOptions.None);
        if (segments.Length < 3 ||
            segments.Any(string.IsNullOrEmpty) ||
            !string.Equals(segments[0], AliasRoot, StringComparison.OrdinalIgnoreCase) ||
            segments[1].Length != 1 ||
            !char.IsAsciiLetter(segments[1][0]) ||
            segments.Skip(2).Any(IsDotPathSegment))
        {
            return false;
        }

        normalizedAlias = string.Join('/',
            AliasRoot,
            char.ToUpperInvariant(segments[1][0]).ToString(),
            string.Join('/', segments.Skip(2)));
        return true;
    }

    public static bool IsAliasWithinRoot(string alias, string rootAlias)
    {
        if (TryParseVersionedAlias(alias, out var aliasRootId, out var aliasSegments, out _) &&
            TryParseVersionedAlias(rootAlias, out var rootId, out var rootSegments, out _))
        {
            return string.Equals(aliasRootId, rootId, StringComparison.Ordinal) &&
                   rootSegments.Count <= aliasSegments.Count &&
                   rootSegments.SequenceEqual(aliasSegments.Take(rootSegments.Count), StringComparer.Ordinal);
        }

        return TryNormalizeLegacyAlias(alias, out var normalizedAlias) &&
               TryNormalizeLegacyAlias(rootAlias, out var normalizedRoot) &&
               (string.Equals(normalizedAlias, normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
                normalizedAlias.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsVersionedAlias(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return trimmed.StartsWith($"{AliasRoot}/{CurrentVersion}/", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith($"{AliasRoot}\\{CurrentVersion}\\", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAnyAlias(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return trimmed.StartsWith(AliasRoot + "/", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith(AliasRoot + "\\", StringComparison.OrdinalIgnoreCase);
    }

    private static string EncodePhysicalSegment(string segment)
    {
        if (string.IsNullOrEmpty(segment) || segment.Contains('/') || segment.Contains('\0'))
        {
            throw new ArgumentException("Physical alias segments must be non-empty and cannot contain '/' or NUL.", nameof(segment));
        }

        var bytes = StrictUtf8.GetBytes(segment);
        var builder = new StringBuilder(bytes.Length);
        var encodeDots = IsDotPathSegment(segment);
        foreach (var value in bytes)
        {
            if (!encodeDots && IsUnreserved(value))
            {
                builder.Append((char)value);
                continue;
            }

            builder.Append('%');
            builder.Append(value.ToString("X2"));
        }

        return builder.ToString();
    }

    private static bool TryDecodePhysicalSegment(string encoded, out string segment)
    {
        segment = string.Empty;
        if (string.IsNullOrEmpty(encoded))
        {
            return false;
        }

        var bytes = new List<byte>(encoded.Length);
        for (var index = 0; index < encoded.Length; index++)
        {
            var character = encoded[index];
            if (character == '%')
            {
                if (index + 2 >= encoded.Length ||
                    !byte.TryParse(encoded.AsSpan(index + 1, 2), System.Globalization.NumberStyles.HexNumber, null, out var value))
                {
                    return false;
                }

                bytes.Add(value);
                index += 2;
                continue;
            }

            if (character > 0x7F || !IsUnreserved((byte)character))
            {
                return false;
            }

            bytes.Add((byte)character);
        }

        try
        {
            segment = StrictUtf8.GetString(bytes.ToArray());
        }
        catch (DecoderFallbackException)
        {
            return false;
        }

        return segment.Length > 0 &&
               !segment.Contains('/') &&
               !segment.Contains('\0') &&
               !IsDotPathSegment(segment);
    }

    private static bool IsValidRootId(string rootId)
    {
        return rootId.Length == RootIdLength && rootId.All(Uri.IsHexDigit);
    }

    private static bool IsUnreserved(byte value)
    {
        return value is >= (byte)'A' and <= (byte)'Z' or
               >= (byte)'a' and <= (byte)'z' or
               >= (byte)'0' and <= (byte)'9' or
               (byte)'-' or (byte)'_' or (byte)'.' or (byte)'~';
    }

    private static bool IsDotPathSegment(string segment)
    {
        return segment is "." or "..";
    }

    private sealed class ExternalTargetAliasEqualityComparer : IEqualityComparer<string>
    {
        public bool Equals(string? left, string? right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left is null || right is null)
            {
                return false;
            }

            var normalizedLeft = NormalizeVersionedAlias(left);
            var normalizedRight = NormalizeVersionedAlias(right);
            if (normalizedLeft is not null || normalizedRight is not null)
            {
                return normalizedLeft is not null &&
                       normalizedRight is not null &&
                       string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal);
            }

            return TryNormalizeLegacyAlias(left, out normalizedLeft) &&
                   TryNormalizeLegacyAlias(right, out normalizedRight)
                ? string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase)
                : string.Equals(left, right, StringComparison.Ordinal);
        }

        public int GetHashCode(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            var normalizedVersionedAlias = NormalizeVersionedAlias(value);
            if (normalizedVersionedAlias is not null)
            {
                return StringComparer.Ordinal.GetHashCode(normalizedVersionedAlias);
            }

            return TryNormalizeLegacyAlias(value, out var normalizedLegacyAlias)
                ? StringComparer.OrdinalIgnoreCase.GetHashCode(normalizedLegacyAlias)
                : StringComparer.Ordinal.GetHashCode(value);
        }
    }
}
