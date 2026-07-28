using System.Diagnostics;
using System.Diagnostics.Metrics;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public enum ProviderSnapshotRevisionProbeKind
{
    Provider,
    ProviderSet
}

public enum ProviderSnapshotRevisionProbeOutcome
{
    Current,
    Changed,
    Failed
}

public static class AgentFrameworkTelemetry
{
    public const string SourceName = "CanDoItAll.AgentFramework";

    public static ActivitySource ActivitySource { get; } = new(SourceName);

    public static Meter Meter { get; } = new(SourceName);

    private static readonly Counter<long> RunCounter = Meter.CreateCounter<long>("agentframework.run.count");
    private static readonly Counter<long> RunResumeCounter = Meter.CreateCounter<long>("agentframework.run.resume.count");
    private static readonly UpDownCounter<long> PendingApprovalCounter = Meter.CreateUpDownCounter<long>("agentframework.pending_approval.count");
    private static readonly Histogram<double> ApprovalWaitDurationMs = Meter.CreateHistogram<double>("agentframework.approval.wait.duration.ms");
    private static readonly Counter<long> ToolExecutionCounter = Meter.CreateCounter<long>("agentframework.tool.execution.count");
    private static readonly Counter<long> ArtifactCounter = Meter.CreateCounter<long>("agentframework.artifact.count");
    private static readonly Counter<long> ProviderErrorCounter = Meter.CreateCounter<long>("agentframework.provider.error.count");
    private static readonly Counter<long> ProviderSnapshotRevisionProbeCounter =
        Meter.CreateCounter<long>(
            "agentframework.provider.snapshot_revision_probe.count");
    private static readonly Histogram<double>
        ProviderSnapshotRevisionProbeDurationMs =
        Meter.CreateHistogram<double>(
            "agentframework.provider.snapshot_revision_probe.duration.ms");
    private static readonly Counter<long> CommandTimeoutCounter = Meter.CreateCounter<long>("agentframework.command.timeout.count");

    public static Activity? StartRunActivity(string activityName, ExecutionRunRecord run)
    {
        var activity = ActivitySource.StartActivity(activityName, ActivityKind.Internal);
        ApplyRunTags(activity, run);
        return activity;
    }

    public static void ApplyRunTags(Activity? activity, ExecutionRunRecord run)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetTag("agentframework.execution_run_id", run.Id.ToString("N"));
        activity.SetTag("agentframework.agent_id", run.AgentId.ToString("N"));
        activity.SetTag("agentframework.chat_session_id", run.ChatSessionId?.ToString("N") ?? string.Empty);
        activity.SetTag("agentframework.correlation_id", run.CorrelationId);
        activity.SetTag("agentframework.causation_id", run.CausationId);
        activity.SetTag("agentframework.source_kind", run.SourceKind);
        activity.SetTag("agentframework.source_id", run.SourceId);
        activity.SetTag("agentframework.requested_by", run.RequestedBy);
        activity.SetTag("agentframework.requested_by_kind", run.RequestedByKind);
        activity.SetTag("agentframework.provider_name", run.ProviderName);
        activity.SetTag("agentframework.model", run.Model);
        activity.SetTag("agentframework.process_run_id", run.ProcessRunId);
        activity.SetTag("agentframework.process_step_id", run.ProcessStepId);
        activity.SetTag("agentframework.scheduler_run_id", run.SchedulerRunId);
        activity.SetTag("agentframework.message_id", run.MessageId);
        activity.SetTag("agentframework.structured_output_contract_key", run.StructuredOutputContractKey);
        activity.SetTag("agentframework.structured_output_schema_name", run.StructuredOutputSchemaName);
    }

    public static void ApplyCurrentAuditScope(Activity? activity)
    {
        if (activity is null)
        {
            return;
        }

        var scope = WorkspaceExecutionAuditContext.Current;
        if (scope is null)
        {
            return;
        }

        activity.SetTag("agentframework.execution_run_id", scope.ExecutionRunId.ToString("N"));
        activity.SetTag("agentframework.agent_id", scope.AgentId.ToString("N"));
        activity.SetTag("agentframework.chat_session_id", scope.ChatSessionId?.ToString("N") ?? string.Empty);
        activity.SetTag("agentframework.correlation_id", scope.CorrelationId);
        activity.SetTag("agentframework.source_kind", scope.SourceKind);
        activity.SetTag("agentframework.source_id", scope.SourceId);
        activity.SetTag("agentframework.provider_name", scope.ProviderName);
        activity.SetTag("agentframework.model", scope.Model);
        activity.SetTag("agentframework.process_run_id", scope.ProcessRunId);
        activity.SetTag("agentframework.process_step_id", scope.ProcessStepId);
        activity.SetTag("agentframework.scheduler_run_id", scope.SchedulerRunId);
        activity.SetTag("agentframework.message_id", scope.MessageId);
    }

    public static void RecordRunOutcome(ExecutionRunRecord run)
    {
        var tags = CreateRunTags(run);
        tags.Add("agentframework.outcome", run.Outcome?.ToString() ?? "Unknown");
        tags.Add("agentframework.state", run.State.ToString());
        RunCounter.Add(1, tags);
    }

    public static void RecordRunResume(ExecutionRunRecord run)
    {
        RunResumeCounter.Add(1, CreateRunTags(run));
    }

    public static void RecordPendingApprovalDelta(ExecutionRunRecord run, long delta)
    {
        if (delta == 0)
        {
            return;
        }

        PendingApprovalCounter.Add(delta, CreateRunTags(run));
    }

    public static void RecordApprovalWaitDuration(ExecutionRunRecord run, ExecutionApprovalRecord approval, TimeSpan duration)
    {
        var tags = CreateRunTags(run);
        tags.Add("agentframework.approval_id", approval.ApprovalId);
        tags.Add("agentframework.tool_name", approval.ToolName);
        tags.Add("agentframework.tool_kind", approval.ToolKind);
        tags.Add("agentframework.approval_status", approval.Status.ToString());
        ApprovalWaitDurationMs.Record(duration.TotalMilliseconds, tags);
    }

    public static void RecordToolExecution(string toolFamily, string toolName, string riskClass)
    {
        var tags = CreateScopeTags();
        tags.Add("agentframework.tool_family", toolFamily);
        tags.Add("agentframework.tool_name", toolName);
        tags.Add("agentframework.risk_class", riskClass);
        ToolExecutionCounter.Add(1, tags);
    }

    public static void RecordArtifactRegistration(string artifactKind, string contentType)
    {
        var tags = CreateScopeTags();
        tags.Add("agentframework.artifact_kind", artifactKind);
        tags.Add("agentframework.content_type", contentType);
        ArtifactCounter.Add(1, tags);
    }

    public static void RecordProviderError(ProviderProfile provider, string model)
    {
        var tags = CreateScopeTags();
        tags.Add("agentframework.provider_name", provider.Name);
        tags.Add("agentframework.model", model);
        ProviderErrorCounter.Add(1, tags);
    }

    public static void RecordProviderSnapshotRevisionProbe(
        ProviderSnapshotRevisionProbeKind kind,
        ProviderSnapshotRevisionProbeOutcome outcome,
        TimeSpan duration)
    {
        TagList tags = [];
        tags.Add(
            "agentframework.provider_revision_probe.kind",
            kind.ToString());
        tags.Add(
            "agentframework.provider_revision_probe.outcome",
            outcome.ToString());
        ProviderSnapshotRevisionProbeCounter.Add(1, tags);
        ProviderSnapshotRevisionProbeDurationMs.Record(
            duration.TotalMilliseconds,
            tags);
    }

    public static void RecordCommandTimeout(string recipeId, string riskClass)
    {
        var tags = CreateScopeTags();
        tags.Add("agentframework.recipe_id", recipeId);
        tags.Add("agentframework.risk_class", riskClass);
        CommandTimeoutCounter.Add(1, tags);
    }

    private static TagList CreateRunTags(ExecutionRunRecord run)
    {
        TagList tags = [];
        tags.Add("agentframework.execution_run_id", run.Id.ToString("N"));
        tags.Add("agentframework.agent_id", run.AgentId.ToString("N"));
        tags.Add("agentframework.chat_session_id", run.ChatSessionId?.ToString("N") ?? string.Empty);
        tags.Add("agentframework.correlation_id", run.CorrelationId);
        tags.Add("agentframework.source_kind", run.SourceKind);
        tags.Add("agentframework.source_id", run.SourceId);
        tags.Add("agentframework.provider_name", run.ProviderName);
        tags.Add("agentframework.model", run.Model);
        tags.Add("agentframework.process_run_id", run.ProcessRunId);
        tags.Add("agentframework.process_step_id", run.ProcessStepId);
        tags.Add("agentframework.scheduler_run_id", run.SchedulerRunId);
        tags.Add("agentframework.message_id", run.MessageId);
        tags.Add("agentframework.structured_output_contract_key", run.StructuredOutputContractKey);
        return tags;
    }

    private static TagList CreateScopeTags()
    {
        TagList tags = [];
        var scope = WorkspaceExecutionAuditContext.Current;
        if (scope is null)
        {
            return tags;
        }

        tags.Add("agentframework.execution_run_id", scope.ExecutionRunId.ToString("N"));
        tags.Add("agentframework.agent_id", scope.AgentId.ToString("N"));
        tags.Add("agentframework.chat_session_id", scope.ChatSessionId?.ToString("N") ?? string.Empty);
        tags.Add("agentframework.correlation_id", scope.CorrelationId);
        tags.Add("agentframework.source_kind", scope.SourceKind);
        tags.Add("agentframework.source_id", scope.SourceId);
        tags.Add("agentframework.provider_name", scope.ProviderName);
        tags.Add("agentframework.model", scope.Model);
        tags.Add("agentframework.process_run_id", scope.ProcessRunId);
        tags.Add("agentframework.process_step_id", scope.ProcessStepId);
        tags.Add("agentframework.scheduler_run_id", scope.SchedulerRunId);
        tags.Add("agentframework.message_id", scope.MessageId);
        return tags;
    }
}
