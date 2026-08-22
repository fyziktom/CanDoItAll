using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Processes.Projections;
using CanDoItAll.SharedKernel;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Conversations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.Web.Infrastructure;
using CanDoItAll.Web.Api.Streaming;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace CanDoItAll.Web.Api;

public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddCanDoItAllApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configuredOptions = configuration
            .GetSection(ApiAccessOptions.SectionName)
            .Get<ApiAccessOptions>() ?? new ApiAccessOptions();
        var validationErrors = ApiAccessOptions.Validate(configuredOptions);
        if (validationErrors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", validationErrors));
        }

        services.AddOptions<ApiAccessOptions>()
            .BindConfiguration(ApiAccessOptions.SectionName)
            .Validate(options => ApiAccessOptions.Validate(options).Count == 0, "API configuration is invalid.")
            .ValidateOnStart();
        services.TryAddSingleton<IApiTokenService, ApiTokenService>();
        services.TryAddScoped<MemoryProviderApiService>();
        services.TryAddSingleton(
            typeof(ProfileBoundedReplayEventStream<>),
            typeof(ProfileBoundedReplayEventStream<>));
        services.ConfigureAgentApiJson();
        services.ConfigureLlmChatApiJson();
        services.AddOpenApi(options =>
        {
            options.AddOperationTransformer(
                ProjectStructureHttpJsonContract.TransformOpenApiOperationAsync);
            options.AddOperationTransformer(
                WorkflowExternalResponseOpenApiContract.TransformOperationAsync);
        });
        services.AddAuthorization(options =>
        {
            options.AddPolicy(ApiAuthorizationPolicies.IssueTokens, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(context =>
                    ApiAuthorizationPolicies.HasScope(
                        context.User,
                        ApiAccessScopeNames.IssueTokens));
            });
            options.AddPolicy(ApiAuthorizationPolicies.ReadMemoryProviders, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(context =>
                    ApiAuthorizationPolicies.HasApiOrSpecificScope(
                        context.User,
                        ApiAccessScopeNames.ReadMemoryProviders));
            });
            options.AddPolicy(ApiAuthorizationPolicies.WriteMemoryProviders, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(context =>
                    ApiAuthorizationPolicies.HasApiOrSpecificScope(
                        context.User,
                        ApiAccessScopeNames.WriteMemoryProviders));
            });
            options.AddPolicy(ApiAuthorizationPolicies.QueryMemoryProviders, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(context =>
                    ApiAuthorizationPolicies.HasApiOrSpecificScope(
                        context.User,
                        ApiAccessScopeNames.QueryMemoryProviders));
            });
            options.AddPolicy(ApiAuthorizationPolicies.WriteProjectStructure, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(context =>
                    ApiAuthorizationPolicies.HasApiOrSpecificScope(
                        context.User,
                        ApiAccessScopeNames.WriteProjectStructure));
            });
            AddExactScopePolicy(
                options,
                ApiAuthorizationPolicies.ReadLlmChats,
                ApiAccessScopeNames.ReadLlmChats);
            AddExactScopePolicy(
                options,
                ApiAuthorizationPolicies.ManageLlmChats,
                ApiAccessScopeNames.ManageLlmChats);
            AddExactScopePolicy(
                options,
                ApiAuthorizationPolicies.ExecuteLlmChats,
                ApiAccessScopeNames.ExecuteLlmChats);
            AddExactScopePolicy(
                options,
                ApiAuthorizationPolicies.RespondWorkflows,
                ApiAccessScopeNames.RespondWorkflows);
        });
        services.AddHttpContextAccessor();
        services.TryAddScoped<WorkflowExternalResponseApiActorResolver>();
        services.Replace(ServiceDescriptor.Singleton<IWorkflowEventSink, WorkflowApiEventSink>());
        services.TryAddScoped<ProcessRuntimeProjectionProjector>();
        services.Replace(ServiceDescriptor.Scoped<IProcessRuntimeProjector>(serviceProvider =>
            new ApiNotifyingProcessRuntimeProjector(
                serviceProvider.GetRequiredService<ProcessRuntimeProjectionProjector>(),
                serviceProvider.GetRequiredService<ProfileBoundedReplayEventStream<ProcessApiRunEvent>>(),
                serviceProvider.GetRequiredService<ILogger<ApiNotifyingProcessRuntimeProjector>>())));
        services.TryAddScoped<IAgentRecruitingTargetResolver, WorkspaceAgentRecruitingTargetResolver>();
        services.Replace(ServiceDescriptor.Scoped<IFileAccessContextProvider, HttpFileAccessContextProvider>());
        services.Replace(ServiceDescriptor.Singleton<IFileAccessPolicy, WebFileAccessPolicy>());

        if (!configuredOptions.Authorization.Enabled)
        {
            return services;
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuredOptions.Authorization.SigningKey));
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = configuredOptions.Authorization.Issuer,
                    ValidateAudience = true,
                    ValidAudience = configuredOptions.Authorization.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = context =>
                    {
                        context.HandleResponse();
                        return WriteAuthorizationErrorAsync(
                            context.HttpContext,
                            StatusCodes.Status401Unauthorized,
                            "api.authorization-required",
                            "A valid bearer token is required.");
                    },
                    OnForbidden = context => WriteAuthorizationErrorAsync(
                        context.HttpContext,
                        StatusCodes.Status403Forbidden,
                        "api.authorization-forbidden",
                        "The bearer token does not authorize this operation.")
                };
            });

        return services;
    }

    private static void AddExactScopePolicy(
        AuthorizationOptions options,
        string policyName,
        string scopeName)
    {
        options.AddPolicy(policyName, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context =>
                ApiAuthorizationPolicies.HasScope(context.User, scopeName));
        });
    }

    internal static IServiceCollection ConfigureAgentApiJson(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(
                new JsonStringEnumConverter<AgentProviderFailureCategory>(
                    JsonNamingPolicy.CamelCase,
                    allowIntegerValues: false)));
        return services;
    }

    internal static IServiceCollection ConfigureLlmChatApiJson(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(
                new JsonStringEnumConverter<LlmChatDefinitionStatus>(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
            options.SerializerOptions.Converters.Add(
                new JsonStringEnumConverter<LlmChatConversationStatus>(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
            options.SerializerOptions.Converters.Add(
                new JsonStringEnumConverter<LlmChatConversationOrigin>(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
            options.SerializerOptions.Converters.Add(
                new JsonStringEnumConverter<LlmMessageRole>(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
            options.SerializerOptions.Converters.Add(
                new JsonStringEnumConverter<LlmChatOperationStatus>(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        });
        return services;
    }

    private static Task WriteAuthorizationErrorAsync(
        HttpContext httpContext,
        int statusCode,
        string code,
        string message)
    {
        httpContext.Response.StatusCode = statusCode;
        return httpContext.Response.WriteAsJsonAsync(
            new ApiErrorResponse(
                [new ApiErrorItem(code, message, ErrorSeverity.Error)]),
            httpContext.RequestAborted);
    }
}
