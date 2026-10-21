using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Web.Api;

internal static class ProcessPlanningReviewRequestReader {
    internal const int MaximumUtf8Bytes = 24 * 1024;

    internal static async Task<T> ReadAsync<T>(HttpRequest request, JsonSerializerOptions options, CancellationToken cancellationToken)
        where T : class {
        if (!request.HasJsonContentType()) {
            throw new BadHttpRequestException("The review requires JSON content.", StatusCodes.Status415UnsupportedMediaType);
        }
        if (request.ContentLength > MaximumUtf8Bytes) {
            throw new BadHttpRequestException("The review request exceeds its size limit.", StatusCodes.Status413PayloadTooLarge);
        }
        var bytes = new byte[MaximumUtf8Bytes + 1];
        var length = await request.Body.ReadAtLeastAsync(bytes, bytes.Length, throwOnEndOfStream: false, cancellationToken);
        if (length > MaximumUtf8Bytes) {
            throw new BadHttpRequestException("The review request exceeds its size limit.", StatusCodes.Status413PayloadTooLarge);
        }
        using var document = JsonDocument.Parse(bytes.AsMemory(0, length), new() { MaxDepth = 16 });
        AgentApiRequestValidation.RequireUniqueMembers(document.RootElement);
        var json = new JsonSerializerOptions(options) {
            PropertyNameCaseInsensitive = false, MaxDepth = 16, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true
        };
        return document.Deserialize<T>(json) ?? throw new JsonException("A review request is required.");
    }
}
