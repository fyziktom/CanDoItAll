using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi;

namespace CanDoItAll.Web.Api;

internal sealed class SharedProviderCatalogOpenApiContract
{
    internal const string AccessContextPattern = "^[A-Za-z0-9._~:-]+$";
    internal const string AccessContextTypePattern = "^[a-z0-9._-]+$";
    internal const string CatalogEntityTagPattern = "^\"sha256:[0-9a-f]{64}\"$";
    internal const string PrivateNoCachePattern = "^private, no-cache$";

    public static SharedProviderCatalogOpenApiContract Instance { get; } = new();

    private SharedProviderCatalogOpenApiContract()
    {
    }

    public static Task TransformOperationAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (!context.Description.ActionDescriptor.EndpointMetadata
                .OfType<SharedProviderCatalogOpenApiContract>()
                .Any())
        {
            return Task.CompletedTask;
        }

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = HeaderNames.IfNoneMatch,
            In = ParameterLocation.Header,
            Description = "Optional RFC 9110 entity-tag list used for conditional catalog retrieval.",
            Required = false,
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                MinLength = 1,
                MaxLength = SharedProviderCatalogApi.MaximumIfNoneMatchLength
            }
        });
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = SharedProviderHeaders.AccessContextReference,
            In = ParameterLocation.Header,
            Description = "Optional opaque access-context correlation reference; it does not authorize the request.",
            Required = false,
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                MinLength = 1,
                MaxLength = AccessContextReference.MaximumLength,
                Pattern = AccessContextPattern
            }
        });
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = SharedProviderHeaders.AccessContextReferenceType,
            In = ParameterLocation.Header,
            Description = "Optional canonical type for the access-context reference, for example project or erp.company-project. Requires the reference header.",
            Required = false,
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                MinLength = 1,
                MaxLength = AccessContextReferenceType.MaximumLength,
                Pattern = AccessContextTypePattern
            }
        });

        if (operation.Responses is not { } responses)
        {
            return Task.CompletedTask;
        }

        foreach (var responseEntry in responses)
        {
            if (responseEntry.Value is not OpenApiResponse response)
            {
                continue;
            }

            response.Headers ??= new Dictionary<string, IOpenApiHeader>(
                StringComparer.OrdinalIgnoreCase);
            response.Headers[HeaderNames.CacheControl] = CreateRequiredStringHeader(
                "Private response caching policy.",
                pattern: PrivateNoCachePattern);
            response.Headers[SharedProviderHeaders.RequestId] = CreateRequiredStringHeader(
                "Server-generated request correlation identifier.",
                minimumLength: 1);
            if (responseEntry.Key is "200" or "304")
            {
                response.Headers[HeaderNames.ETag] = CreateRequiredStringHeader(
                    "Strong entity tag for the canonical public catalog representation.",
                    pattern: CatalogEntityTagPattern);
            }
        }

        return Task.CompletedTask;
    }

    private static OpenApiHeader CreateRequiredStringHeader(
        string description,
        int? minimumLength = null,
        string? pattern = null)
        => new()
        {
            Description = description,
            Required = true,
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                MinLength = minimumLength,
                Pattern = pattern
            }
        };
}
