using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web.Infrastructure;
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
        services.AddOpenApi();
        services.AddAuthorization();
        services.AddHttpContextAccessor();
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
