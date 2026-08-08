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

public sealed record ToolInvocationPolicyEvaluationResult(
    ToolInvocationPolicyContext EffectiveContext,
    ToolInvocationPolicyDecision Decision);

/// <summary>
/// The injected tool-governance pipeline: applies every registered context
/// contributor in registration order, then evaluates the composed context
/// with the configured invocation policy. A governed process run (an audit
/// scope carrying a process-run identity) fails closed when no contributor
/// enriched the context — process restrictions must come from the Processes
/// contributor, never from adapter defaults.
/// </summary>
public sealed class AgentToolInvocationPolicyPipeline
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

    /// <summary>
    /// Applies contributors to the neutral context and evaluates the result.
    /// </summary>
    public async ValueTask<ToolInvocationPolicyEvaluationResult> ComposeAndEvaluateAsync(
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

        if (!string.IsNullOrWhiteSpace(auditScope?.ProcessRunId))
        {
            EnsureGovernedProcessContext(composedContext, auditScope);
        }

        var decision = await policy
            .EvaluateAsync(composedContext, cancellationToken)
            .ConfigureAwait(false);
        return new ToolInvocationPolicyEvaluationResult(composedContext, decision);
    }

    private static void EnsureGovernedProcessContext(
        ToolInvocationPolicyContext context,
        WorkspaceExecutionAuditContext.WorkspaceExecutionAuditScopeState auditScope)
    {
        var hasExactIdentity =
            !string.IsNullOrWhiteSpace(auditScope.ProcessStepId) &&
            string.Equals(context.ProcessRunId, auditScope.ProcessRunId, StringComparison.Ordinal) &&
            string.Equals(context.ProcessStepId, auditScope.ProcessStepId, StringComparison.Ordinal);
        var hasExactRestrictions =
            context.ProcessAllowsProductMutation == auditScope.ProcessAllowsProductMutation &&
            context.ProcessRequiresProductMutationBeforeManagedOutput ==
                auditScope.ProcessRequiresProductMutationBeforeManagedOutput &&
            HasSameValues(
                context.ProcessProductMutationToolNames,
                auditScope.ProcessProductMutationToolNames) &&
            HasSameValues(
                context.ProcessProductMutationRequiredBranchOutcomeKeys,
                auditScope.ProcessProductMutationRequiredBranchOutcomeKeys) &&
            HasSameValues(
                context.ProcessStepAllowedOperations,
                auditScope.ProcessStepAllowedOperations) &&
            string.Equals(
                context.ProcessStepTargetScope,
                auditScope.ProcessStepTargetScope,
                StringComparison.Ordinal);
        if (!hasExactIdentity || !hasExactRestrictions)
        {
            throw new InvalidOperationException(
                "This governed process run requires its exact audit identity and typed restrictions in the effective tool-policy context.");
        }
    }

    private static bool HasSameValues(
        IReadOnlyList<string>? effectiveValues,
        IReadOnlyList<string>? auditValues)
        => effectiveValues is not null &&
           auditValues is not null &&
           effectiveValues.SequenceEqual(auditValues, StringComparer.Ordinal);
}
