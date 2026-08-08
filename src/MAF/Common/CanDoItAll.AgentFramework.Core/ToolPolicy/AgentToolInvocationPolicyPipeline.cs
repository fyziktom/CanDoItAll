namespace CanDoItAll.AgentFramework.Core;

/// <summary>
/// Enriches a provider-neutral <see cref="ToolInvocationPolicyContext"/> with
/// domain-owned facts before policy evaluation. The runtime adapter builds the
/// neutral context (agent, tool, classification, governance, workspace facts)
/// and never interprets domain semantics; a contributor owned by the domain
/// module (for example Processes) maps its typed restrictions onto the
/// context. Contributors may only add or narrow facts — they must never widen
/// authority or remove restrictions another contributor added.
/// </summary>
public interface IToolInvocationPolicyContextContributor
{
    ToolInvocationPolicyContext Contribute(
        ToolInvocationPolicyContext context,
        WorkspaceExecutionAuditContext.WorkspaceExecutionAuditScopeState? auditScope);
}

/// <summary>
/// The injected tool-governance pipeline: applies every registered context
/// contributor in registration order, then evaluates the composed context
/// with the configured invocation policy. A governed process run (an audit
/// scope carrying a process-run identity) fails closed when no contributor
/// enriched the context — process restrictions must come from the Processes
/// contributor, never from adapter defaults.
/// </summary>
public sealed class AgentToolInvocationPolicyPipeline : IAgentToolInvocationPolicy
{
    private readonly IAgentToolInvocationPolicy policy;
    private readonly IReadOnlyList<IToolInvocationPolicyContextContributor> contributors;

    public AgentToolInvocationPolicyPipeline(
        IAgentToolInvocationPolicy policy,
        IReadOnlyList<IToolInvocationPolicyContextContributor>? contributors = null)
    {
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
        this.contributors = contributors ?? [];
    }

    public ValueTask<ToolInvocationPolicyDecision> EvaluateAsync(
        ToolInvocationPolicyContext context,
        CancellationToken cancellationToken)
        => policy.EvaluateAsync(context, cancellationToken);

    /// <summary>
    /// Applies contributors to the neutral context and evaluates the result.
    /// </summary>
    public async ValueTask<ToolInvocationPolicyDecision> ComposeAndEvaluateAsync(
        ToolInvocationPolicyContext context,
        WorkspaceExecutionAuditContext.WorkspaceExecutionAuditScopeState? auditScope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var composedContext = context;
        foreach (var contributor in contributors)
        {
            composedContext = contributor.Contribute(composedContext, auditScope)
                ?? throw new InvalidOperationException(
                    $"Policy context contributor '{contributor.GetType().Name}' returned no context.");
        }

        if (!string.IsNullOrWhiteSpace(auditScope?.ProcessRunId) &&
            ReferenceEquals(composedContext, context))
        {
            throw new InvalidOperationException(
                "This governed process run requires a registered process policy contributor; refusing to evaluate tool policy without its typed restrictions.");
        }

        return await policy.EvaluateAsync(composedContext, cancellationToken).ConfigureAwait(false);
    }
}
