using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Web.Api;

internal static class ProjectDefinitionHttpBody {
    internal const int MaximumDepth = 16;
    private const int BufferSize = 16 * 1024;
    private static readonly ProjectDefinitionLimits Limits = ProjectDefinitionLimits.VersionOneMaximum;

    internal static async Task<ProjectDefinitionRequest<T>> ReadAsync<T>(HttpRequest request, JsonSerializerOptions configured,
        CancellationToken cancellationToken) where T : class {
        if (!request.HasJsonContentType()) {
            throw new BadHttpRequestException("A protected definition request requires JSON.", StatusCodes.Status415UnsupportedMediaType);
        }
        if (request.ContentLength > Limits.MaxRawRequestBytes) {
            throw TooLarge();
        }
        using var content = new MemoryStream();
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try {
            while (true) {
                var remaining = Limits.MaxRawRequestBytes - checked((int)content.Length);
                var read = await request.Body.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining + 1)), cancellationToken);
                if (read == 0) {
                    break;
                }
                if (read > remaining) {
                    throw TooLarge();
                }
                content.Write(buffer, 0, read);
            }
        } finally {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
        var bytes = content.GetBuffer().AsSpan(0, checked((int)content.Length));
        ValidateTokens(bytes);
        var json = new JsonSerializerOptions(configured) {
            MaxDepth = MaximumDepth,
            AllowTrailingCommas = false,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        return JsonSerializer.Deserialize<ProjectDefinitionRequest<T>>(bytes, json)
            ?? throw new JsonException("A definition request must be an object.");
    }

    internal static void ValidateTokens(ReadOnlySpan<byte> bytes) {
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = MaximumDepth });
        var containers = new Stack<TokenContainer>();
        while (reader.Read()) {
            if (reader.TokenType == JsonTokenType.PropertyName) {
                var property = reader.GetString()!;
                if (property.Length > 128 || containers.Count == 0 || containers.Peek().Properties is not { } names || !names.Add(property)) {
                    throw new JsonException("Definition objects require bounded, unique property names.");
                }
                continue;
            }
            if (reader.TokenType is JsonTokenType.EndArray or JsonTokenType.EndObject) {
                containers.Pop();
                continue;
            }
            if (containers.TryPeek(out var parent) && parent.Properties is null && ++parent.Count > Math.Max(Limits.MaxPhaseCount, Limits.MaxOptionCount)) {
                throw TooLarge();
            }
            if (reader.TokenType is JsonTokenType.StartArray or JsonTokenType.StartObject) {
                containers.Push(new(reader.TokenType == JsonTokenType.StartObject));
            } else if (reader.TokenType == JsonTokenType.String && Encoding.UTF8.GetByteCount(reader.GetString()!) > Limits.MaxFieldUtf8Bytes) {
                throw TooLarge();
            }
        }
    }

    private static BadHttpRequestException TooLarge()
        => new("The definition request exceeds the supported byte, field or collection limit.", StatusCodes.Status413PayloadTooLarge);

    private sealed class TokenContainer(bool isObject) {
        internal HashSet<string>? Properties { get; } = isObject ? new(StringComparer.OrdinalIgnoreCase) : null;
        internal int Count { get; set; }
    }
}
