using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Plugins.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Plugins;

public static class PluginWorkflowExecutorRuntimeRegistration
{
    public static void RegisterWorkflowExecutors(
        IServiceCollection services,
        Assembly assembly,
        PluginDescriptor pluginDescriptor)
        => RegisterWorkflowExecutors(
            services,
            DiscoverWorkflowExecutorTypes(assembly),
            pluginDescriptor);

    public static IReadOnlyList<Type> DiscoverWorkflowExecutorTypes(Assembly assembly)
        => assembly.DefinedTypes
            .Where(type => type is { IsVisible: true, IsClass: true, IsAbstract: false } && !type.ContainsGenericParameters)
            .Select(type => type.AsType())
            .Where(typeof(IWorkflowExecutor).IsAssignableFrom)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

    public static void RegisterWorkflowExecutors(
        IServiceCollection services,
        IEnumerable<Type> implementationTypes,
        PluginDescriptor pluginDescriptor)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(implementationTypes);
        ArgumentNullException.ThrowIfNull(pluginDescriptor);

        var runtimeTypes = implementationTypes
            .Where(IsConcreteWorkflowExecutor)
            .Distinct()
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
        var declaredExecutorIds = pluginDescriptor.WorkflowExecutors
            .Select(executor => executor.ExecutorId.Value)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (runtimeTypes.Length != declaredExecutorIds.Length)
        {
            throw PluginWorkflowExecutorActivationException.ManifestRuntimeMismatch(
                pluginDescriptor,
                declaredExecutorIds,
                runtimeTypes.Select(type => $"unresolved:{type.FullName ?? type.Name}").ToArray(),
                runtimeTypes);
        }

        if (runtimeTypes.Length == 0)
        {
            return;
        }

        var descriptors = pluginDescriptor.WorkflowExecutors
            .Select(executor => PluginWorkflowExecutorSourceMapper.CreateDescriptor(pluginDescriptor, executor))
            .ToArray();
        foreach (var descriptor in descriptors)
        {
            services.AddSingleton<IWorkflowExecutorContribution>(
                new RuntimePackageWorkflowExecutorDescriptorContribution(descriptor));
        }

        var registrationKey = new RuntimePackageWorkflowExecutorRegistrationKey();
        services.AddKeyedScoped<RuntimePackageWorkflowExecutorContributionGroup>(
            registrationKey,
            (serviceProvider, _) => CreateContributionGroup(
                serviceProvider,
                runtimeTypes,
                pluginDescriptor));
        foreach (var descriptor in descriptors)
        {
            var executorId = descriptor.Id;
            services.AddScoped<IWorkflowExecutor>(serviceProvider =>
                serviceProvider
                    .GetRequiredKeyedService<RuntimePackageWorkflowExecutorContributionGroup>(registrationKey)
                    .ImplementationsById[executorId]);
        }
    }

    public static RuntimePackageWorkflowExecutor CreateRuntimePackageWorkflowExecutor(
        IServiceProvider serviceProvider,
        Type implementationType,
        PluginDescriptor pluginDescriptor)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(implementationType);
        ArgumentNullException.ThrowIfNull(pluginDescriptor);

        try
        {
            var executor = (IWorkflowExecutor)ActivatorUtilities.CreateInstance(serviceProvider, implementationType);
            var manifestDescriptor = pluginDescriptor.WorkflowExecutors.SingleOrDefault(item =>
                item.ExecutorId == executor.Descriptor.Id);
            if (manifestDescriptor is null)
            {
                throw PluginWorkflowExecutorActivationException.ManifestRuntimeMismatch(
                    pluginDescriptor,
                    pluginDescriptor.WorkflowExecutors.Select(item => item.ExecutorId.Value).ToArray(),
                    [executor.Descriptor.Id.Value],
                    [implementationType]);
            }

            ThrowIfMetadataDoesNotMatch(pluginDescriptor, manifestDescriptor, executor, implementationType);
            return new RuntimePackageWorkflowExecutor(
                executor,
                PluginWorkflowExecutorSourceMapper.CreateDescriptor(pluginDescriptor, manifestDescriptor));
        }
        catch (PluginWorkflowExecutorActivationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw PluginWorkflowExecutorActivationException.ActivationFailed(
                pluginDescriptor,
                implementationType,
                exception);
        }
    }

    private static bool IsConcreteWorkflowExecutor(Type implementationType)
        => implementationType is { IsClass: true, IsAbstract: false } &&
           !implementationType.ContainsGenericParameters &&
           typeof(IWorkflowExecutor).IsAssignableFrom(implementationType);

    private static RuntimePackageWorkflowExecutorContributionGroup CreateContributionGroup(
        IServiceProvider serviceProvider,
        IReadOnlyList<Type> implementationTypes,
        PluginDescriptor pluginDescriptor)
    {
        var executors = implementationTypes
            .Select(type => CreateRuntimePackageWorkflowExecutor(serviceProvider, type, pluginDescriptor))
            .ToArray();
        var declaredExecutorIds = pluginDescriptor.WorkflowExecutors
            .Select(executor => executor.ExecutorId.Value)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var runtimeExecutorIds = executors
            .Select(executor => executor.Descriptor.Id.Value)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (!declaredExecutorIds.SequenceEqual(runtimeExecutorIds, StringComparer.OrdinalIgnoreCase))
        {
            throw PluginWorkflowExecutorActivationException.ManifestRuntimeMismatch(
                pluginDescriptor,
                declaredExecutorIds,
                runtimeExecutorIds,
                implementationTypes);
        }

        return new RuntimePackageWorkflowExecutorContributionGroup(
            executors.ToDictionary(executor => executor.Descriptor.Id));
    }

    private static void ThrowIfMetadataDoesNotMatch(
        PluginDescriptor pluginDescriptor,
        PluginWorkflowExecutorDescriptor manifestDescriptor,
        IWorkflowExecutor executor,
        Type implementationType)
    {
        var mismatchedFields = FindMetadataMismatches(manifestDescriptor, executor.Descriptor);
        if (mismatchedFields.Count == 0)
        {
            return;
        }

        throw PluginWorkflowExecutorActivationException.ManifestRuntimeMetadataMismatch(
            pluginDescriptor,
            executor.Descriptor.Id,
            implementationType,
            mismatchedFields);
    }

    private static IReadOnlyList<string> FindMetadataMismatches(
        PluginWorkflowExecutorDescriptor manifest,
        WorkflowExecutorDescriptor runtime)
    {
        var mismatches = new List<string>();
        AddMismatch(mismatches, "name", string.Equals(manifest.Name, runtime.Name, StringComparison.Ordinal));
        AddMismatch(mismatches, "description", string.Equals(manifest.Description, runtime.Description, StringComparison.Ordinal));
        AddMismatch(mismatches, "category", manifest.Category == runtime.Category);
        AddMismatch(mismatches, "settingsRendererKey", string.Equals(manifest.SettingsRendererKey.Value, runtime.SetupRendererKey, StringComparison.Ordinal));
        AddMismatch(mismatches, "settingsPresentationMode", manifest.SettingsPresentationMode == runtime.SettingsPresentationMode);
        AddMismatch(mismatches, "settingsSchema", JsonValuesEqual(manifest.SettingsSchema, runtime.ConfigurationSchema));
        AddMismatch(mismatches, "inputShape", manifest.InputShape == runtime.InputShape);
        AddMismatch(mismatches, "resultShape", manifest.ResultShape == runtime.ResultShape);
        AddMismatch(mismatches, "defaultPolicy", manifest.DefaultPolicy == runtime.DefaultPolicy);
        AddMismatch(mismatches, "defaultSettingsJson", JsonValuesEqual(manifest.DefaultSettingsJson, runtime.DefaultSettingsJson));
        AddMismatch(mismatches, "simulation", SimulationsEqual(manifest.Simulation, runtime.Simulation));
        AddMismatch(mismatches, "permissionPolicy", manifest.PermissionPolicy == runtime.PermissionPolicy);
        AddMismatch(mismatches, "sideEffects", manifest.SideEffects == runtime.SideEffects);
        AddMismatch(mismatches, "deterministicTestMode", manifest.DeterministicTestMode == runtime.DeterministicTestMode);
        return mismatches;
    }

    private static void AddMismatch(
        ICollection<string> mismatches,
        string field,
        bool matches)
    {
        if (!matches)
        {
            mismatches.Add(field);
        }
    }

    private static bool SimulationsEqual(
        WorkflowExecutorSimulationDescriptor manifest,
        WorkflowExecutorSimulationDescriptor runtime)
        => manifest.SupportsPreviewSimulation == runtime.SupportsPreviewSimulation &&
           string.Equals(manifest.Description, runtime.Description, StringComparison.Ordinal) &&
           JsonValuesEqual(manifest.OutputTemplateJson, runtime.OutputTemplateJson, allowEmpty: true);

    private static bool JsonValuesEqual<T>(T manifest, T runtime)
    {
        var manifestNode = JsonSerializer.SerializeToNode(manifest);
        var runtimeNode = JsonSerializer.SerializeToNode(runtime);
        return JsonNode.DeepEquals(manifestNode, runtimeNode);
    }

    private static bool JsonValuesEqual(
        string manifest,
        string runtime,
        bool allowEmpty = false)
    {
        if (allowEmpty && string.IsNullOrWhiteSpace(manifest) && string.IsNullOrWhiteSpace(runtime))
        {
            return true;
        }

        try
        {
            return JsonNode.DeepEquals(JsonNode.Parse(manifest), JsonNode.Parse(runtime));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed class RuntimePackageWorkflowExecutorRegistrationKey;

    private sealed class RuntimePackageWorkflowExecutorContributionGroup(
        IReadOnlyDictionary<WorkflowExecutorId, RuntimePackageWorkflowExecutor> implementationsById)
    {
        public IReadOnlyDictionary<WorkflowExecutorId, RuntimePackageWorkflowExecutor> ImplementationsById { get; } = implementationsById;
    }

    private sealed class RuntimePackageWorkflowExecutorDescriptorContribution(
        WorkflowExecutorDescriptor descriptor) : IWorkflowExecutorContribution
    {
        public WorkflowExecutorDescriptor Descriptor { get; } = descriptor;
    }
}
