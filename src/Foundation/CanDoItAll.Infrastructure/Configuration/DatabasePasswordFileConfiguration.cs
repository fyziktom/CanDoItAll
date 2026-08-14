using System.Text;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace CanDoItAll.Infrastructure.Configuration;

public static class DatabasePasswordFileConfiguration
{
    private const int MaximumPasswordFileBytes = 4096;
    private const string ConnectionStringKey = "Database:ConnectionString";
    private const string PasswordFileKey = "Database:PasswordFile";

    public static void Apply(IConfiguration configuration, string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        var configuredPasswordFile = configuration[PasswordFileKey];
        if (string.IsNullOrWhiteSpace(configuredPasswordFile))
        {
            return;
        }

        var configuredConnectionString = configuration[ConnectionStringKey];
        if (string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            throw new InvalidOperationException(
                $"{ConnectionStringKey} is required when {PasswordFileKey} is configured.");
        }

        var passwordFilePath = Path.IsPathRooted(configuredPasswordFile)
            ? Path.GetFullPath(configuredPasswordFile)
            : Path.GetFullPath(configuredPasswordFile, contentRootPath);
        if (!File.Exists(passwordFilePath))
        {
            throw new FileNotFoundException(
                "The configured database password file was not found.");
        }

        var fileInfo = new FileInfo(passwordFilePath);
        if ((fileInfo.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.Device)) != 0 ||
            fileInfo.LinkTarget is not null)
        {
            throw new InvalidOperationException(
                "The configured database password path must identify a regular file and cannot be a link.");
        }

        if (fileInfo.Length > MaximumPasswordFileBytes)
        {
            throw new InvalidOperationException(
                $"The configured database password file exceeds the {MaximumPasswordFileBytes}-byte limit.");
        }

        byte[] readBuffer = new byte[MaximumPasswordFileBytes + 1];
        int passwordByteCount = 0;
        using (var stream = new FileStream(
                   passwordFilePath,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.Read,
                   bufferSize: MaximumPasswordFileBytes,
                   FileOptions.SequentialScan))
        {
            if (!stream.CanSeek || stream.Length > MaximumPasswordFileBytes)
            {
                throw new InvalidOperationException(
                    $"The configured database password file exceeds the {MaximumPasswordFileBytes}-byte limit or is not a regular file.");
            }

            int bytesRead;
            while (passwordByteCount < readBuffer.Length &&
                   (bytesRead = stream.Read(
                       readBuffer,
                       passwordByteCount,
                       readBuffer.Length - passwordByteCount)) > 0)
            {
                passwordByteCount += bytesRead;
            }
        }

        if (passwordByteCount > MaximumPasswordFileBytes)
        {
            throw new InvalidOperationException(
                $"The configured database password file exceeds the {MaximumPasswordFileBytes}-byte limit.");
        }

        ReadOnlySpan<byte> passwordBytes = readBuffer.AsSpan(0, passwordByteCount);

        string password;
        try
        {
            password = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(passwordBytes)
                .TrimEnd('\r', '\n');
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidOperationException(
                "The configured database password file must contain valid UTF-8 text.",
                exception);
        }

        if (password.Length == 0)
        {
            throw new InvalidOperationException(
                "The configured database password file is empty.");
        }

        if (password.Contains('\0'))
        {
            throw new InvalidOperationException(
                "The configured database password file contains an invalid NUL character.");
        }

        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(configuredConnectionString)
        {
            Password = password
        };
        configuration[ConnectionStringKey] = connectionStringBuilder.ConnectionString;
    }
}
