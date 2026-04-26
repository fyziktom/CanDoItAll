using System.ComponentModel.DataAnnotations;
using CanDoItAll.Mcp.Core.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Mcp.DotNetWatch.Tests;

public sealed class McpHostBuilderExtensionsTests
{
    [Fact]
    public void AddCanDoItAllMcpSettings_LoadsJsonThenEnvironmentOverrides()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"candoitall-mcp-settings-{Guid.NewGuid():N}.json");
        var previous = Environment.GetEnvironmentVariable("CanDoItAllMcp_Server__Name");

        try
        {
            File.WriteAllText(
                settingsPath,
                """
                {
                  "Server": {
                    "Name": "from-json"
                  }
                }
                """);
            Environment.SetEnvironmentVariable("CanDoItAllMcp_Server__Name", "from-environment");

            var configuration = new ConfigurationBuilder()
                .AddCanDoItAllMcpSettings(settingsPath)
                .Build();

            Assert.Equal("from-environment", configuration["Server:Name"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CanDoItAllMcp_Server__Name", previous);

            if (File.Exists(settingsPath))
            {
                File.Delete(settingsPath);
            }
        }
    }

    [Fact]
    public void AddValidatedCanDoItAllMcpOptions_BindsOptionsAndRegistersCustomValidator()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Name"] = "test-server"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddValidatedCanDoItAllMcpOptions<SampleOptions, SampleOptionsValidator>(configuration);

        var validatorDescriptor = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(IValidateOptions<SampleOptions>) &&
                descriptor.ImplementationType == typeof(SampleOptionsValidator));
        Assert.Equal(ServiceLifetime.Singleton, validatorDescriptor.Lifetime);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<SampleOptions>>().Value;

        Assert.Equal("test-server", options.Name);
    }

    [Fact]
    public void AddValidatedCanDoItAllMcpOptions_CanPreserveCustomValidatorOnlyBehavior()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        services.AddValidatedCanDoItAllMcpOptions<SampleOptions, SampleOptionsValidator>(
            configuration,
            validateDataAnnotations: false);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<SampleOptions>>().Value;

        Assert.Null(options.Name);
    }

    [Fact]
    public void ConfigureCanDoItAllMcpStdioLogging_ConfiguresResolvableLoggerFactory()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.ConfigureCanDoItAllMcpStdioLogging());

        using var provider = services.BuildServiceProvider();
        var loggerFactory = provider.GetRequiredService<ILoggerFactory>();

        Assert.NotNull(loggerFactory.CreateLogger("test"));
    }

    private sealed class SampleOptions
    {
        [Required]
        public string? Name { get; set; }
    }

    private sealed class SampleOptionsValidator : IValidateOptions<SampleOptions>
    {
        public ValidateOptionsResult Validate(string? name, SampleOptions options)
        {
            return ValidateOptionsResult.Success;
        }
    }
}
