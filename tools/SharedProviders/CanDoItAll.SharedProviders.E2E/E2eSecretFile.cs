using System.Text;

namespace CanDoItAll.SharedProviders.E2E;

internal static class E2eSecretFile
{
    private const int MaximumSecretBytes = 4096;
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static async Task<string> ReadRequiredAsync(
        string path,
        string purpose,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);

        if (path.Contains('\0'))
        {
            throw new E2eSafeException($"The {purpose} secret file path is invalid.");
        }

        string value;
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists ||
                info.Attributes.HasFlag(FileAttributes.Directory) ||
                info.Attributes.HasFlag(FileAttributes.Device) ||
                info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new E2eSafeException($"The {purpose} secret path is not a regular file.");
            }

            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var bytes = new byte[MaximumSecretBytes + 1];
            var total = 0;
            while (total < bytes.Length)
            {
                var read = await stream.ReadAsync(bytes.AsMemory(total), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                total += read;
            }

            if (total > MaximumSecretBytes)
            {
                throw new E2eSafeException($"The {purpose} secret file exceeds the size limit.");
            }

            value = StrictUtf8.GetString(bytes, 0, total);
        }
        catch (Exception exception) when (exception is
            IOException or
            UnauthorizedAccessException or
            DecoderFallbackException)
        {
            throw new E2eSafeException($"The {purpose} secret file could not be read.", exception);
        }

        value = value.TrimEnd('\r', '\n');
        if (string.IsNullOrWhiteSpace(value) || value.Contains('\0'))
        {
            throw new E2eSafeException($"The {purpose} secret file is empty.");
        }

        return value;
    }
}
