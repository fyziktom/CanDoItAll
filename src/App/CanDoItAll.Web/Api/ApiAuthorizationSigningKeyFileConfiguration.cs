using CanDoItAll.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace CanDoItAll.Web.Api;

public static class ApiAuthorizationSigningKeyFileConfiguration
{
    private const int MaximumSigningKeyFileBytes = 4096;
    private const string SigningKeyKey = "Api:Authorization:SigningKey";
    private const string SigningKeyFileKey = "Api:Authorization:SigningKeyFile";

    public static void Apply(IConfiguration configuration, string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        var configuredFile = configuration[SigningKeyFileKey];
        if (string.IsNullOrWhiteSpace(configuredFile))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(configuration[SigningKeyKey]))
        {
            throw new InvalidOperationException(
                $"Configure either {SigningKeyKey} or {SigningKeyFileKey}, not both.");
        }

        configuration[SigningKeyKey] = BoundedConfigurationSecretFileReader.Read(
            configuredFile,
            contentRootPath,
            "API authorization signing key",
            MaximumSigningKeyFileBytes);
    }
}
