using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

internal sealed class AgentMemorySettingsPanelState(
    IMemoryProviderProfileStore providerProfileStore,
    IEnumerable<IMemoryProviderDriver> providerDrivers,
    ILogger logger,
    AgentMemoryAccessSettings initialValue)
{
    private AgentMemoryAccessSettings value = initialValue;

    public string NewAlias { get; set; } = string.Empty;

    public string NewProviderInstanceId { get; set; } = string.Empty;

    public bool IncludeInAutomaticContext { get; set; } = true;

    public AgentMemoryProviderRequirement NewRequirement { get; set; }

    public string ValidationMessage { get; private set; } = string.Empty;

    public string ProviderLoadError { get; private set; } = string.Empty;

    public IReadOnlyList<MemoryProviderProfile> AvailableProviders { get; private set; } = [];

    public bool CanAddBindings =>
        string.IsNullOrWhiteSpace(ProviderLoadError) && AvailableProviders.Count > 0;

    public void UpdateValue(AgentMemoryAccessSettings updatedValue)
    {
        value = updatedValue;
        EnforceInvocationMode();
    }

    public async Task LoadProvidersAsync()
    {
        try
        {
            var driverCounts = providerDrivers
                .GroupBy(driver => driver.DriverKind)
                .ToDictionary(group => group.Key, group => group.Count());
            AvailableProviders = (await providerProfileStore.ListAsync())
                .Where(provider => provider.IsEnabled &&
                    provider.HealthState == MemoryProviderHealthState.Healthy &&
                    driverCounts.GetValueOrDefault(provider.DriverKind) == 1 &&
                    provider.Manifest.Capabilities.Any(capability =>
                        capability.Supported && capability.Id == MemoryCapabilityIds.ContextQuerySync))
                .OrderBy(provider => provider.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(provider => provider.InstanceId.Value, StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception exception)
        {
            logger.LogError(
                "Failed to load enabled memory providers for agent configuration. Exception type: {ExceptionType}.",
                exception.GetType().Name);
            ProviderLoadError = "Enabled memory providers could not be loaded. Agent memory bindings cannot be changed.";
        }
    }

    public Task HandleInvocationModeChanged(AgentMemoryInvocationMode mode)
    {
        value.InvocationMode = mode;
        EnforceInvocationMode();
        if (mode == AgentMemoryInvocationMode.Disabled)
        {
            value.RequireContextContributions = false;
            value.AllowAsyncContextContributions = false;
        }

        return Task.CompletedTask;
    }

    public void HandleMemoryToolsChanged(ChangeEventArgs eventArgs)
    {
        if (value.InvocationMode != AgentMemoryInvocationMode.Automatic)
        {
            value.CanUseMemoryTools = false;
            value.CanIngestSources = false;
            return;
        }

        value.CanUseMemoryTools = ReadCheckbox(eventArgs.Value);
        if (!value.CanUseMemoryTools)
        {
            value.CanIngestSources = false;
        }
    }

    public Task AddBindingAsync()
    {
        if (!CanAddBindings)
        {
            ValidationMessage = string.IsNullOrWhiteSpace(ProviderLoadError)
                ? "Configure and enable a memory provider before adding a binding."
                : ProviderLoadError;
            return Task.CompletedTask;
        }

        try
        {
            var alias = AgentMemoryProviderAlias.Parse(NewAlias);
            if (value.ProviderBindings.Any(binding => binding.Alias == alias))
            {
                ValidationMessage = $"Alias '{alias}' is already bound.";
                return Task.CompletedTask;
            }

            var providerId = MemoryProviderInstanceId.Parse(NewProviderInstanceId.Trim());
            if (!AvailableProviders.Any(provider => provider.InstanceId == providerId))
            {
                ValidationMessage = "Select an enabled configured memory provider.";
                return Task.CompletedTask;
            }

            if (value.ProviderBindings.Any(binding => string.Equals(
                    binding.ProviderInstanceId.Value,
                    providerId.Value,
                    StringComparison.OrdinalIgnoreCase)))
            {
                ValidationMessage = $"Provider '{providerId}' is already bound to this agent.";
                return Task.CompletedTask;
            }

            value.ProviderBindings = value.ProviderBindings
                .Append(new AgentMemoryProviderBindingSetting(
                    alias,
                    providerId,
                    IncludeInAutomaticContext,
                    NewRequirement))
                .ToArray();
            AddToExistingAllowlist(providerId);
            NewAlias = string.Empty;
            NewProviderInstanceId = string.Empty;
            IncludeInAutomaticContext = true;
            NewRequirement = AgentMemoryProviderRequirement.Optional;
            ValidationMessage = string.Empty;
        }
        catch (ArgumentException exception)
        {
            ValidationMessage = exception.Message;
        }

        return Task.CompletedTask;
    }

    public Task RemoveBindingAsync(AgentMemoryProviderAlias alias)
    {
        AgentMemoryBindingRemovalPolicy.Remove(value, alias);
        return Task.CompletedTask;
    }

    public Task MoveBindingAsync(AgentMemoryProviderAlias alias, int offset)
    {
        var bindings = value.ProviderBindings.ToList();
        var currentIndex = bindings.FindIndex(binding => binding.Alias == alias);
        var targetIndex = currentIndex + offset;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= bindings.Count)
        {
            return Task.CompletedTask;
        }

        var binding = bindings[currentIndex];
        bindings.RemoveAt(currentIndex);
        bindings.Insert(targetIndex, binding);
        value.ProviderBindings = bindings;
        return Task.CompletedTask;
    }

    public Task UpdateRequirementAsync(
        AgentMemoryProviderAlias alias,
        AgentMemoryProviderRequirement requirement)
    {
        value.ProviderBindings = value.ProviderBindings
            .Select(binding => binding.Alias == alias
                ? binding with { Requirement = requirement }
                : binding)
            .ToArray();
        return Task.CompletedTask;
    }

    public static bool ReadCheckbox(object? value) => value is bool isChecked && isChecked;

    public static string DescribeMode(AgentMemoryInvocationMode mode) => mode switch
    {
        AgentMemoryInvocationMode.Disabled => "Disabled",
        AgentMemoryInvocationMode.Automatic => "Automatic",
        AgentMemoryInvocationMode.ExplicitDirective => "Explicit /mem directive",
        _ => mode.ToString()
    };

    private void AddToExistingAllowlist(MemoryProviderInstanceId providerId)
    {
        if (value.AllowedProviderInstanceIds.Count == 0 ||
            value.AllowedProviderInstanceIds.Any(id => id == providerId))
        {
            return;
        }

        value.AllowedProviderInstanceIds = value.AllowedProviderInstanceIds
            .Append(providerId)
            .ToArray();
    }

    private void EnforceInvocationMode()
    {
        if (value.InvocationMode == AgentMemoryInvocationMode.Automatic)
        {
            return;
        }

        value.CanUseMemoryTools = false;
        value.CanIngestSources = false;
    }
}
