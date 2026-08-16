using System.Text.Json;

namespace CanDoItAll.Web.Api;

internal static class LlmChatApiRequestReader
{
    public static async ValueTask<(T? Value, IResult? Error)> ReadAsync<T>(
        HttpRequest request,
        CancellationToken cancellationToken)
        where T : class
    {
        if (!request.HasJsonContentType())
        {
            return (null, LlmChatApiResults.InvalidRequest("A JSON request body is required."));
        }

        try
        {
            var value = await request.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
            return value is null
                ? (null, LlmChatApiResults.InvalidRequest("A JSON request body is required."))
                : (value, null);
        }
        catch (JsonException)
        {
            return (null, LlmChatApiResults.InvalidRequest("The JSON request body is invalid."));
        }
        catch (NotSupportedException)
        {
            return (null, LlmChatApiResults.InvalidRequest("The JSON request body is invalid."));
        }
        catch (BadHttpRequestException)
        {
            return (null, LlmChatApiResults.InvalidRequest("The JSON request body is invalid."));
        }
    }
}
