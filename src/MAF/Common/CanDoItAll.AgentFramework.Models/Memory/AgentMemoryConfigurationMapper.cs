using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.AgentFramework.Models;

internal static class AgentMemoryConfigurationMapper
{
    public static AgentMemoryAccessSettings FromDto(AgentMemoryConfigurationDto dto)
    {
        return new AgentMemoryAccessSettings
        {
            InvocationMode = ResolveInvocationMode(dto),
            CanUseMemoryTools = dto.CanUseMemoryTools,
            RequireContextContributions = dto.RequireContextContributions,
            AllowAsyncContextContributions = dto.AllowAsyncContextContributions,
            CanIngestSources = dto.CanIngestSources,
            PreferredProviderInstanceId = ParseOptionalProviderId(
                dto.PreferredProviderInstanceId,
                "preferredProviderInstanceId"),
            DefaultProviderInstanceId = ParseOptionalProviderId(
                dto.DefaultProviderInstanceId,
                "defaultProviderInstanceId"),
            AllowedProviderInstanceIds = ParseProviderIds(
                dto.AllowedProviderInstanceIds,
                "allowedProviderInstanceIds"),
            ProviderBindings = (dto.ProviderBindings ?? []).Select(ParseBinding).ToArray(),
            AllowedCapabilityIds = ParseCapabilities(dto.AllowedCapabilityIds),
            DeniedCapabilityIds = ParseCapabilities(dto.DeniedCapabilityIds),
            AllowedSourceScopes = ParseSourceScopes(dto.AllowedSourceScopes),
            ProviderAssignments = (dto.ProviderAssignments ?? []).Select(ParseAssignment).ToArray()
        };
    }

    public static AgentMemoryConfigurationDto ToDto(AgentMemoryAccessSettings settings)
    {
        return new AgentMemoryConfigurationDto
        {
            InvocationMode = settings.InvocationMode.ToString(),
            CanUseContextContributions = settings.CanUseContextContributions,
            CanUseMemoryTools = settings.CanUseMemoryTools,
            RequireContextContributions = settings.RequireContextContributions,
            AllowAsyncContextContributions = settings.AllowAsyncContextContributions,
            CanIngestSources = settings.CanIngestSources,
            PreferredProviderInstanceId = settings.PreferredProviderInstanceId?.Value,
            DefaultProviderInstanceId = settings.DefaultProviderInstanceId?.Value,
            AllowedProviderInstanceIds = settings.AllowedProviderInstanceIds.Select(item => item.Value).ToArray(),
            ProviderBindings = settings.ProviderBindings.Select(ToDto).ToArray(),
            AllowedCapabilityIds = settings.AllowedCapabilityIds.Select(item => item.Value).ToArray(),
            DeniedCapabilityIds = settings.DeniedCapabilityIds.Select(item => item.Value).ToArray(),
            AllowedSourceScopes = settings.AllowedSourceScopes.Select(item => item.ToString()).ToArray(),
            ProviderAssignments = settings.ProviderAssignments.Select(ToDto).ToArray()
        };
    }

    private static AgentMemoryInvocationMode ResolveInvocationMode(AgentMemoryConfigurationDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.InvocationMode))
        {
            return dto.CanUseContextContributions == true
                ? AgentMemoryInvocationMode.Automatic
                : AgentMemoryInvocationMode.Disabled;
        }

        if (!Enum.TryParse<AgentMemoryInvocationMode>(dto.InvocationMode, ignoreCase: true, out var mode) ||
            !Enum.IsDefined(mode))
        {
            throw new AgentMemoryConfigurationException(
                $"Unknown agent memory invocation mode '{dto.InvocationMode}'.");
        }

        if (dto.CanUseContextContributions.HasValue &&
            dto.CanUseContextContributions.Value != (mode != AgentMemoryInvocationMode.Disabled))
        {
            throw new AgentMemoryConfigurationException(
                "Agent memory invocationMode conflicts with legacy canUseContextContributions.");
        }

        return mode;
    }

    private static AgentMemoryProviderBindingSetting ParseBinding(AgentMemoryProviderBindingDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Alias) || string.IsNullOrWhiteSpace(dto.ProviderInstanceId))
        {
            throw new AgentMemoryConfigurationException(
                "Every memory provider binding requires a non-empty alias and provider instance id.");
        }

        return new AgentMemoryProviderBindingSetting(
            AgentMemoryProviderAlias.Parse(dto.Alias),
            MemoryProviderInstanceId.Parse(dto.ProviderInstanceId.Trim()),
            dto.IncludeInAutomaticContext,
            ParseRequirement(dto.Requirement));
    }

    private static AgentMemoryProviderAssignmentSetting ParseAssignment(AgentMemoryProviderAssignmentDto dto)
    {
        if (!Enum.TryParse<MemoryProviderAssignmentScope>(dto.Scope, ignoreCase: true, out var scope) ||
            !Enum.IsDefined(scope) ||
            string.IsNullOrWhiteSpace(dto.Key) ||
            string.IsNullOrWhiteSpace(dto.ProviderInstanceId))
        {
            throw new AgentMemoryConfigurationException(
                "Every memory provider assignment requires a valid scope, key and provider instance id.");
        }

        return new AgentMemoryProviderAssignmentSetting(
            scope,
            dto.Key.Trim(),
            MemoryProviderInstanceId.Parse(dto.ProviderInstanceId.Trim()));
    }

    private static IReadOnlyList<MemoryProviderInstanceId> ParseProviderIds(
        IReadOnlyList<string>? values,
        string propertyName)
    {
        if (values is null)
        {
            return [];
        }

        if (values.Any(string.IsNullOrWhiteSpace))
        {
            throw new AgentMemoryConfigurationException(
                $"Memory property '{propertyName}' cannot contain empty provider ids.");
        }

        return values.Select(value => MemoryProviderInstanceId.Parse(value.Trim())).ToArray();
    }

    private static MemoryProviderInstanceId? ParseOptionalProviderId(string? value, string propertyName)
    {
        if (value is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new AgentMemoryConfigurationException(
                $"Memory property '{propertyName}' must be omitted instead of empty.");
        }

        return MemoryProviderInstanceId.Parse(value.Trim());
    }

    private static IReadOnlyList<MemoryCapabilityId> ParseCapabilities(IReadOnlyList<string>? values)
    {
        if (values is null)
        {
            return [];
        }

        if (values.Any(string.IsNullOrWhiteSpace))
        {
            throw new AgentMemoryConfigurationException("Memory capability lists cannot contain empty ids.");
        }

        return values.Select(value => MemoryCapabilityId.Parse(value.Trim())).ToArray();
    }

    private static IReadOnlyList<MemorySourceScope> ParseSourceScopes(IReadOnlyList<string>? values)
    {
        return values?.Select(value =>
            Enum.TryParse<MemorySourceScope>(value, ignoreCase: true, out var scope) && Enum.IsDefined(scope)
                ? scope
                : throw new AgentMemoryConfigurationException($"Unknown memory source scope '{value}'."))
            .ToArray()
        ?? [];
    }

    private static AgentMemoryProviderBindingDto ToDto(AgentMemoryProviderBindingSetting binding)
    {
        return new AgentMemoryProviderBindingDto
        {
            Alias = binding.Alias.Value,
            ProviderInstanceId = binding.ProviderInstanceId.Value,
            IncludeInAutomaticContext = binding.IncludeInAutomaticContext,
            Requirement = binding.Requirement.ToString()
        };
    }

    private static AgentMemoryProviderRequirement ParseRequirement(string? value)
    {
        if (value is null)
        {
            return AgentMemoryProviderRequirement.Optional;
        }

        if (!Enum.TryParse<AgentMemoryProviderRequirement>(value, ignoreCase: true, out var requirement) ||
            !Enum.IsDefined(requirement))
        {
            throw new AgentMemoryConfigurationException(
                $"Unknown memory provider requirement '{value}'.");
        }

        return requirement;
    }

    private static AgentMemoryProviderAssignmentDto ToDto(AgentMemoryProviderAssignmentSetting assignment)
    {
        return new AgentMemoryProviderAssignmentDto
        {
            Scope = assignment.Scope.ToString(),
            Key = assignment.Key,
            ProviderInstanceId = assignment.ProviderInstanceId.Value
        };
    }
}
