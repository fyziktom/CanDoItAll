using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.AgentFramework.Models;

/// <summary>
/// When an agent's memory providers are queried, as a JSON integer: 0 Disabled (never; a prompt that starts with a
/// <c>/mem:</c> directive fails the run), 1 Automatic (every turn queries the bindings included in automatic context),
/// 2 ExplicitDirective (only when the prompt starts with <c>/mem:{alias}</c>; the directive is removed from the
/// prompt).
/// </summary>
public enum AgentMemoryInvocationMode
{
    Disabled = 0,
    Automatic = 1,
    ExplicitDirective = 2
}

/// <summary>
/// Whether a bound memory provider must answer, as a JSON integer: 0 Optional (a failure of the provider is tolerated),
/// 1 Required (a failure of the provider fails the run).
/// </summary>
public enum AgentMemoryProviderRequirement
{
    Optional = 0,
    Required = 1
}

/// <summary>
/// Short name of a memory provider binding, used in <c>/mem:{alias}</c> prompt directives. Serialized as an object
/// whose <c>value</c> member holds the alias: lower-case, at most 64 characters, starting with a letter or digit and
/// containing only letters, digits, '.', '_' and '-'. The server currently cannot read this object from a request (its
/// value arrives empty), so a save that includes memory provider bindings is rejected with HTTP 400.
/// </summary>
public readonly record struct AgentMemoryProviderAlias
{
    private const int MaximumLength = 64;

    private AgentMemoryProviderAlias(string value)
    {
        Value = value;
    }

    /// <summary>The alias text, lower-case; for example <c>project-notes</c>.</summary>
    public string Value { get; }

    public static AgentMemoryProviderAlias Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > MaximumLength ||
            !char.IsAsciiLetterOrDigit(normalized[0]) ||
            normalized.Any(character => !IsAllowedCharacter(character)))
        {
            throw new ArgumentException(
                "Memory provider aliases must start with a letter or digit and contain only letters, digits, '.', '_' or '-'.",
                nameof(value));
        }

        return new AgentMemoryProviderAlias(normalized);
    }

    public static bool TryParse(string? value, out AgentMemoryProviderAlias alias)
    {
        try
        {
            alias = Parse(value ?? string.Empty);
            return true;
        }
        catch (ArgumentException)
        {
            alias = default;
            return false;
        }
    }

    public override string ToString() => Value;

    private static bool IsAllowedCharacter(char character)
    {
        return char.IsAsciiLetterOrDigit(character) ||
               character is '.' or '_' or '-';
    }
}

/// <summary>
/// Memory provider bound to an agent, one entry of <c>memoryAccess.providerBindings</c>. The binding order is the order
/// in which providers are queried and their context is presented.
/// </summary>
/// <param name="Alias">
/// Short name of the binding, unique per agent (ignoring case), serialized as <c>{ "value": "project-notes" }</c>.
/// Returned by reads but currently not readable from requests, so a save containing bindings is rejected with
/// HTTP 400.
/// </param>
/// <param name="ProviderInstanceId">
/// Memory provider instance to query, serialized as an object whose <c>value</c> member holds the instance
/// identifier; an instance managed through <c>/api/memory-providers</c>. Each instance may be bound once per agent.
/// </param>
/// <param name="IncludeInAutomaticContext">
/// True (the default) to query this provider on every turn when the invocation mode is 1 Automatic.
/// </param>
/// <param name="Requirement">
/// Whether the provider must answer, as a JSON integer: 0 Optional (the default; a failure is tolerated), 1 Required
/// (a failure fails the run). Another value is rejected.
/// </param>
public sealed record AgentMemoryProviderBindingSetting(
    AgentMemoryProviderAlias Alias,
    MemoryProviderInstanceId ProviderInstanceId,
    bool IncludeInAutomaticContext = true,
    AgentMemoryProviderRequirement Requirement = AgentMemoryProviderRequirement.Optional);

/// <summary>
/// Rule that selects a bound memory provider for an agent's memory tools by run context, one entry of
/// <c>memoryAccess.providerAssignments</c>. Rules are tried by scope in the order WorkflowNode, Workflow, Process,
/// Agent, AgentRole. Each scope and key pair may appear once.
/// </summary>
/// <param name="Scope">
/// What <c>key</c> is compared with, as a JSON integer: 0 Agent (the agent's own identifier), 1 AgentRole (the name of
/// the agent's workload, for example <c>Programming</c>), 2 Workflow and 3 WorkflowNode (the run's source identifier),
/// 4 Process (the process run identifier). Another value is rejected.
/// </param>
/// <param name="Key">
/// Value compared, ignoring case, with the run context selected by <c>scope</c>. Trimmed; must not be blank.
/// </param>
/// <param name="ProviderInstanceId">
/// Memory provider instance to use when the rule matches, serialized as an object whose <c>value</c> member holds the
/// instance identifier; it must be bound and allowed for the agent.
/// </param>
public sealed record AgentMemoryProviderAssignmentSetting(
    MemoryProviderAssignmentScope Scope,
    string Key,
    MemoryProviderInstanceId ProviderInstanceId);

/// <summary>
/// Memory provider access of an agent, the <c>memoryAccess</c> member of the agent editor form, stored in the agent's
/// <c>configurationJson</c> under <c>memory</c>. It configures the memory providers managed through
/// <c>/api/memory-providers</c>; it is unrelated to the workspace memory notes of <c>/api/agents/{agentId}/memory</c>.
/// A save validates and replaces the whole section (invalid values are rejected with HTTP 400), and an omitted object
/// removes all memory access. Known limitation: <c>providerBindings</c> cannot currently be sent back, so an agent that
/// has bindings cannot be saved through this API without losing its memory access.
/// </summary>
public sealed class AgentMemoryAccessSettings
{
    /// <summary>
    /// When the memory providers are queried, as a JSON integer: 0 Disabled (the default; never, and a <c>/mem:</c>
    /// directive fails the run), 1 Automatic (every turn, for bindings included in automatic context),
    /// 2 ExplicitDirective (only when the prompt starts with <c>/mem:{alias}</c>). Another value is rejected.
    /// </summary>
    public AgentMemoryInvocationMode InvocationMode { get; set; }

    /// <summary>
    /// Adds the memory query and operation status tools to the agent's runs; kept only when the invocation mode is
    /// 1 Automatic, otherwise saved as false. The tools also need the agent's <c>canUseTools</c> permission.
    /// </summary>
    public bool CanUseMemoryTools { get; set; }

    /// <summary>
    /// True when the invocation mode is not 0 Disabled. Computed by the server; ignored in requests.
    /// </summary>
    public bool CanUseContextContributions => InvocationMode != AgentMemoryInvocationMode.Disabled;

    /// <summary>
    /// True to fail a run when memory context cannot be obtained or comes back empty, instead of continuing without
    /// it. Not allowed with invocation mode 0 Disabled.
    /// </summary>
    public bool RequireContextContributions { get; set; }

    /// <summary>Not supported: always saved as false.</summary>
    public bool AllowAsyncContextContributions { get; set; }

    /// <summary>Not supported: always saved as false.</summary>
    public bool CanIngestSources { get; set; }

    /// <summary>
    /// Provider instance the memory tools use first when a tool call names none, or null. It must be bound and, when
    /// <c>allowedProviderInstanceIds</c> is not empty, listed there. Automatic context does not use it.
    /// </summary>
    public MemoryProviderInstanceId? PreferredProviderInstanceId { get; set; }

    /// <summary>
    /// Provider instance the memory tools fall back to after the preferred provider and matching assignments, or null.
    /// Same constraints as <c>preferredProviderInstanceId</c>.
    /// </summary>
    public MemoryProviderInstanceId? DefaultProviderInstanceId { get; set; }

    /// <summary>
    /// Provider instances the agent may use; empty means every bound provider. Duplicates (ignoring case) are removed
    /// and the list is sorted. Every binding and assignment must name an allowed provider.
    /// </summary>
    public IReadOnlyList<MemoryProviderInstanceId> AllowedProviderInstanceIds { get; set; } = [];

    /// <summary>
    /// Memory providers bound to the agent, in query order. Aliases and provider instances must each be unique. Known
    /// limitation: a save that contains bindings is currently rejected with HTTP 400, because the alias object cannot
    /// be read from requests.
    /// </summary>
    public IReadOnlyList<AgentMemoryProviderBindingSetting> ProviderBindings { get; set; } = [];

    /// <summary>
    /// Memory protocol capabilities the agent may use, for example <c>context.query.sync</c>,
    /// <c>context.query.async</c> or <c>operations.status</c>; empty means all. A capability cannot be both allowed and
    /// denied.
    /// </summary>
    public IReadOnlyList<MemoryCapabilityId> AllowedCapabilityIds { get; set; } = [];

    /// <summary>Memory protocol capabilities the agent may not use; a denial wins over the allowed list.</summary>
    public IReadOnlyList<MemoryCapabilityId> DeniedCapabilityIds { get; set; } = [];

    /// <summary>
    /// Source scopes passed to the memory providers with each query, as JSON integers: 0 Workspace, 1 Project,
    /// 2 Process, 3 Workflow, 4 Agent, 5 Crm, 6 Resource, 7 Manual. Duplicates are removed. Values are not validated on
    /// save and an undefined value makes later reads and runs of the agent fail, so send only these values. Scope 5 Crm
    /// is also required by the CRM planning tools and some HR tools.
    /// </summary>
    public IReadOnlyList<MemorySourceScope> AllowedSourceScopes { get; set; } = [];

    /// <summary>
    /// Rules that select a bound provider for the memory tools by run context. Each scope and key pair may appear once,
    /// and each rule's provider must be bound and allowed.
    /// </summary>
    public IReadOnlyList<AgentMemoryProviderAssignmentSetting> ProviderAssignments { get; set; } = [];
}

public sealed class AgentMemoryConfigurationException : InvalidOperationException
{
    public AgentMemoryConfigurationException(string message)
        : base(message)
    {
    }

    public AgentMemoryConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
