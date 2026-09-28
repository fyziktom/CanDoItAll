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
                "200" or "204" => documentation.SuccessDescription,
                "400" => "The request is malformed or violates the documented field and capability limits; no requested mutation was accepted.",
                "401" => "Credentials are missing, invalid, expired or no longer live; no authenticated authority was established.",
                "403" => "The authenticated credential lacks the required capability or credential kind; no requested mutation was accepted.",
                "404" => "The addressed account, template or definition does not exist in the current catalog.",
                "409" => "A version or uniqueness conflict prevented the requested mutation. Read current state before retrying.",
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
