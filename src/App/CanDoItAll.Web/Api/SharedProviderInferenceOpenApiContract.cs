using System.Net.Mime;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi;

namespace CanDoItAll.Web.Api;

internal sealed class SharedProviderInferenceOpenApiContract
{
    private const string PrivateInferenceCachePattern = "^private, no-store, no-cache$";

    private SharedProviderInferenceOpenApiContract(SharedProviderRelayOperation operation)
    {
        Operation = operation;
    }

    public SharedProviderRelayOperation Operation { get; }

    public static SharedProviderInferenceOpenApiContract For(
        SharedProviderRelayOperation operation) => operation switch
    {
        SharedProviderRelayOperation.Responses => Responses,
        SharedProviderRelayOperation.ChatCompletions => ChatCompletions,
        SharedProviderRelayOperation.ImageGenerations => ImageGenerations,
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    public static Task TransformOperationAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var contract = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<SharedProviderInferenceOpenApiContract>()
            .SingleOrDefault();
        if (contract is null)
        {
            return Task.CompletedTask;
        }

        var jsonSchema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Description = RequestDescription(contract.Operation)
        };
        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Description =
                $"Bounded JSON body with at most {SharedProviderInferenceApi.MaximumRequestBodyBytes} UTF-8 bytes.",
            Content = new Dictionary<string, OpenApiMediaType>(StringComparer.OrdinalIgnoreCase)
            {
                [MediaTypeNames.Application.Json] = new OpenApiMediaType
                {
                    Schema = jsonSchema
                }
            }
        };

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = SharedProviderHeaders.AccessContextReference,
            In = ParameterLocation.Header,
            Description =
                "Optional opaque audit correlation reference; it is never sent to the upstream provider and does not authorize the request.",
            Required = false,
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                MinLength = 1,
                MaxLength = AccessContextReference.MaximumLength,
                Pattern = SharedProviderCatalogOpenApiContract.AccessContextPattern
            }
        });
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = SharedProviderHeaders.AccessContextReferenceType,
            In = ParameterLocation.Header,
            Description =
                "Optional canonical type for the audit correlation reference, for example project or erp.company-project. Requires the reference header and is never sent to the upstream provider.",
            Required = false,
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                MinLength = 1,
                MaxLength = AccessContextReferenceType.MaximumLength,
                Pattern = SharedProviderCatalogOpenApiContract.AccessContextTypePattern
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
            response.Headers[HeaderNames.CacheControl] = CreateStringHeader(
                "Private no-store response caching policy.",
                required: true,
                pattern: PrivateInferenceCachePattern);
            response.Headers[SharedProviderHeaders.RequestId] = CreateStringHeader(
                "Server-generated request correlation identifier.",
                required: true,
                minimumLength: 1);
            response.Headers[HeaderNames.XContentTypeOptions] = CreateStringHeader(
                "Response MIME-sniffing protection.",
                required: true,
                pattern: "^nosniff$");
            if (responseEntry.Key == StatusCodes.Status429TooManyRequests.ToString())
            {
                response.Headers[HeaderNames.RetryAfter] = CreateStringHeader(
                    "Optional bounded retry delay in seconds.",
                    required: false,
                    pattern: "^[0-9]{1,5}$");
            }
        }

        return Task.CompletedTask;
    }

    private static string RequestDescription(
        SharedProviderRelayOperation operation) => operation switch
    {
        SharedProviderRelayOperation.ChatCompletions =>
            "OpenAI-compatible chat-completions request. Supported top-level fields: model, messages, stream, tools, tool_choice, parallel_tool_calls, response_format, temperature, top_p, stop, max_tokens, and max_completion_tokens.",
        SharedProviderRelayOperation.Responses =>
            "OpenAI-compatible Responses request. Supported top-level fields: model, input, instructions, stream, tools, tool_choice, text, temperature, top_p, and max_output_tokens.",
        SharedProviderRelayOperation.ImageGenerations =>
            "OpenAI-compatible image-generation request. Supported top-level fields: model, prompt, n, size, quality, response_format, and output_format. Only base64 image output is supported.",
        _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };

    private static OpenApiHeader CreateStringHeader(
        string description,
        bool required,
        int? minimumLength = null,
        string? pattern = null) => new()
    {
        Description = description,
        Required = required,
        Schema = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            MinLength = minimumLength,
            Pattern = pattern
        }
    };

    private static SharedProviderInferenceOpenApiContract Responses { get; } =
        new(SharedProviderRelayOperation.Responses);

    private static SharedProviderInferenceOpenApiContract ChatCompletions { get; } =
        new(SharedProviderRelayOperation.ChatCompletions);

    private static SharedProviderInferenceOpenApiContract ImageGenerations { get; } =
        new(SharedProviderRelayOperation.ImageGenerations);
}
