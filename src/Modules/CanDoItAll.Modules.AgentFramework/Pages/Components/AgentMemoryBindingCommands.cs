using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public readonly record struct AgentMemoryBindingMove(
    AgentMemoryProviderAlias Alias,
    int Offset);

public readonly record struct AgentMemoryBindingRequirementChange(
    AgentMemoryProviderAlias Alias,
    AgentMemoryProviderRequirement Requirement);
