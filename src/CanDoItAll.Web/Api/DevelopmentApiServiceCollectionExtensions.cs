using System.Text;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace CanDoItAll.Web.Api;

public static class DevelopmentApiServiceCollectionExtensions
{
    public static IServiceCollection AddCanDoItAllDevelopmentApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configuredOptions = configuration
            .GetSection(DevelopmentApiAccessOptions.SectionName)
            .Get<DevelopmentApiAccessOptions>() ?? new DevelopmentApiAccessOptions();
        var validationErrors = DevelopmentApiAccessOptions.Validate(configuredOptions);
        if (validationErrors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", validationErrors));
        }

        services.AddOptions<DevelopmentApiAccessOptions>()
            .BindConfiguration(DevelopmentApiAccessOptions.SectionName)
            .Validate(options => DevelopmentApiAccessOptions.Validate(options).Count == 0, "Development API configuration is invalid.")
            .ValidateOnStart();
        services.TryAddSingleton<IDevelopmentApiTokenService, DevelopmentApiTokenService>();
        services.AddOpenApi();
        services.AddAuthorization();

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
            });

        return services;
    }
}
