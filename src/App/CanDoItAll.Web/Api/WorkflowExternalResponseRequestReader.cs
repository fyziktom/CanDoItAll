using System.Buffers;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Web.Api;

internal enum WorkflowExternalResponseRequestReadFailure
{
    UnsupportedContentType,
    EmptyBody,
    BodyTooLarge,
    InvalidJson,
    DuplicateProperty,
    InvalidContract
}

internal sealed record WorkflowExternalResponseRequestReadResult(
    WorkflowExternalResponseApiRequest? Request,
    WorkflowExternalResponseRequestReadFailure? Failure,
    string SafeMessage)
{
    public bool Succeeded => Request is not null && Failure is null;
}

internal static class WorkflowExternalResponseRequestReader
{
    public const int MaximumBodyBytes = 70 * 1024;

    private const int MaximumJsonDepth = 32;

    public static async ValueTask<WorkflowExternalResponseRequestReadResult> ReadAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.HasJsonContentType())
        {
            return Failure(
                WorkflowExternalResponseRequestReadFailure.UnsupportedContentType,
                "A JSON request body is required.");
        }

        if (request.ContentLength > MaximumBodyBytes)
        {
            return Failure(
                WorkflowExternalResponseRequestReadFailure.BodyTooLarge,
                "The workflow external response request body is too large.");
        }

        byte[] body;
        try
        {
            body = await ReadBoundedBodyAsync(request.Body, cancellationToken).ConfigureAwait(false);
        }
        catch (WorkflowExternalResponseBodyTooLargeException)
        {
            return Failure(
                WorkflowExternalResponseRequestReadFailure.BodyTooLarge,
                "The workflow external response request body is too large.");
        }
        catch (Exception exception) when (exception is IOException or BadHttpRequestException)
        {
            return Failure(
                WorkflowExternalResponseRequestReadFailure.InvalidJson,
                "The workflow external response request body could not be read.");
        }

        if (body.Length == 0)
        {
            return Failure(
                WorkflowExternalResponseRequestReadFailure.EmptyBody,
                "A JSON request body is required.");
        }

        try
        {
            if (ContainsDuplicateProperties(body))
            {
                return Failure(
                    WorkflowExternalResponseRequestReadFailure.DuplicateProperty,
                    "The JSON request body contains duplicate properties.");
            }

            using var document = JsonDocument.Parse(
                body,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = MaximumJsonDepth
                });
            if (!TryMap(document.RootElement, out var mapped))
            {
                return Failure(
                    WorkflowExternalResponseRequestReadFailure.InvalidContract,
                    "The JSON request body must contain only a positive expectedRequestVersion and a response value.");
            }

            return new WorkflowExternalResponseRequestReadResult(
                mapped,
                Failure: null,
                string.Empty);
        }
        catch (JsonException)
        {
            return Failure(
                WorkflowExternalResponseRequestReadFailure.InvalidJson,
                "The JSON request body is invalid or exceeds the maximum nesting depth.");
        }
    }

    private static async Task<byte[]> ReadBoundedBodyAsync(
        Stream body,
        CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        var buffer = ArrayPool<byte>.Shared.Rent(8 * 1024);
        try
        {
            while (true)
            {
                var read = await body.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    return output.ToArray();
                }

                if (output.Length + read > MaximumBodyBytes)
                {
                    throw new WorkflowExternalResponseBodyTooLargeException();
                }

                output.Write(buffer, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static bool ContainsDuplicateProperties(ReadOnlySpan<byte> body)
    {
        var reader = new Utf8JsonReader(
            body,
            new JsonReaderOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = MaximumJsonDepth
            });
        var objects = new Stack<HashSet<string>?>();
        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    objects.Push(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                    break;
                case JsonTokenType.StartArray:
                    objects.Push(null);
                    break;
                case JsonTokenType.EndObject:
                case JsonTokenType.EndArray:
                    objects.Pop();
                    break;
                case JsonTokenType.PropertyName:
                    if (objects.Peek() is { } properties &&
                        !properties.Add(reader.GetString()!))
                    {
                        return true;
                    }

                    break;
            }
        }

        return false;
    }

    private static bool TryMap(
        JsonElement root,
        out WorkflowExternalResponseApiRequest? request)
    {
        request = null;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        JsonElement? response = null;
        long? expectedRequestVersion = null;
        foreach (var property in root.EnumerateObject())
        {
            switch (property.Name)
            {
                case "expectedRequestVersion" when
                    property.Value.ValueKind == JsonValueKind.Number &&
                    property.Value.TryGetInt64(out var parsedVersion) &&
                    parsedVersion > 0:
                    expectedRequestVersion = parsedVersion;
                    break;
                case "response":
                    response = property.Value.Clone();
                    break;
                default:
                    return false;
            }
        }

        if (!expectedRequestVersion.HasValue || !response.HasValue)
        {
            return false;
        }

        request = new WorkflowExternalResponseApiRequest(
            expectedRequestVersion.Value,
            response.Value);
        return true;
    }

    private static WorkflowExternalResponseRequestReadResult Failure(
        WorkflowExternalResponseRequestReadFailure failure,
        string message)
        => new(Request: null, failure, message);

    private sealed class WorkflowExternalResponseBodyTooLargeException : Exception;
}
