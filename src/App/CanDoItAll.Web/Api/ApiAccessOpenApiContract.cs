using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CanDoItAll.Web.Api;

internal static class ApiAccessOpenApiContract {
    public static Task TransformOperationAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken) {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (!metadata.OfType<IAuthorizeData>().Any() || metadata.OfType<IAllowAnonymous>().Any()) {
            return Task.CompletedTask;
        }
        var document = context.Document ?? throw new InvalidOperationException("OpenAPI document is required for bearer references.");
        document.Components ??= new();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[JwtBearerDefaults.AuthenticationScheme] = new OpenApiSecurityScheme {
            Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header
        };
        operation.Security ??= [];
        operation.Security.Add(new() { [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, document)] = [] });
        return Task.CompletedTask;
    }

    public static Task TransformSchemaAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken) {
        if (context.JsonTypeInfo.Type != typeof(ApiLoginRequest) && context.JsonTypeInfo.Type != typeof(ApiUserCreateRequest) &&
            context.JsonTypeInfo.Type != typeof(ApiUserPasswordResetRequest)) {
            return Task.CompletedTask;
        }
        if (schema.Properties is { } properties && properties.TryGetValue("password", out var value) && value is OpenApiSchema password) {
            password.Format = "password";
            password.WriteOnly = true;
            password.MinLength = ApiIdentityRules.MinimumPasswordLength;
            password.MaxLength = ApiIdentityRules.MaximumPasswordLength;
        }
        return Task.CompletedTask;
    }
}
