using System.Reflection;
using CanDoItAll.AgentFramework.Core;
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
            .Where(type => type is { IsClass: true, IsAbstract: false } && !type.ContainsGenericParameters)
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

        foreach (var implementationType in implementationTypes.Where(IsConcreteWorkflowExecutor))
        {
            var runtimeExecutorType = implementationType;
            services.AddScoped(typeof(IWorkflowExecutor), serviceProvider =>
                CreateRuntimePackageWorkflowExecutor(serviceProvider, runtimeExecutorType, pluginDescriptor));
            services.AddScoped<IWorkflowExecutorDescriptorSource>(serviceProvider =>
                new RuntimePackageWorkflowExecutorDescriptorSource(
                    CreateRuntimePackageWorkflowExecutor(serviceProvider, runtimeExecutorType, pluginDescriptor)));
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
            return new RuntimePackageWorkflowExecutor(executor, pluginDescriptor, implementationType);
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
}
