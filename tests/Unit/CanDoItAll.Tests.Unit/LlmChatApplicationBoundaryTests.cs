using System.Reflection;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.LlmChats.Application;
using CanDoItAll.Modules.LlmChats.Ports;

namespace CanDoItAll.Tests.Unit;

public sealed class LlmChatApplicationBoundaryTests
{
    [Fact]
    public void Application_services_have_explicit_dependencies_without_service_location()
    {
        var serviceTypes = new[]
        {
            typeof(LlmChatDefinitionApplicationService),
            typeof(LlmChatConversationApplicationService)
        };

        foreach (var type in serviceTypes)
        {
            var constructor = Assert.Single(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
            Assert.DoesNotContain(constructor.GetParameters(), parameter => parameter.ParameterType == typeof(IServiceProvider));
        }
    }

    [Fact]
    public void Commands_and_results_do_not_expose_live_provider_profiles_or_credentials()
    {
        var contractTypes = typeof(CreateLlmChatDefinitionCommand).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(CreateLlmChatDefinitionCommand).Namespace)
            .ToArray();

        foreach (var property in contractTypes.SelectMany(type => type.GetProperties()))
        {
            Assert.NotEqual(typeof(ProviderProfile), property.PropertyType);
            Assert.DoesNotContain("Credential", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ApiKey", property.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Safe_provider_options_expose_capability_but_not_provider_configuration()
    {
        var propertyNames = typeof(LlmChatProviderOption).GetProperties().Select(property => property.Name).ToArray();

        Assert.Contains(nameof(LlmChatProviderOption.Models), propertyNames);
        Assert.DoesNotContain(propertyNames, name => name.Contains("Configuration", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("Credential", StringComparison.OrdinalIgnoreCase));
    }
}
