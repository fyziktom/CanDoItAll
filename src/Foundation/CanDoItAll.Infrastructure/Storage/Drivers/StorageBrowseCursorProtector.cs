using System.Security.Cryptography;
using System.Text.Json;

namespace CanDoItAll.Infrastructure.Storage;

internal sealed class StorageBrowseCursorProtector
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly byte[] _signingKey = RandomNumberGenerator.GetBytes(32);

    public StorageBrowseCursor Encode<TState>(TState state)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(state, SerializerOptions);
        byte[] signature = HMACSHA256.HashData(_signingKey, payload);
        return new StorageBrowseCursor($"{Base64UrlEncode(payload)}.{Base64UrlEncode(signature)}");
    }

    public TState Decode<TState>(StorageBrowseCursor cursor, string invalidMessage)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        string[] parts = cursor.Token.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            throw InvalidCursor(invalidMessage);
        }

        try
        {
            byte[] payload = Base64UrlDecode(parts[0]);
            byte[] signature = Base64UrlDecode(parts[1]);
            byte[] expected = HMACSHA256.HashData(_signingKey, payload);
            if (!CryptographicOperations.FixedTimeEquals(signature, expected))
            {
                throw InvalidCursor(invalidMessage);
            }

            return JsonSerializer.Deserialize<TState>(payload, SerializerOptions)
                ?? throw InvalidCursor(invalidMessage);
        }
        catch (StorageBrowseException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is FormatException or JsonException or NotSupportedException)
        {
            throw InvalidCursor(invalidMessage);
        }
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }

    private static StorageBrowseException InvalidCursor(string message)
        => new(new StorageBrowseError(StorageBrowseErrorCode.InvalidCursor, message));
}
