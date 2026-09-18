using System.Reflection;
using CanDoItAll.AgentFramework.Llm.SimpleChats.UI;
using CanDoItAll.AgentFramework.UI.Chat;
using CanDoItAll.AgentFramework.Workflows.UI;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentUiCompiledBoundaryTests {
    [Fact]
    public void Primary_surfaces_have_no_injected_services_or_runtime_assembly_reference() {
        var assemblies = new[] { typeof(AgentChatSurface).Assembly, typeof(LlmChatDefinitionEditorSurface).Assembly, typeof(WorkflowHistorySurface).Assembly };
        foreach (var assembly in assemblies) {
            var surfaces = assembly.GetTypes().Where(type => typeof(ComponentBase).IsAssignableFrom(type)
                && (type.Name.EndsWith("Surface", StringComparison.Ordinal) || type.Name == "AgentCatalogPanel")).ToArray();
            Assert.NotEmpty(surfaces);
            foreach (var surface in surfaces) {
                Assert.DoesNotContain(surface.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                    property => property.IsDefined(typeof(InjectAttribute)));
            }
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference => reference.Name is { } name
                && name.StartsWith("CanDoItAll.", StringComparison.Ordinal)
                && (name.StartsWith("CanDoItAll.Modules.", StringComparison.Ordinal)
                    || name.EndsWith(".Runtime", StringComparison.Ordinal)
                    || name.EndsWith(".Persistence", StringComparison.Ordinal)
                    || name.EndsWith(".Core", StringComparison.Ordinal)
                    || name.Contains(".Maf", StringComparison.Ordinal)));
        }
    }

    [Fact]
    public void Provider_host_uses_ui_ports_and_adapters_depend_only_on_provider_management_ports() {
        var injections = typeof(AgentProviderProfilesPanel).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(property => property.IsDefined(typeof(InjectAttribute))).Select(property => property.PropertyType).ToArray();
        Assert.Contains(typeof(IProviderEditorCommands), injections);
        Assert.Contains(typeof(IProviderProfilesReads), injections);
        Assert.All(injections, dependency => Assert.True(dependency == typeof(IProviderEditorCommands)
            || dependency == typeof(IProviderProfilesReads) || dependency == typeof(ProviderEditorRecovery)
            || dependency == typeof(CanDoItAll.Components.BaseLib.NotificationService), dependency.FullName));
        foreach (var adapter in new[] { typeof(ProviderEditorCommands), typeof(ProviderProfilesReads) }) {
            var dependencies = adapter.GetConstructors().SelectMany(constructor => constructor.GetParameters()).Select(parameter => parameter.ParameterType);
            Assert.All(dependencies, dependency => {
                Assert.True(dependency.IsInterface);
                Assert.Equal(typeof(CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderRuntimeAdministrationService).Assembly, dependency.Assembly);
            });
        }
    }
}
