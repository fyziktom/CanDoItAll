using System.Text;

namespace CanDoItAll.Infrastructure.Configuration;

public static class BoundedConfigurationSecretFileReader
{
    public static string Read(
        string configuredFilePath,
        string contentRootPath,
        string secretDescription,
        int maximumBytes = 4096)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretDescription);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);

        var path = Path.IsPathRooted(configuredFilePath)
            ? Path.GetFullPath(configuredFilePath)
            : Path.GetFullPath(configuredFilePath, contentRootPath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"The configured {secretDescription} file was not found.");
        }

        var fileInfo = new FileInfo(path);
        if ((fileInfo.Attributes &
                (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.Device)) != 0 ||
            fileInfo.LinkTarget is not null)
        {
            throw new InvalidOperationException(
                $"The configured {secretDescription} path must identify a regular file and cannot be a link.");
        }

        if (fileInfo.Length > maximumBytes)
        {
            throw TooLarge(secretDescription, maximumBytes);
        }

        var buffer = new byte[maximumBytes + 1];
        var byteCount = 0;
        using (var stream = new FileStream(
                   path,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.Read,
                   bufferSize: maximumBytes,
                   FileOptions.SequentialScan))
        {
            if (!stream.CanSeek || stream.Length > maximumBytes)
            {
                throw TooLarge(secretDescription, maximumBytes);
            }

            int bytesRead;
            while (byteCount < buffer.Length &&
                   (bytesRead = stream.Read(
                       buffer,
                       byteCount,
                       buffer.Length - byteCount)) > 0)
            {
                byteCount += bytesRead;
            }
        }

        if (byteCount > maximumBytes)
        {
            throw TooLarge(secretDescription, maximumBytes);
        }

        string value;
        try
        {
            value = new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false,
                    throwOnInvalidBytes: true)
                .GetString(buffer.AsSpan(0, byteCount))
                .TrimEnd('\r', '\n');
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidOperationException(
                $"The configured {secretDescription} file must contain valid UTF-8 text.",
                exception);
        }

        if (value.Length == 0)
        {
            throw new InvalidOperationException(
                $"The configured {secretDescription} file is empty.");
        }

        if (value.Contains('\0'))
        {
            throw new InvalidOperationException(
                $"The configured {secretDescription} file contains an invalid NUL character.");
        }

        return value;
    }

    private static InvalidOperationException TooLarge(
        string secretDescription,
        int maximumBytes)
        => new(
            $"The configured {secretDescription} file exceeds the {maximumBytes}-byte limit.");
}
