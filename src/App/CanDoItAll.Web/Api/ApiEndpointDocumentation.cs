using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CanDoItAll.Web.Api;

internal sealed record ApiEndpointDocumentation(string SuccessDescription, string? RequestDescription) {
    public static Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken) {
        var documentation = context.Description.ActionDescriptor.EndpointMetadata.OfType<ApiEndpointDocumentation>().LastOrDefault();
        if (documentation is null) {
            return Task.CompletedTask;
        }
        if (operation.RequestBody is { } body) {
            body.Description = documentation.RequestDescription;
        }
        foreach (var (status, response) in operation.Responses ?? []) {
            response.Description = status switch {
                "200" or "201" or "204" => documentation.SuccessDescription,
                "202" => "The original request was admitted for asynchronous work. Inspect the returned progress; acceptance does not establish completion.",
                "400" => "The request is malformed or violates the documented field and capability limits; no requested mutation was accepted.",
                "401" => "Credentials are missing, invalid, expired or no longer live; no authenticated authority was established.",
                "403" => "The authenticated credential lacks the required capability or credential kind; no requested mutation was accepted.",
                "404" => "The addressed resource or retained receipt was not observed. A missing receipt does not establish rollback of an earlier uncertain request.",
                "409" => "The original expectation, authority or requested transition conflicts with observed state. Inspect the typed outcome and retain any unresolved original intent before choosing another action.",
                "410" => "The original operation epoch or retained evidence has expired. Expiry does not authorize replacing an unresolved operation.",
                "412" => "The supplied version or workspace precondition does not match the addressed resource; no conditional replacement was accepted.",
                "413" => "The request exceeds the documented body limit; no requested mutation was accepted.",
                "415" => "The request requires the documented JSON media type; no requested mutation was accepted.",
                "428" => "The original conditional expectation or lifecycle submission is required. Capture it before creating a new intent; this legacy request performs no mutation.",
                "429" => "Login attempt or password-verification capacity is exhausted temporarily. Retry after the Retry-After interval.",
                "503" => "The required persistence or runtime service is unavailable; inspect the safe error code before retrying.",
                _ => response.Description
            };
        }
        return Task.CompletedTask;
    }
}

internal static class ApiEndpointDocumentationExtensions {
    public static RouteHandlerBuilder DescribeApi(this RouteHandlerBuilder endpoint, string summary, string description,
        string successDescription, string? requestDescription = null) => endpoint.WithSummary(summary).WithDescription(description)
        .WithMetadata(new ApiEndpointDocumentation(successDescription, requestDescription));
}
