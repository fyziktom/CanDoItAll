using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Web.Api;

/// <summary>
/// Short catalog entry of a technical agent definition, as listed by <c>GET /api/agents/bootstrap</c>. It describes
/// the agent definition only, not the CRM party that may represent the agent; read the full definition with
/// <c>GET /api/agents/{agentId}</c>.
/// </summary>
/// <param name="Id">Identifier of the agent definition.</param>
/// <param name="Name">Display name of the agent.</param>
/// <param name="RoleTitle">Role title shown with the agent; may be empty.</param>
/// <param name="Summary">Short description of the agent; may be empty.</param>
/// <param name="Status">
/// Lifecycle status of the agent, as a JSON integer: 0 Draft, 1 Active, 2 Suspended, 3 Archived.
/// </param>
/// <param name="Model">Model identifier set on the agent; empty means the provider profile's default model.</param>
/// <param name="IsTemplate">True when the agent is a template used to create working agents.</param>
/// <param name="AvatarImageUrl">
/// Avatar image reference (a bundled avatar path or a <c>data:</c> image URL), or null when none is set.
/// </param>
internal sealed record AgentCatalogItemApiResponse(
    Guid Id,
    string Name,
    string RoleTitle,
    string Summary,
    AgentLifecycleStatus Status,
    string Model,
    bool IsTemplate,
    string? AvatarImageUrl);

/// <summary>
/// Tool call of an agent execution run that waits for an approval decision. Decide it with
/// <c>POST /api/agents/execution-runs/{executionRunId}/pending-approvals</c>; the run's approval history, including
/// decided approvals, is listed by <c>GET /api/agents/execution-runs/{executionRunId}/approvals</c>.
/// </summary>
/// <param name="ApprovalId">
/// Opaque identifier of the approval request, issued by the model runtime; copy it exactly into a decision.
/// </param>
/// <param name="ToolName">Name of the tool the agent wants to call.</param>
/// <param name="ToolKind">
/// Kind of the tool call: <c>mcp</c> (a tool of an MCP server), <c>function</c> (a function tool) or <c>tool</c>
/// (another kind of tool call).
/// </param>
internal sealed record AgentPendingApprovalApiResponse(
    string ApprovalId,
    string ToolName,
    string ToolKind);

/// <summary>
/// Identity of a change that the owning feature's receipt confirms was committed by a tool call of a cancelled run.
/// The change exists even though the run was cancelled: read it back from its owner instead of repeating the call.
/// </summary>
/// <param name="SourceKind">Kind of the owner record that holds the change, as recorded by the owner.</param>
/// <param name="SourceId">Identifier of that owner record, as recorded by the owner.</param>
internal sealed record AgentCommittedEffectApiResponse(string SourceKind, string SourceId);

/// <summary>
/// Reconciled state of one admitted tool call of a cancelled agent execution run.
/// </summary>
/// <param name="IntentId">Identifier of the admitted tool call within the run.</param>
/// <param name="ToolName">Name of the tool.</param>
/// <param name="EffectState">
/// What is known about the call's effect, as a JSON integer: 0 Unknown (it may or may not have changed something),
/// 1 None (the call changes nothing, for example a read), 2 NotCommitted (no change was made), 3 Committed (the change
/// was made).
/// </param>
/// <param name="Disposition">
/// How the cancellation resolved the call, as a JSON integer, or null when the call was not cancelled (for example
/// because it completed before the cancellation): 0 NotDispatched (never started), 1 NoExternalMutation (a read that
/// changed nothing), 2 ReceiptCommitted (the owner's receipt shows the change was made), 3 CancelledUnreconciled
/// (started, and its effect could not be confirmed).
/// </param>
/// <param name="Reason">
/// Why that disposition applies, as a JSON integer, or null when the call was not cancelled: 0 NeverDispatched,
/// 1 ReceiptFound, 2 ReceiptNotObserved (the owner reported no receipt), 3 NoReceiptProtocol (the tool offers no
/// receipt lookup), 4 CurrentAccessDenied (the receipt lookup was refused by current access), 5 ReadOnlyInvocation.
/// </param>
/// <param name="CommittedEffect">
/// Identity of the committed change when <c>disposition</c> is 2 ReceiptCommitted; otherwise null.
/// </param>
internal sealed record AgentToolCancellationOutcomeApiResponse(
    Guid IntentId,
    string ToolName,
    AgentToolEffectState EffectState,
    AgentToolCancellationDisposition? Disposition,
    AgentToolCancellationReason? Reason,
    AgentCommittedEffectApiResponse? CommittedEffect);

/// <summary>
/// Result of reconciling a cancelled agent execution run, returned by
/// <c>POST /api/agents/execution-runs/{executionRunId}/reconcile-cancellation</c>. It lists every tool call admitted
/// for the run with what is known about its effect, and every model-provider request that carried approved tool calls.
/// </summary>
/// <param name="ExecutionRunId">Identifier of the reconciled execution run.</param>
/// <param name="ChatSessionId">
/// Identifier of the run's chat session; the all-zero GUID for a run without a chat session.
/// </param>
/// <param name="HasUnknownEffects">
/// True when at least one tool call still has effect state 0 Unknown or a provider request is not in state
/// 1 ResponseAdmitted. Changes may then exist without confirmation: check the affected records with their owners
/// before repeating any work.
/// </param>
/// <param name="Outcomes">The admitted tool calls of the run, in admission order; empty when there were none.</param>
/// <param name="ProviderDispatches">
/// Model-provider requests that carried approvals of tool calls the provider runs itself, in order; empty when there
/// were none.
/// </param>
internal sealed record AgentCancellationReconciliationApiResponse(
    Guid ExecutionRunId,
    Guid ChatSessionId,
    bool HasUnknownEffects,
    IReadOnlyList<AgentToolCancellationOutcomeApiResponse> Outcomes,
    IReadOnlyList<AgentProviderDispatchOutcomeApiResponse> ProviderDispatches);

/// <summary>
/// State of one model-provider request that carried approvals of tool calls the provider runs itself.
/// </summary>
/// <param name="DispatchId">Identifier of the provider request within the run.</param>
/// <param name="State">
/// State of the request, as a JSON integer: 0 Started (sent; no response was recorded), 1 ResponseAdmitted (the
/// provider's response was recorded), 2 CancelledUnreconciled (cancelled after sending; the provider may have run the
/// approved calls).
/// </param>
internal sealed record AgentProviderDispatchOutcomeApiResponse(Guid DispatchId, AgentToolProviderDispatchState State);

/// <summary>
/// One message of an agent chat session transcript.
/// </summary>
/// <param name="Id">Identifier of the message.</param>
/// <param name="Role">
/// Author of the message, as a JSON integer: 0 System (written by the product, for example the record of the tool
/// calls made during a run), 1 User (a prompt), 2 Assistant (the agent's reply).
/// </param>
/// <param name="Content">Text of the message.</param>
/// <param name="CreatedAtUtc">When the message was added, in UTC.</param>
internal sealed record AgentChatMessageApiResponse(
    Guid Id,
    ChatMessageRole Role,
    string Content,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// Summary of an agent chat session for session lists.
/// </summary>
/// <param name="Id">Identifier of the chat session.</param>
/// <param name="AgentId">Identifier of the agent that owns the session.</param>
/// <param name="Title">Title of the session.</param>
/// <param name="CreatedAtUtc">When the session was created, in UTC.</param>
/// <param name="UpdatedAtUtc">When the session last changed, in UTC.</param>
/// <param name="MessageCount">Number of messages in the transcript, including messages written by the product.</param>
/// <param name="LastMessagePreview">
/// Text of the last message, cut to 177 characters followed by <c>...</c> when longer than 180 characters, or
/// <c>No messages yet.</c> for a session without messages.
/// </param>
/// <param name="PendingApprovalCount">Number of tool approvals waiting on the session's latest execution run.</param>
/// <param name="AutoApprovePendingToolCalls">
/// True when the session's latest execution run approves its tool calls automatically.
/// </param>
internal sealed record AgentChatSessionSummaryApiResponse(
    Guid Id,
    Guid AgentId,
    string Title,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int MessageCount,
    string LastMessagePreview,
    int PendingApprovalCount,
    bool AutoApprovePendingToolCalls);

/// <summary>
/// An agent chat session with its full transcript. The session is the conversation; each message sent in it starts
/// a separate agent execution run, listed by <c>GET /api/agents/execution-runs?chatSessionId={chatSessionId}</c>.
/// </summary>
/// <param name="Id">Identifier of the chat session.</param>
/// <param name="AgentId">Identifier of the agent that owns the session.</param>
/// <param name="Title">
/// Title of the session: <c>New exploration thread</c> for a session created empty, the first prompt (cut to 45
/// characters followed by <c>...</c> when longer than 48 characters) for a session created by a message, or the title
/// set by a rename.
/// </param>
/// <param name="CreatedAtUtc">When the session was created, in UTC.</param>
/// <param name="UpdatedAtUtc">When the session last changed, in UTC.</param>
/// <param name="Messages">The transcript, oldest message first.</param>
/// <param name="LatestExecutionRunId">
/// Identifier of the session's most recent execution run, or null when no run has been started in it.
/// </param>
/// <param name="PendingApprovals">
/// Normally empty: waiting tool approvals are reported on the execution run (its <c>pendingApprovals</c>). Only
/// sessions stored by earlier product versions can still list approvals here.
/// </param>
/// <param name="AutoApprovePendingToolCalls">
/// Normally false: automatic approval is reported on the execution run. Only sessions stored by earlier product
/// versions can report true here.
/// </param>
internal sealed record AgentChatSessionApiResponse(
    Guid Id,
    Guid AgentId,
    string Title,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<AgentChatMessageApiResponse> Messages,
    Guid? LatestExecutionRunId,
    IReadOnlyList<AgentPendingApprovalApiResponse> PendingApprovals,
    bool AutoApprovePendingToolCalls);

/// <summary>
/// Summary of the latest agent execution run shown in an agent's chat workspace.
/// </summary>
/// <param name="ExecutionRunId">Identifier of the execution run.</param>
/// <param name="AgentId">Identifier of the agent that ran.</param>
/// <param name="ChatSessionId">Identifier of the run's chat session, or null for a run without one.</param>
/// <param name="Title">
/// Title of the run with line breaks replaced by spaces, cut to 117 characters followed by <c>...</c> when longer
/// than 120 characters.
/// </param>
/// <param name="State">
/// State from the run's latest log entry (or the run's own state when it has no log), as a JSON integer: 0 Idle,
/// 1 Preparing, 2 Running, 3 WaitingOnTool, 4 Persisting, 5 Completed, 6 Failed.
/// </param>
/// <param name="Phase">
/// Phase label of the latest log entry, for example <c>Completed</c>; free text for display.
/// </param>
/// <param name="Message">
/// Message of the latest log entry, or the run's result summary or title when it has no log; free text for display.
/// </param>
/// <param name="Outcome">
/// Final outcome, as a JSON integer, or null while the run is not finished: 0 Succeeded, 1 Failed, 2 Cancelled.
/// </param>
/// <param name="CreatedAtUtc">When the run was created, in UTC.</param>
/// <param name="UpdatedAtUtc">When the run last changed, in UTC.</param>
/// <param name="StartedAtUtc">When the run started, in UTC, or null.</param>
/// <param name="CompletedAtUtc">When the run finished, in UTC, or null while it is not finished.</param>
/// <param name="Duration">
/// Wall-clock time from the start to the finish (or to the last change of an unfinished run), as a time span string
/// in the form <c>[d.]hh:mm:ss[.fffffff]</c>; null when the start time is unknown.
/// </param>
/// <param name="KnownCostUsd">
/// Estimated cost of the run in US dollars: the sum of the costs recorded with the run's provider usage or, when none
/// has a cost, of its positive per-segment metric costs; 0 when no cost is known.
/// </param>
/// <param name="HasUnknownCost">
/// True when part of the run's usage has no cost or no cost is known at all; <c>knownCostUsd</c> is then a lower bound.
/// </param>
internal sealed record AgentChatRunSummaryApiResponse(
    Guid ExecutionRunId,
    Guid AgentId,
    Guid? ChatSessionId,
    string Title,
    ExecutionState State,
    string Phase,
    string Message,
    RunOutcome? Outcome,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    TimeSpan? Duration,
    decimal KnownCostUsd,
    bool HasUnknownCost);

/// <summary>
/// Chat workspace of one agent, returned by <c>GET /api/agents/{agentId}/chat-workspace</c> and inside
/// <c>GET /api/agents/bootstrap</c>.
/// </summary>
/// <param name="AgentId">Identifier of the agent.</param>
/// <param name="Sessions">Summaries of the agent's chat sessions, most recently updated first; may be empty.</param>
/// <param name="SelectedSession">
/// The selected session with its transcript, or null when the agent has no sessions.
/// </param>
/// <param name="SelectedSessionId">Identifier of the selected session, or null when none is selected.</param>
/// <param name="LatestRun">
/// Summary of the most recently updated execution run of the selected session (of the whole agent when no session is
/// selected), or null when there is none.
/// </param>
/// <param name="SelectedRun">
/// The selected session's latest execution run with its pending approvals, or null when no session is selected or
/// the session has no run.
/// </param>
internal sealed record AgentChatWorkspaceApiResponse(
    Guid AgentId,
    IReadOnlyList<AgentChatSessionSummaryApiResponse> Sessions,
    AgentChatSessionApiResponse? SelectedSession,
    Guid? SelectedSessionId,
    AgentChatRunSummaryApiResponse? LatestRun,
    AgentExecutionRunApiResponse? SelectedRun);

/// <summary>
/// Data for opening the agent chat page, returned by <c>GET /api/agents/bootstrap</c>.
/// </summary>
/// <param name="Agents">
/// The listed agents as short catalog items; templates only when <c>includeTemplates</c> was true.
/// </param>
/// <param name="InitialAgentId">
/// The agent to select first: the one with the most recently updated chat session, otherwise the first listed agent;
/// null when there are no agents.
/// </param>
/// <param name="SelectedAgentWorkspace">Chat workspace of that agent, or null when there are no agents.</param>
internal sealed record AgentChatPageBootstrapApiResponse(
    IReadOnlyList<AgentCatalogItemApiResponse> Agents,
    Guid? InitialAgentId,
    AgentChatWorkspaceApiResponse? SelectedAgentWorkspace);

/// <summary>
/// An agent execution run: one invocation of an agent for one prompt, started by a chat message or by
/// <c>POST /api/agents/execution-runs</c>. It is distinct from the chat session that may contain it and from any
/// workflow or process run that started it.
/// </summary>
/// <param name="Id">Identifier of the execution run.</param>
/// <param name="AgentId">Identifier of the agent that runs.</param>
/// <param name="ChatSessionId">
/// Identifier of the chat session the run belongs to, or null for a run without one.
/// </param>
/// <param name="Title">
/// Title of the run: the chat session title for chat-backed runs, otherwise the prompt cut to 45 characters followed
/// by <c>...</c> when longer than 48 characters.
/// </param>
/// <param name="ProviderName">Name of the provider profile the run uses.</param>
/// <param name="Model">Model identifier taken from the agent when the run started; may be empty.</param>
/// <param name="State">
/// Current state, as a JSON integer: 0 Idle, 1 Preparing, 2 Running, 3 WaitingOnTool (stopped until the pending
/// approvals are decided), 4 Persisting, 5 Completed, 6 Failed (also for cancelled runs, whose outcome is 2 Cancelled).
/// </param>
/// <param name="Outcome">
/// Final outcome, as a JSON integer, or null while the run is active or waiting: 0 Succeeded, 1 Failed, 2 Cancelled.
/// </param>
/// <param name="CreatedAtUtc">When the run was created, in UTC.</param>
/// <param name="UpdatedAtUtc">When the run last changed, in UTC.</param>
/// <param name="StartedAtUtc">When the run started, in UTC, or null.</param>
/// <param name="CompletedAtUtc">When the run finished, in UTC, or null while it is active or waiting.</param>
/// <param name="PendingApprovals">
/// Tool calls of the run waiting for an approval decision; empty when none waits.
/// </param>
/// <param name="PendingApprovalCount">Number of entries in <c>pendingApprovals</c>.</param>
/// <param name="AutoApprovePendingToolCalls">
/// True when later approval requests of the run are approved automatically.
/// </param>
/// <param name="Revision">
/// Change counter of the run: 1 when created and increased with each stored change. Compare values to detect that the
/// run changed between reads; no request accepts it as a precondition.
/// </param>
internal sealed record AgentExecutionRunApiResponse(
    Guid Id,
    Guid AgentId,
    Guid? ChatSessionId,
    string Title,
    string ProviderName,
    string Model,
    ExecutionState State,
    RunOutcome? Outcome,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    IReadOnlyList<AgentPendingApprovalApiResponse> PendingApprovals,
    int PendingApprovalCount,
    bool AutoApprovePendingToolCalls,
    long Revision);

/// <summary>
/// An approval request of an agent execution run and its decision, from the run's approval history.
/// </summary>
/// <param name="ApprovalId">Opaque identifier of the approval request, issued by the model runtime.</param>
/// <param name="ExecutionRunId">Identifier of the execution run.</param>
/// <param name="ToolName">Name of the tool the agent wanted to call.</param>
/// <param name="ToolKind">Kind of the tool call: <c>mcp</c>, <c>function</c> or <c>tool</c>.</param>
/// <param name="Status">
/// Decision status, as a JSON integer: 0 Pending, 1 Approved, 2 Rejected (including calls rejected because the run
/// was cancelled before they started).
/// </param>
/// <param name="RequestedAtUtc">When the approval was requested, in UTC.</param>
/// <param name="DecidedAtUtc">When it was decided, in UTC, or null while pending.</param>
/// <param name="DecisionSourceKind">
/// Where the decision came from: <c>chat-session</c> or <c>execution-run</c> (a decision sent for a chat-backed or
/// another run), <c>auto-approval</c> (approved automatically), <c>execution-cancellation</c> (rejected by the run's
/// cancellation), or an empty string while pending.
/// </param>
internal sealed record AgentExecutionApprovalApiResponse(
    string ApprovalId,
    Guid ExecutionRunId,
    string ToolName,
    string ToolKind,
    ExecutionApprovalStatus Status,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? DecidedAtUtc,
    string DecisionSourceKind);

/// <summary>
/// One progress entry of an agent execution run log.
/// </summary>
/// <param name="Id">Identifier of the entry.</param>
/// <param name="CreatedAtUtc">When the entry was written, in UTC.</param>
/// <param name="State">
/// Run state reported by the entry, as a JSON integer: 0 Idle (the entry does not change the run state), 1 Preparing,
/// 2 Running, 3 WaitingOnTool, 4 Persisting, 5 Completed, 6 Failed.
/// </param>
/// <param name="Phase">Short phase label, for example <c>Approval</c>; free text for display.</param>
/// <param name="Message">Human-readable progress message; free text for display.</param>
internal sealed record AgentExecutionLogApiResponse(
    Guid Id,
    DateTimeOffset CreatedAtUtc,
    ExecutionState State,
    string Phase,
    string Message);

/// <summary>
/// Usage and duration of one segment of an agent execution run: the first turn and each continuation after an
/// approval decision record one metric each.
/// </summary>
/// <param name="Id">Identifier of the metric.</param>
/// <param name="CreatedAtUtc">When the segment ended, in UTC.</param>
/// <param name="Outcome">
/// Outcome of the segment, as a JSON integer: 0 Succeeded, 1 Failed, 2 Cancelled. A segment that stopped to wait for
/// tool approvals also records 2 Cancelled.
/// </param>
/// <param name="ProviderName">Name of the provider profile used by the segment.</param>
/// <param name="Model">Model identifier used by the segment.</param>
/// <param name="DurationMs">Duration of the segment in milliseconds; at least 1.</param>
/// <param name="InputTokens">Input tokens reported by the provider for the segment; 0 when none were reported.</param>
/// <param name="CachedInputTokens">
/// Part of <c>inputTokens</c> served from the provider's prompt cache; 0 when not reported.
/// </param>
/// <param name="CacheWriteTokens">Not recorded for agent execution runs: always 0.</param>
/// <param name="OutputTokens">
/// Output tokens reported by the provider for the segment; 0 when none were reported.
/// </param>
/// <param name="ToolCalls">Number of tool calls made in the segment.</param>
/// <param name="CostUsd">
/// Estimated cost of the segment in US dollars, calculated from the provider profile's configured token prices; 0
/// when the model has no configured price.
/// </param>
internal sealed record AgentRunMetricApiResponse(
    Guid Id,
    DateTimeOffset CreatedAtUtc,
    RunOutcome Outcome,
    string ProviderName,
    string Model,
    long DurationMs,
    int InputTokens,
    int CachedInputTokens,
    int CacheWriteTokens,
    int OutputTokens,
    int ToolCalls,
    decimal CostUsd);

/// <summary>
/// Execution log entries and segment metrics of an agent's execution runs, returned by
/// <c>GET /api/agents/{agentId}/runtime-snapshot</c>.
/// </summary>
/// <param name="ExecutionLog">Log entries of the included runs, newest first.</param>
/// <param name="Metrics">Segment metrics of the included runs, newest first.</param>
internal sealed record AgentChatRuntimeApiResponse(
    IReadOnlyList<AgentExecutionLogApiResponse> ExecutionLog,
    IReadOnlyList<AgentRunMetricApiResponse> Metrics);

/// <summary>
/// A file that a tool produced or touched during an agent execution run, recorded from the tool's receipt.
/// </summary>
/// <param name="Id">Identifier of the artifact record.</param>
/// <param name="ArtifactKind">
/// Kind of artifact, for example <c>generated-output</c> (output written or captured by a tool),
/// <c>converted-document</c> or <c>workspace-file</c>.
/// </param>
/// <param name="DisplayName">Display name, usually the file name.</param>
/// <param name="RelativePath">
/// Path of the file as recorded by the tool, with forward slashes and no leading slash.
/// </param>
/// <param name="ContentType">Media type recorded for the file, for example <c>text/plain</c>.</param>
/// <param name="ProducedBy">Name of the tool operation that produced or touched the file.</param>
/// <param name="Summary">Short description of the artifact.</param>
/// <param name="CreatedAtUtc">When the producing tool call completed, in UTC.</param>
internal sealed record AgentExecutionArtifactApiResponse(
    Guid Id,
    string ArtifactKind,
    string DisplayName,
    string RelativePath,
    string ContentType,
    string ProducedBy,
    string Summary,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// A checkpoint of an agent execution run, saved when the run stopped to wait for tool approvals.
/// </summary>
/// <param name="Id">Identifier of the checkpoint.</param>
/// <param name="CheckpointKind">Kind of checkpoint; currently always <c>approval-wait</c>.</param>
/// <param name="RunState">
/// Run state when the checkpoint was saved, as a JSON integer: 0 Idle, 1 Preparing, 2 Running, 3 WaitingOnTool,
/// 4 Persisting, 5 Completed, 6 Failed.
/// </param>
/// <param name="PendingApprovalCount">Number of approvals the run was waiting for.</param>
/// <param name="CapturedAtUtc">When the checkpoint was saved, in UTC.</param>
/// <param name="ResumedAtUtc">When the run resumed from it, in UTC, or null when it has not resumed.</param>
internal sealed record AgentExecutionCheckpointApiResponse(
    Guid Id,
    string CheckpointKind,
    ExecutionState RunState,
    int PendingApprovalCount,
    DateTimeOffset CapturedAtUtc,
    DateTimeOffset? ResumedAtUtc);

/// <summary>
/// Audit receipt of one tool call made during an agent execution run.
/// </summary>
/// <param name="Id">Identifier of the receipt.</param>
/// <param name="ExecutionRunId">Identifier of the execution run.</param>
/// <param name="ToolFamily">
/// Family of the tool, for example <c>runtime-provider</c> (a first-party runtime tool), <c>workspace-file</c>,
/// <c>workspace-process</c>, <c>mcp-server</c> or <c>agent-tool-trace</c> (a traced call without its own receipt).
/// </param>
/// <param name="ToolName">Name of the tool or tool operation.</param>
/// <param name="RiskClass">
/// Risk label recorded for the call, for example <c>RuntimeProvider:Mutation</c> or <c>ReadOnlyWorkspace</c>; free
/// text.
/// </param>
/// <param name="ApprovalMode">
/// How approval applied to the call, for example <c>PolicyEnforced</c> or <c>NotRequired</c>; free text.
/// </param>
/// <param name="IsolationGuarantee">Description of the isolation boundary the call ran in; free text.</param>
/// <param name="StartedAtUtc">When the call started, in UTC.</param>
/// <param name="CompletedAtUtc">When the call completed, in UTC.</param>
/// <param name="RuntimeToolProviderName">
/// Name of the runtime tool provider that owns the tool, or an empty string when there is none.
/// </param>
/// <param name="DeclaredSideEffectMode">
/// Side effect the tool declares, as a JSON integer: 0 Unspecified, 1 NoMutation, 2 ManagedProcessArtifacts (writes
/// managed workspace or process files), 3 ExternalArtifactDestination (acts outside the product, for example external
/// actions or media generation), 4 ProductMutation (changes product data such as processes or project structure).
/// </param>
/// <param name="InvocationOutcome">
/// Outcome of the call, as a JSON integer: 0 Unknown (not recorded, as for workspace and MCP receipts), 1 Succeeded,
/// 2 Failed, 3 Cancelled.
/// </param>
/// <param name="EffectState">
/// What is known about the call's effect, as a JSON integer: 0 Unknown (not confirmed, or not recorded as for
/// workspace and MCP receipts), 1 None, 2 NotCommitted, 3 Committed.
/// </param>
/// <param name="FailureCode">Stable failure code reported by the tool, or an empty string.</param>
/// <param name="FailureMessage">Failure message reported by the tool, or an empty string.</param>
/// <param name="CanRetryWithCorrectedInput">
/// True when the tool reported that the call may be retried with corrected input.
/// </param>
/// <param name="EffectSourceKind">Kind of the owner record changed by the call, or an empty string.</param>
/// <param name="EffectSourceId">Identifier of the owner record changed by the call, or an empty string.</param>
internal sealed record AgentExecutionToolReceiptApiResponse(
    Guid Id,
    Guid ExecutionRunId,
    string ToolFamily,
    string ToolName,
    string RiskClass,
    string ApprovalMode,
    string IsolationGuarantee,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string RuntimeToolProviderName,
    ToolExecutionSideEffectMode DeclaredSideEffectMode,
    AgentToolInvocationOutcome InvocationOutcome,
    AgentToolEffectState EffectState,
    string FailureCode,
    string FailureMessage,
    bool CanRetryWithCorrectedInput,
    string EffectSourceKind,
    string EffectSourceId);

/// <summary>
/// Totals over the model-provider usage observations recorded for an agent execution run.
/// </summary>
/// <param name="ObservationCount">Number of usage observations of the run.</param>
/// <param name="KnownObservationCount">
/// Observations whose token usage is known (reported by the provider or taken from the segment metric); the token
/// sums below cover only these.
/// </param>
/// <param name="UnknownObservationCount">Observations without known token usage.</param>
/// <param name="InputTokens">Sum of input tokens over the known observations.</param>
/// <param name="CachedInputTokens">Sum of cached input tokens over the known observations.</param>
/// <param name="CacheWriteTokens">
/// Sum of prompt-cache write tokens over the known observations; 0 when providers do not report them.
/// </param>
/// <param name="OutputTokens">Sum of output tokens over the known observations.</param>
/// <param name="ReasoningTokens">Sum of reasoning tokens over the known observations.</param>
/// <param name="TotalTokens">Sum of total tokens over the known observations.</param>
/// <param name="ToolCallCount">Sum of tool calls over the known observations.</param>
/// <param name="KnownCostObservationCount">
/// Observations with a provider-reported or calculated cost, including costs estimated from the segment metric.
/// </param>
/// <param name="UnknownCostObservationCount">Observations without a cost.</param>
/// <param name="KnownCostUsd">
/// Sum of the known costs in US dollars, rounded to 6 decimal places; a lower bound when
/// <c>unknownCostObservationCount</c> is not 0.
/// </param>
internal sealed record AgentProviderUsageTotalsApiResponse(
    int ObservationCount,
    int KnownObservationCount,
    int UnknownObservationCount,
    int InputTokens,
    int CachedInputTokens,
    int CacheWriteTokens,
    int OutputTokens,
    int ReasoningTokens,
    int TotalTokens,
    int ToolCallCount,
    int KnownCostObservationCount,
    int UnknownCostObservationCount,
    decimal KnownCostUsd);

/// <summary>
/// Full record of an agent execution run, returned by <c>GET /api/agents/execution-runs/{executionRunId}</c> and
/// <c>GET /api/agents/{agentId}/execution-runs/{executionRunId}</c>.
/// </summary>
/// <param name="Run">The execution run.</param>
/// <param name="ChatSession">The run's chat session with its transcript, or null for a run without one.</param>
/// <param name="ExecutionLog">Progress entries of the run.</param>
/// <param name="Metrics">Segment metrics of the run.</param>
/// <param name="Approvals">Approval history of the run, including decided approvals.</param>
/// <param name="Artifacts">Files produced or touched by the run's tool calls.</param>
/// <param name="Checkpoints">Checkpoints saved while the run waited for approvals.</param>
/// <param name="ToolReceipts">Audit receipts of the run's tool calls.</param>
/// <param name="UsageTotals">Totals of the run's model-provider usage.</param>
internal sealed record AgentExecutionRunDetailApiResponse(
    AgentExecutionRunApiResponse Run,
    AgentChatSessionApiResponse? ChatSession,
    IReadOnlyList<AgentExecutionLogApiResponse> ExecutionLog,
    IReadOnlyList<AgentRunMetricApiResponse> Metrics,
    IReadOnlyList<AgentExecutionApprovalApiResponse> Approvals,
    IReadOnlyList<AgentExecutionArtifactApiResponse> Artifacts,
    IReadOnlyList<AgentExecutionCheckpointApiResponse> Checkpoints,
    IReadOnlyList<AgentExecutionToolReceiptApiResponse> ToolReceipts,
    AgentProviderUsageTotalsApiResponse UsageTotals);

/// <summary>
/// Result of a chat message sent with <c>POST /api/agents/{agentId}/chat</c>. The streaming variant sends the same
/// object as <c>result</c> of its <c>agent.command.completed</c> event, with enum values written as camel-case strings
/// (for example <c>waitingOnTool</c>) instead of integers.
/// </summary>
/// <param name="ChatSessionId">Identifier of the chat session, including a session created for this message.</param>
/// <param name="AssistantMessage">
/// The agent's reply message. While the run waits for approvals it holds only the text produced before the approval
/// request, which can be empty.
/// </param>
/// <param name="Metric">Usage and duration of this run segment.</param>
/// <param name="ExecutionRunId">Identifier of the execution run started by the message.</param>
/// <param name="State">
/// State of the run when the request returned, as a JSON integer: 5 Completed, 3 WaitingOnTool (decide the pending
/// approvals with <c>POST /api/agents/execution-runs/{executionRunId}/pending-approvals</c>) or 6 Failed (a changing
/// tool call failed without a later successful attempt).
/// </param>
internal sealed record AgentChatRunApiResponse(
    Guid ChatSessionId,
    AgentChatMessageApiResponse AssistantMessage,
    AgentRunMetricApiResponse Metric,
    Guid ExecutionRunId,
    ExecutionState State);

/// <summary>
/// Result of starting, continuing or recovering an agent execution run. The request returns when the run completes,
/// ends in a failure recorded on the run, or stops to wait for tool approvals. The streaming variants send the same
/// object as <c>result</c> of their <c>agent.command.completed</c> event, with enum values written as camel-case
/// strings (for example <c>waitingOnTool</c> or <c>schemaValidationFailed</c>) instead of the forms described here.
/// </summary>
/// <param name="ExecutionRunId">Identifier of the execution run.</param>
/// <param name="ChatSessionId">Identifier of the run's chat session, or null for a run without one.</param>
/// <param name="ResponseText">
/// Final text produced by the model in this segment; may be empty, for example while the run waits for approvals.
/// </param>
/// <param name="AssistantMessage">
/// The reply added to the chat transcript of a chat-backed run; null for a run without a chat session.
/// </param>
/// <param name="Metric">Usage and duration of this run segment.</param>
/// <param name="State">
/// State of the run when the request returned, as a JSON integer: 5 Completed, 3 WaitingOnTool (decide the pending
/// approvals with <c>POST /api/agents/execution-runs/{executionRunId}/pending-approvals</c>) or 6 Failed (the answer
/// did not satisfy the requested JSON Schema, or a changing tool call failed without a later successful attempt).
/// </param>
/// <param name="StructuredOutput">
/// Validation result of the requested JSON Schema output, or null when the run requested none or is waiting for
/// approvals.
/// </param>
internal sealed record AgentExecutionRunResultApiResponse(
    Guid ExecutionRunId,
    Guid? ChatSessionId,
    string ResponseText,
    AgentChatMessageApiResponse? AssistantMessage,
    AgentRunMetricApiResponse Metric,
    ExecutionState State,
    AgentStructuredOutputApiResponse? StructuredOutput);

/// <summary>
/// One reason why the model's answer did not satisfy the requested JSON Schema.
/// </summary>
/// <param name="Code">
/// Stable error code, for example <c>malformed-json</c>, <c>provider-refusal</c>, <c>output-too-large</c>,
/// <c>type-mismatch</c> or <c>required-property-missing</c>; trimmed and cut to 128 characters.
/// </param>
/// <param name="Message">Human-readable description; trimmed and cut to 1024 characters.</param>
/// <param name="Path">
/// JSON path of the failing value in the answer, starting with <c>$</c>; trimmed and cut to 512 characters.
/// </param>
internal sealed record AgentStructuredOutputValidationErrorApiResponse(
    string Code,
    string Message,
    string Path);

/// <summary>
/// Validation result of the JSON Schema output requested for an agent execution run.
/// </summary>
/// <param name="Data">
/// The model's answer parsed as JSON, present even when it failed schema validation; null when the answer was not one
/// complete JSON value, was larger than 1 MiB or was a text refusal.
/// </param>
/// <param name="ValidationStatus">
/// Validation status, as a string: <c>Valid</c>, <c>ProviderRefusal</c> (the model refused), <c>MalformedJson</c> (not
/// one complete JSON value, or larger than 1 MiB) or <c>SchemaValidationFailed</c>. Any status other than
/// <c>Valid</c> ends the run in state 6 Failed.
/// </param>
/// <param name="ValidationErrors">Reasons for a status other than <c>Valid</c>, at most 20; empty when valid.</param>
internal sealed record AgentStructuredOutputApiResponse(
    JsonElement? Data,
    AgentJsonSchemaOutputValidationStatus ValidationStatus,
    IReadOnlyList<AgentStructuredOutputValidationErrorApiResponse> ValidationErrors);

internal static class AgentApiResponseMapper
{
    public static AgentCancellationReconciliationApiResponse ToCancellationReconciliation(AgentToolRunCancellationReconciliation source) {
        ArgumentNullException.ThrowIfNull(source);
        return new(source.ExecutionRunId, source.ChatSessionId, source.HasUnknownEffects,
            source.Outcomes.Select(outcome => new AgentToolCancellationOutcomeApiResponse(
                outcome.IntentId.Value, outcome.ToolName, outcome.EffectState,
                outcome.Cancellation?.Disposition, outcome.Cancellation?.Reason,
                outcome.Cancellation?.CommittedEffect is { } effect ? new(effect.SourceKind, effect.SourceId) : null)).ToArray(),
            (source.ProviderDispatches ?? []).Select(dispatch => new AgentProviderDispatchOutcomeApiResponse(
                dispatch.Id.Value, dispatch.State)).ToArray());
    }

    public static AgentChatPageBootstrapApiResponse ToChatPageBootstrap(ChatPageBootstrapSnapshot source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new AgentChatPageBootstrapApiResponse(
            source.Agents.Select(ToAgentCatalogItem).ToArray(),
            source.InitialAgentId,
            source.SelectedAgentWorkspace is null
                ? null
                : ToChatWorkspace(source.SelectedAgentWorkspace));
    }

    public static AgentChatWorkspaceApiResponse ToChatWorkspace(ChatAgentWorkspaceSnapshot source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new AgentChatWorkspaceApiResponse(
            source.AgentId,
            source.Sessions.Select(ToChatSessionSummary).ToArray(),
            source.SelectedSession is null
                ? null
                : ToChatSession(source.SelectedSession),
            source.SelectedSessionId,
            source.LatestRun is null
                ? null
                : ToChatRunSummary(source.LatestRun),
            source.SelectedRun is null
                ? null
                : ToExecutionRun(source.SelectedRun));
    }

    public static AgentChatSessionApiResponse ToChatSession(ChatSessionRecord source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var compatibility = source.Compatibility;
        return new AgentChatSessionApiResponse(
            source.Id,
            source.AgentId,
            source.Title,
            source.CreatedAtUtc,
            source.UpdatedAtUtc,
            source.Messages.Select(ToChatMessage).ToArray(),
            source.LatestExecutionRunId,
            MapPendingApprovals(compatibility?.PendingApprovals ?? []),
            compatibility?.AutoApprovePendingToolCalls ?? false);
    }

    public static IReadOnlyList<AgentChatSessionSummaryApiResponse> ToChatSessionSummaries(
        IReadOnlyList<ChatSessionSummaryRecord> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Select(ToChatSessionSummary).ToArray();
    }

    public static IReadOnlyList<AgentChatSessionApiResponse> ToChatSessions(
        IReadOnlyList<ChatSessionRecord> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Select(ToChatSession).ToArray();
    }

    public static IReadOnlyList<AgentExecutionRunApiResponse> ToExecutionRuns(
        IReadOnlyList<ExecutionRunRecord> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Select(ToExecutionRun).ToArray();
    }

    public static AgentExecutionRunApiResponse ToExecutionRun(ExecutionRunRecord source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var pendingApprovals = MapPendingApprovals(source.PendingApprovals);
        return new AgentExecutionRunApiResponse(
            source.Id,
            source.AgentId,
            source.ChatSessionId,
            source.Title,
            source.ProviderName,
            source.Model,
            source.State,
            source.Outcome,
            source.CreatedAtUtc,
            source.UpdatedAtUtc,
            source.StartedAtUtc,
            source.CompletedAtUtc,
            pendingApprovals,
            pendingApprovals.Count,
            source.AutoApprovePendingToolCalls,
            source.Revision);
    }

    public static AgentExecutionRunDetailApiResponse ToExecutionRunDetail(ExecutionRunDetail source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new AgentExecutionRunDetailApiResponse(
            ToExecutionRun(source.Run),
            source.ChatSession is null
                ? null
                : ToChatSession(source.ChatSession),
            source.ExecutionLog.Select(ToExecutionLog).ToArray(),
            source.Metrics.Select(ToMetric).ToArray(),
            ToExecutionApprovals(source.Approvals),
            source.Artifacts.Select(ToArtifact).ToArray(),
            source.Checkpoints.Select(ToCheckpoint).ToArray(),
            ToToolReceipts(source.ToolReceipts),
            ToProviderUsageTotals(source.UsageObservations));
    }

    public static AgentChatRunApiResponse ToChatRunResult(AgentChatRunResult source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new AgentChatRunApiResponse(
            source.ChatSessionId,
            ToChatMessage(source.AssistantMessage),
            ToMetric(source.Metric),
            source.ExecutionRunId,
            source.State);
    }

    public static AgentExecutionRunResultApiResponse ToExecutionRunResult(ExecutionRunResult source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new AgentExecutionRunResultApiResponse(
            source.ExecutionRunId,
            source.ChatSessionId,
            source.ResponseText,
            source.AssistantMessage is null
                ? null
                : ToChatMessage(source.AssistantMessage),
            ToMetric(source.Metric),
            source.State,
            source.StructuredOutput is null
                ? null
                : ToStructuredOutput(source.StructuredOutput));
    }

    public static IReadOnlyList<AgentExecutionApprovalApiResponse> ToExecutionApprovals(
        IReadOnlyList<ExecutionApprovalRecord> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Select(static approval => new AgentExecutionApprovalApiResponse(
            approval.ApprovalId,
            approval.ExecutionRunId,
            approval.ToolName,
            approval.ToolKind,
            approval.Status,
            approval.RequestedAtUtc,
            approval.DecidedAtUtc,
            approval.DecisionSourceKind)).ToArray();
    }

    public static IReadOnlyList<AgentExecutionToolReceiptApiResponse> ToToolReceipts(
        IReadOnlyList<ToolExecutionReceiptRecord> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Select(static receipt => new AgentExecutionToolReceiptApiResponse(
            receipt.Id,
            receipt.ExecutionRunId,
            receipt.ToolFamily,
            receipt.ToolName,
            receipt.RiskClass,
            receipt.ApprovalMode,
            receipt.IsolationGuarantee,
            receipt.StartedAtUtc,
            receipt.CompletedAtUtc,
            receipt.RuntimeToolProviderName,
            receipt.DeclaredSideEffectMode,
            receipt.InvocationOutcome,
            receipt.EffectState,
            receipt.FailureCode,
            receipt.FailureMessage,
            receipt.CanRetryWithCorrectedInput,
            receipt.EffectSourceKind,
            receipt.EffectSourceId)).ToArray();
    }

    public static IReadOnlyList<AgentExecutionLogApiResponse> ToExecutionLog(
        IReadOnlyList<ExecutionLogEntry> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Select(ToExecutionLog).ToArray();
    }

    public static IReadOnlyList<AgentRunMetricApiResponse> ToMetrics(
        IReadOnlyList<AgentRunMetric> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Select(ToMetric).ToArray();
    }

    public static AgentChatRuntimeApiResponse ToChatRuntime(ChatRuntimeSnapshot source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new AgentChatRuntimeApiResponse(
            ToExecutionLog(source.ExecutionLog),
            ToMetrics(source.Metrics));
    }

    public static IReadOnlyList<AgentExecutionArtifactApiResponse> ToArtifacts(
        IReadOnlyList<ExecutionArtifactRecord> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Select(ToArtifact).ToArray();
    }

    public static IReadOnlyList<AgentExecutionCheckpointApiResponse> ToCheckpoints(
        IReadOnlyList<ExecutionWorkflowCheckpointRecord> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Select(ToCheckpoint).ToArray();
    }

    private static AgentCatalogItemApiResponse ToAgentCatalogItem(AgentDefinition source)
    {
        return new AgentCatalogItemApiResponse(
            source.Id,
            source.Name,
            source.RoleTitle,
            source.Summary,
            source.Status,
            source.Model,
            source.IsTemplate,
            source.AvatarImageUrl);
    }

    private static AgentChatMessageApiResponse ToChatMessage(ChatMessageRecord source)
    {
        return new AgentChatMessageApiResponse(
            source.Id,
            source.Role,
            source.Content,
            source.CreatedAtUtc);
    }

    private static AgentChatSessionSummaryApiResponse ToChatSessionSummary(ChatSessionSummaryRecord source)
    {
        return new AgentChatSessionSummaryApiResponse(
            source.Id,
            source.AgentId,
            source.Title,
            source.CreatedAtUtc,
            source.UpdatedAtUtc,
            source.MessageCount,
            source.LastMessagePreview,
            source.PendingApprovalCount,
            source.AutoApprovePendingToolCalls);
    }

    private static AgentChatRunSummaryApiResponse ToChatRunSummary(ChatRunSummaryRecord source)
    {
        return new AgentChatRunSummaryApiResponse(
            source.ExecutionRunId,
            source.AgentId,
            source.ChatSessionId,
            source.Title,
            source.State,
            source.Phase,
            source.Message,
            source.Outcome,
            source.CreatedAtUtc,
            source.UpdatedAtUtc,
            source.StartedAtUtc,
            source.CompletedAtUtc,
            source.Duration,
            source.KnownCostUsd,
            source.HasUnknownCost);
    }

    private static AgentExecutionLogApiResponse ToExecutionLog(ExecutionLogEntry source)
    {
        return new AgentExecutionLogApiResponse(
            source.Id,
            source.CreatedAtUtc,
            source.State,
            source.Phase,
            source.Message);
    }

    private static AgentRunMetricApiResponse ToMetric(AgentRunMetric source)
    {
        return new AgentRunMetricApiResponse(
            source.Id,
            source.CreatedAtUtc,
            source.Outcome,
            source.ProviderName,
            source.Model,
            source.DurationMs,
            source.InputTokens,
            source.CachedInputTokens,
            source.CacheWriteTokens,
            source.OutputTokens,
            source.ToolCalls,
            source.CostUsd);
    }

    private static AgentExecutionArtifactApiResponse ToArtifact(ExecutionArtifactRecord source)
    {
        return new AgentExecutionArtifactApiResponse(
            source.Id,
            source.ArtifactKind,
            source.DisplayName,
            source.RelativePath,
            source.ContentType,
            source.ProducedBy,
            source.Summary,
            source.CreatedAtUtc);
    }

    private static AgentExecutionCheckpointApiResponse ToCheckpoint(
        ExecutionWorkflowCheckpointRecord source)
    {
        return new AgentExecutionCheckpointApiResponse(
            source.Id,
            source.CheckpointKind,
            source.RunState,
            source.PendingApprovalIds.Count,
            source.CapturedAtUtc,
            source.ResumedAtUtc);
    }

    private static IReadOnlyList<AgentPendingApprovalApiResponse> MapPendingApprovals(
        IReadOnlyList<PendingToolApprovalRecord> source)
    {
        return source.Select(static approval => new AgentPendingApprovalApiResponse(
            approval.ApprovalId,
            approval.ToolName,
            approval.ToolKind)).ToArray();
    }

    private static AgentProviderUsageTotalsApiResponse ToProviderUsageTotals(
        IReadOnlyList<ProviderUsageObservation> source)
    {
        var knownUsage = source
            .Where(static observation => observation.UsageStatus is
                ProviderUsageObservationStatus.Observed or
                ProviderUsageObservationStatus.ObservedFromMetric)
            .ToArray();
        var knownCosts = source
            .Select(static observation => observation.ProviderCostUsd ?? observation.CalculatedCostUsd)
            .Where(static cost => cost.HasValue)
            .Select(static cost => cost.GetValueOrDefault())
            .ToArray();

        return new AgentProviderUsageTotalsApiResponse(
            source.Count,
            knownUsage.Length,
            source.Count - knownUsage.Length,
            knownUsage.Sum(static observation => observation.InputTokens),
            knownUsage.Sum(static observation => observation.CachedInputTokens),
            knownUsage.Sum(static observation => observation.CacheWriteTokens),
            knownUsage.Sum(static observation => observation.OutputTokens),
            knownUsage.Sum(static observation => observation.ReasoningTokens),
            knownUsage.Sum(static observation => observation.TotalTokens),
            knownUsage.Sum(static observation => observation.ToolCallCount),
            knownCosts.Length,
            source.Count - knownCosts.Length,
            decimal.Round(knownCosts.Sum(), 6, MidpointRounding.AwayFromZero));
    }

    private static AgentStructuredOutputApiResponse ToStructuredOutput(
        AgentJsonSchemaOutputResult source)
    {
        const int maximumValidationErrors = 20;
        return new AgentStructuredOutputApiResponse(
            source.Data,
            source.ValidationStatus,
            source.ValidationErrors
                .Take(maximumValidationErrors)
                .Select(static error => new AgentStructuredOutputValidationErrorApiResponse(
                    NormalizeBounded(error.Code, 128),
                    NormalizeBounded(error.Message, 1024),
                    NormalizeBounded(error.Path, 512)))
                .ToArray());
    }

    private static string NormalizeBounded(string value, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return normalized.Length <= maximumLength
            ? normalized
            : normalized[..maximumLength];
    }
}
