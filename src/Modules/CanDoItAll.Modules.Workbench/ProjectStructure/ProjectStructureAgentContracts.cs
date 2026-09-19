using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.SharedKernel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Modules.Workbench;

public static class ProjectStructureAgentHttpHeaders
{
    public const string AgentId = "X-CanDoItAll-Agent-Id";
    public const string AgentName = "X-CanDoItAll-Agent-Name";
    public const string MachineName = "X-CanDoItAll-Agent-Machine";
    public const string RepositoryRoot = "X-CanDoItAll-Agent-RepoRoot";
    public const string BranchName = "X-CanDoItAll-Agent-Branch";
    public const string SessionId = "X-CanDoItAll-Agent-Session";
    public const string AgentToken = "X-CanDoItAll-Agent-Token";
    public const string EstimatedMinutes = "X-CanDoItAll-Estimated-Minutes";
}

public sealed record ProjectStructureAgentContext(
    string AgentId,
    string AgentName,
    string MachineName,
    string RepositoryRoot,
    string BranchName,
    string SessionId) {
    [JsonIgnore]
    public ProjectStructureWorkflowAuthoritySource? WorkflowAuthority { get; init; }
    [JsonIgnore]
    public ProjectWriteAdmission? ExpectedProjectAdmission { get; init; }

    [JsonIgnore]
    public System.Collections.Immutable.ImmutableArray<ProjectWriteAdmission> ExpectedProjectAdmissions { get; init; } = [];

    [JsonIgnore]
    public ProjectProcessMutationAdmission? ProcessMutationAdmission { get; init; }

    [JsonIgnore]
    public ProjectAgentMutationAdmission? AgentMutationAdmission { get; init; }

    [JsonIgnore]
    public ProjectStructureProcessLaunchInvocation? ProcessLaunchInvocation { get; init; }

    [JsonIgnore]
    public ProjectProcessAssetInvocation? ProcessAssetInvocation { get; init; }

    [JsonIgnore]
    public WorkspaceScopeDescriptor? ActiveWorkspaceScope { get; init; }
}

/// <summary>
/// Kind of scope a Project Structure lease coordinates; it decides how the scope key is read. Request bodies accept a
/// JSON integer, a numeric string or a case-insensitive name; responses write the JSON integer. 0 Project (the key is a
/// project id; the only kind that Project Structure mutations check), 1 ProjectNode (the key is a node identifier that
/// exists in exactly one project), 2 RepoBranch (a free-form repository and branch key that is not tied to a project).
/// ProjectNode and RepoBranch leases only coordinate callers that look for them; no Project Structure operation
/// requires or checks them.
/// </summary>
[JsonConverter(typeof(FlexibleProjectStructureLeaseScopeKindJsonConverter))]
public enum ProjectStructureLeaseScopeKind
{
    Project,
    ProjectNode,
    RepoBranch
}

internal sealed class FlexibleProjectStructureLeaseScopeKindJsonConverter : JsonConverter<ProjectStructureLeaseScopeKind>
{
    public override ProjectStructureLeaseScopeKind Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number &&
            reader.TryGetInt32(out var numericValue) &&
            Enum.IsDefined(typeof(ProjectStructureLeaseScopeKind), numericValue))
        {
            return (ProjectStructureLeaseScopeKind)numericValue;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var rawValue = reader.GetString();
            if (int.TryParse(rawValue, out numericValue) &&
                Enum.IsDefined(typeof(ProjectStructureLeaseScopeKind), numericValue))
            {
                return (ProjectStructureLeaseScopeKind)numericValue;
            }

            if (Enum.TryParse<ProjectStructureLeaseScopeKind>(rawValue, ignoreCase: true, out var parsedValue) &&
                Enum.IsDefined(parsedValue))
            {
                return parsedValue;
            }
        }

        throw new JsonException("Invalid project-structure lease scope kind. Use Project, ProjectNode, RepoBranch, or the corresponding numeric value.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        ProjectStructureLeaseScopeKind value,
        JsonSerializerOptions options)
    {
        writer.WriteNumberValue((int)value);
    }
}

/// <summary>
/// Lease acquisition request: the scope to lease, a reason shown to other callers and the requested duration. The
/// lease belongs to the calling identity. When that identity already holds the active lease on the scope, the lease is
/// extended and keeps its token; when another identity holds it, the request is rejected with HTTP 409
/// <c>LeaseConflict</c>.
/// </summary>
/// <param name="ScopeKind">
/// Kind of scope to lease, as a JSON integer, a numeric string or a case-insensitive name: 0 Project, 1 ProjectNode,
/// 2 RepoBranch.
/// </param>
/// <param name="ScopeKey">
/// Key of the leased scope. Project: the id of an existing project in its hyphenated form, for example
/// <c>3f2504e0-4f89-11d3-9a0c-0305e82c3301</c>; any other GUID spelling is accepted but creates a lease that mutations
/// do not recognize. ProjectNode: a node identifier as returned in <c>nodes[].id</c> by the structure read.
/// RepoBranch: a free-form repository and branch key. The key is trimmed and lower-cased (RepoBranch keys also get
/// forward slashes instead of backslashes) and is stored with at most 300 characters. A blank key is rejected with HTTP
/// 400 <c>ScopeKeyRequired</c>, a Project key that is not a GUID with HTTP 400 <c>InvalidProjectScope</c>, an unknown
/// project with HTTP 404 <c>ProjectNotFound</c>.
/// </param>
/// <param name="Reason">
/// Free text shown to other callers in the lease snapshot, for example why the scope is locked. Trimmed; a blank reason
/// is stored as <c>Project structure mutation</c>.
/// </param>
/// <param name="DurationMinutes">
/// Requested lifetime in minutes, counted from now; values are clamped to 1 through 120. Defaults to 15 when omitted.
/// </param>
public sealed record ProjectStructureLeaseAcquireRequest(
    ProjectStructureLeaseScopeKind ScopeKind,
    string ScopeKey,
    string Reason,
    int DurationMinutes = 15);

/// <summary>
/// Lease renewal request: extends an active lease that the calling identity holds, identified by its scope and token.
/// </summary>
/// <param name="ScopeKind">
/// Kind of the leased scope, as used when the lease was acquired: a JSON integer, a numeric string or a
/// case-insensitive name, 0 Project, 1 ProjectNode or 2 RepoBranch.
/// </param>
/// <param name="ScopeKey">
/// Key of the leased scope, normalized like the acquisition key (trimmed and lower-cased), so send the key used to
/// acquire the lease or the <c>scopeKey</c> returned in its snapshot.
/// </param>
/// <param name="LeaseToken">
/// Token returned in <c>leaseToken</c> when the lease was acquired, compared exactly after trimming. A blank token is
/// rejected with HTTP 400 <c>LeaseTokenRequired</c>.
/// </param>
/// <param name="DurationMinutes">
/// New lifetime in minutes, counted from now (not added to the remaining time); clamped to 1 through 120. Defaults
/// to 15.
/// </param>
public sealed record ProjectStructureLeaseRenewRequest(
    ProjectStructureLeaseScopeKind ScopeKind,
    string ScopeKey,
    string LeaseToken,
    int DurationMinutes = 15);

/// <summary>
/// Lease release request: ends a lease that the calling identity holds, identified by its scope and token.
/// </summary>
/// <param name="ScopeKind">
/// Kind of the leased scope, as used when the lease was acquired: a JSON integer, a numeric string or a
/// case-insensitive name, 0 Project, 1 ProjectNode or 2 RepoBranch.
/// </param>
/// <param name="ScopeKey">
/// Key of the leased scope, normalized like the acquisition key (trimmed and lower-cased).
/// </param>
/// <param name="LeaseToken">
/// Token returned in <c>leaseToken</c> when the lease was acquired, compared exactly after trimming. A blank token is
/// rejected with HTTP 400 <c>LeaseTokenRequired</c>.
/// </param>
public sealed record ProjectStructureLeaseReleaseRequest(
    ProjectStructureLeaseScopeKind ScopeKind,
    string ScopeKey,
    string LeaseToken);

/// <summary>
/// State of one Project Structure lease: its scope, token, holder and validity window. Returned by the lease acquire,
/// renew, release and current-lease operations. A lease coordinates writers; it is not an authorization and not a
/// project write admission. The holder is the caller identity: the bearer token's subject, or the shared identity
/// <c>local-api-operator</c> when API authorization is disabled, together with the server's machine name. Another
/// identity cannot renew, release or use the lease even when it knows the token.
/// </summary>
/// <param name="ScopeKind">Kind of the leased scope, as a JSON integer (0 Project, 1 ProjectNode,
/// 2 RepoBranch).</param>
/// <param name="ScopeKey">Normalized (trimmed, lower-cased) key of the leased scope.</param>
/// <param name="LeaseToken">
/// Opaque lease token (currently 32 hexadecimal characters). Send it as <c>leaseToken</c> to renew or release the lease
/// and, for a Project lease, with the mutations of that project. Every snapshot includes it, including the one returned
/// by the current-lease read to any caller.
/// </param>
/// <param name="AgentId">
/// Identity that holds the lease: the bearer token's subject, <c>local-api-operator</c> for unauthenticated API calls,
/// or the agent id of an in-process agent.
/// </param>
/// <param name="AgentName">Display name of the holder, for example the token's name claim.</param>
/// <param name="MachineName">
/// Machine name recorded for the holder. For HTTP callers it is the name of the server that handled the request, not
/// the client's machine.
/// </param>
/// <param name="RepositoryRoot">
/// Repository root recorded by the holder; empty for HTTP callers, which cannot supply one.
/// </param>
/// <param name="BranchName">Branch name recorded by the holder; empty for HTTP callers.</param>
/// <param name="Reason">Reason given when the lease was acquired or last re-acquired.</param>
/// <param name="AcquiredAtUtc">Instant (UTC, with offset) when the lease was first acquired.</param>
/// <param name="RenewedAtUtc">
/// Instant (UTC, with offset) of the last acquisition, renewal or release of the lease.
/// </param>
/// <param name="ExpiresAtUtc">
/// Instant (UTC, with offset) when the lease stops being active. A released lease has this set to the release time.
/// </param>
/// <param name="IsActive">
/// True when the lease is neither released nor expired at the time of the response; false for a released or expired
/// lease.
/// </param>
public sealed record ProjectStructureLeaseSnapshot(
    ProjectStructureLeaseScopeKind ScopeKind,
    string ScopeKey,
    string LeaseToken,
    string AgentId,
    string AgentName,
    string MachineName,
    string RepositoryRoot,
    string BranchName,
    string Reason,
    DateTimeOffset AcquiredAtUtc,
    DateTimeOffset RenewedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    bool IsActive);

public sealed record ProjectStructureLeaseConflict(
    ProjectStructureLeaseScopeKind ScopeKind,
    string ScopeKey,
    string AgentId,
    string AgentName,
    string MachineName,
    string RepositoryRoot,
    string BranchName,
    string Reason,
    DateTimeOffset AcquiredAtUtc,
    DateTimeOffset RenewedAtUtc,
    DateTimeOffset ExpiresAtUtc);

/// <summary>
/// Project values for creating a root project or replacing an existing project's values. An update replaces every
/// member with the sent value, so send the current values of members you keep: an omitted <c>status</c> becomes Draft
/// and an omitted <c>targetDateUtc</c> is cleared. An update also removes the project's phases and option selections,
/// which this request cannot carry.
/// </summary>
/// <param name="Name">Project name; required, not blank, trimmed.</param>
/// <param name="Description">Project description; null is stored as empty. Trimmed.</param>
/// <param name="Objective">Project objective; null is stored as empty. Trimmed.</param>
/// <param name="CurrentPhase">Name of the project's current phase; null is stored as empty. Trimmed.</param>
/// <param name="Status">
/// Project status, as a JSON integer: 0 Draft, 1 Active, 2 OnHold, 3 Completed, 4 Archived. Defaults to Draft when
/// omitted.
/// </param>
/// <param name="TargetDateUtc">Optional target date as an ISO 8601 date and time in UTC; null clears it.</param>
/// <param name="LeaseToken">
/// For an update only: optional token of the caller's active Project lease on this project, from <c>POST
/// /api/project-structure/leases/acquire</c>. When omitted, the update uses the project lease the caller's identity
/// already holds, or else takes a five-minute project lease for its own duration, waiting for another identity's lease
/// that ends within 30 seconds (a longer one fails with HTTP 409 <c>LeaseConflict</c>); when sent, it must match the
/// caller's active project lease, otherwise HTTP 409 <c>LeaseMissing</c> or <c>LeaseConflict</c>. Ignored when creating
/// a project.
/// </param>
public sealed record ProjectStructureProjectSaveRequest(
    string Name,
    string Description,
    string Objective,
    string CurrentPhase,
    ProjectStatus Status = ProjectStatus.Draft,
    DateTime? TargetDateUtc = null,
    string? LeaseToken = null);

/// <summary>
/// Hierarchy change that makes a project a subproject of the route's parent project, either as an additional parent or
/// by moving it from one of its current parents.
/// </summary>
/// <param name="ChildProjectId">Identifier of the project that becomes the subproject; required.</param>
/// <param name="CurrentParentProjectId">
/// Parent project to move the child away from; null or omitted adds the route's parent as an additional parent and
/// keeps the child's existing parents.
/// </param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on the child project (not the parent), from <c>POST
/// /api/project-structure/leases/acquire</c>. When omitted, the operation uses the lease the caller's identity already
/// holds on the child project, or else takes a five-minute lease on it for its own duration, waiting for another
/// identity's lease that ends within 30 seconds (a longer one fails with HTTP 409 <c>LeaseConflict</c>); when sent, it
/// must match the caller's active lease on the child project, otherwise HTTP 409 <c>LeaseMissing</c> or
/// <c>LeaseConflict</c>.
/// </param>
public sealed record ProjectStructureSubprojectChangeRequest(
    Guid ChildProjectId,
    Guid? CurrentParentProjectId = null,
    string? LeaseToken = null);

/// <summary>
/// Source of a structure read, as a JSON integer. 0 ContextDefault (the default; over HTTP it reads the current
/// structure), 1 InvocationSnapshot (a bounded snapshot attached to an in-process agent invocation; not available over
/// HTTP and rejected with HTTP 400 <c>ProjectStructureReadSourceUnavailable</c>), 2 CanonicalCurrent (the authoritative
/// current structure). Any other value is rejected with HTTP 400 <c>ProjectStructureReadSourceInvalid</c>. A
/// snapshot is never silently replaced by current data.
/// </summary>
public enum ProjectStructureReadSource
{
    ContextDefault,
    InvocationSnapshot,
    CanonicalCurrent
}

/// <summary>
/// Structure read request: which nodes of the project to return and which optional sections to fill. Every member is
/// optional; the empty object <c>{}</c> returns all nodes without notes, metadata, layout and file fields and without
/// links. <c>nodeIds</c> and <c>subtreeRootIds</c> are combined (union); every other filter must also match. Nodes are
/// ordered by title, ignoring case.
/// </summary>
/// <param name="NodeIds">
/// Node identifiers to return, as returned in <c>nodes[].id</c>, matched exactly; blank entries are ignored and unknown
/// identifiers match nothing. When both <c>nodeIds</c> and <c>subtreeRootIds</c> are null or empty, every node is a
/// candidate.
/// </param>
/// <param name="SubtreeRootIds">
/// Node identifiers whose subtrees to return: each listed node and all its descendants by <c>parentId</c>. Combined
/// with <c>nodeIds</c> as a union.
/// </param>
/// <param name="ObjectTypes">
/// Object types to keep, each a case-insensitive object type symbol such as <c>WorkItem</c> or its integer; null or
/// empty keeps every type.
/// </param>
/// <param name="ProjectRoles">
/// Project hierarchy roles to keep, as JSON integers: 0 None, 1 ActiveProject, 2 Subproject, 3 ParentProject,
/// 4 AdditionalParentProject. For example <c>[1, 2]</c> keeps the project root and its subproject nodes; null or empty
/// keeps every role.
/// </param>
/// <param name="Statuses">
/// Status texts to keep, compared whole and ignoring case (no partial match); null or empty keeps every status.
/// </param>
/// <param name="OnlyUnfinished">
/// True to drop finished nodes: nodes with progress mode <c>complete</c> or progress 100, or whose status contains
/// done, complete, approved, ready, final or archived. Defaults to false.
/// </param>
/// <param name="MaxPriority">
/// Keep only nodes whose effective priority is from 1 through this value (1 is the most urgent); nodes without a
/// priority (0) are dropped. Null applies no priority filter.
/// </param>
/// <param name="IncludeLinks">
/// True to return the links whose source and target are both in the result; false (the default) returns an empty
/// <c>links</c> array.
/// </param>
/// <param name="IncludeLayout">True to fill the canvas coordinates <c>x</c> and <c>y</c>; otherwise they are
/// null.</param>
/// <param name="IncludeMetadata">
/// True to fill <c>metadataJson</c>; otherwise it is null. Task edits need it, because a task's current estimate,
/// execution state and assignment revision are read from it.
/// </param>
/// <param name="IncludeNotes">True to fill <c>notes</c>; otherwise it is null.</param>
/// <param name="IncludeAssets">
/// True to fill the stored-file members <c>mediaRelativePath</c>, <c>mediaContentType</c> and
/// <c>mediaOriginalFileName</c> (empty strings when a node has no file); otherwise they are null.
/// </param>
/// <param name="Take">
/// Maximum number of nodes, applied after filtering and ordering; a cut result adds a warning. A value below 1 is
/// treated as 1. Null returns every matching node.
/// </param>
/// <param name="Source">
/// Read source, as a JSON integer: 0 ContextDefault (the default) and 2 CanonicalCurrent read the current structure;
/// 1 InvocationSnapshot is rejected over HTTP.
/// </param>
public sealed record ProjectStructureReadRequest(
    IReadOnlyList<string>? NodeIds = null,
    IReadOnlyList<string>? SubtreeRootIds = null,
    IReadOnlyList<ProjectObjectType>? ObjectTypes = null,
    IReadOnlyList<ProjectStructureProjectRole>? ProjectRoles = null,
    IReadOnlyList<string>? Statuses = null,
    bool OnlyUnfinished = false,
    int? MaxPriority = null,
    bool IncludeLinks = false,
    bool IncludeLayout = false,
    bool IncludeMetadata = false,
    bool IncludeNotes = false,
    bool IncludeAssets = false,
    int? Take = null,
    ProjectStructureReadSource Source = ProjectStructureReadSource.ContextDefault);

/// <summary>
/// One action the application offers for a node in its structure editor, for example running a script node or showing
/// a file in its folder. Descriptive only: the HTTP API does not execute these actions.
/// </summary>
/// <param name="ActionId">
/// Stable action identifier, for example <c>runtime:open</c>, <c>runtime:terminal</c>, <c>runtime:admin</c>,
/// <c>runtime:stop</c>, <c>open-local</c>, <c>open-new-tab</c> or <c>browse-files</c>.
/// </param>
/// <param name="Label">Display label of the action.</param>
/// <param name="Surface">Where the structure editor offers the action, as display text.</param>
/// <param name="Description">What the action does, as display text.</param>
public sealed record ProjectStructureNodeActionDescriptor(
    string ActionId,
    string Label,
    string Surface,
    string Description);

/// <summary>
/// What the server can do with a node on its own machine: run its runtime plan (script, environment and Docker nodes),
/// open its file location, open it in a new browser tab or browse its files, plus the storage it is bound to. It
/// describes the host's capabilities, not the caller's permissions, and the HTTP API does not start these actions.
/// </summary>
/// <param name="CanRunNormally">True when the node's runtime plan can be executed directly on the server.</param>
/// <param name="CanRunAsAdministrator">True when the runtime plan can be started with elevation on the server.</param>
/// <param name="CanOpenInFileExplorer">
/// True when an existing file or folder of the node, inside the configured workspace root, can be shown in the server's
/// system file browser.
/// </param>
/// <param name="CanOpenInNewTab">True when the node is IPFS-backed and opens in a separate browser tab.</param>
/// <param name="RuntimeDisplayName">Display name of the node's runtime plan; empty when there is none.</param>
/// <param name="RuntimeDisplayCommand">Always empty in this API: runtime commands are not disclosed over HTTP.</param>
/// <param name="RuntimeWorkingDirectory">Always empty in this API: working directories are not disclosed over
/// HTTP.</param>
/// <param name="OpenInNewTabRoute">Route opened in a new tab when <c>canOpenInNewTab</c> is true; otherwise
/// empty.</param>
/// <param name="StorageProvider">
/// Name of the storage provider holding the node's stored object; empty when the node has no stored object.
/// </param>
/// <param name="StorageLocatorKind">Name of the kind of <c>storageLocator</c>; empty when there is no stored
/// object.</param>
/// <param name="StorageLocator">
/// Provider-specific locator of the stored object (its syntax depends on the provider and locator kind); empty when
/// there is no stored object. Do not treat it as a path on the client's machine.
/// </param>
/// <param name="Actions">Actions the structure editor offers for the node.</param>
/// <param name="Guidance">
/// Display notes about the actions, including why a runtime action is unavailable on this host.
/// </param>
public sealed record ProjectStructureNodeActionCapabilities(
    bool CanRunNormally,
    bool CanRunAsAdministrator,
    bool CanOpenInFileExplorer,
    bool CanOpenInNewTab,
    string RuntimeDisplayName,
    string RuntimeDisplayCommand,
    string RuntimeWorkingDirectory,
    string OpenInNewTabRoute,
    string StorageProvider,
    string StorageLocatorKind,
    string StorageLocator,
    IReadOnlyList<ProjectStructureNodeActionDescriptor> Actions,
    IReadOnlyList<string> Guidance);

/// <summary>
/// One node of a project structure: its identity, place in the hierarchy, display fields, status, progress, markers,
/// priority, schedule and optional sections. Returned in <c>nodes</c> by the structure read and as the result of node,
/// approval-request and asset creation and of node changes. In a structure read, <c>notes</c>, <c>metadataJson</c>,
/// <c>x</c>/<c>y</c> and the stored-file members are null unless the matching include flag is set; node creation and
/// change responses always fill them.
/// </summary>
/// <param name="Id">
/// String node identifier, unique within the project; use it in node routes and requests. Nodes created through this
/// API have the form <c>custom:</c> followed by 32 hexadecimal characters, for example
/// <c>custom:3f2504e04f8911d39a0c0305e82c3301</c>. Nodes kept in sync with other records use other prefixes followed by
/// that record's identifier, for example <c>project:{projectId}</c> for the project root,
/// <c>project-child:{projectId}</c> for a subproject or <c>phase:{phaseId}</c>. Treat it as opaque and compare it
/// exactly; it is not a GUID.
/// </param>
/// <param name="ParentId">
/// Identifier of the parent node in the hierarchy; null for a top-level node such as the project root.
/// </param>
/// <param name="ObjectType">Kind of project object the node represents.</param>
/// <param name="ObjectSubtype">
/// Lower-case subtype that refines the object type, for example <c>task</c> for a WorkItem or <c>pdf</c> for a File;
/// empty when none. A WorkItem with subtype <c>task</c> is a canonical task, changed only through the task operations.
/// </param>
/// <param name="Title">Display title of the node.</param>
/// <param name="Subtitle">Secondary display text; may be empty.</param>
/// <param name="Status">
/// Free-text status, for example Draft, Active or Done. Progress and the finished state are derived from words in it
/// (see <c>progressMode</c>).
/// </param>
/// <param name="Notes">Free-text notes; in a structure read, null unless <c>includeNotes</c> was true.</param>
/// <param name="Route">Application route that shows the node or its artifact in the web UI; informational.</param>
/// <param name="ArtifactKind">
/// Kind of record the node represents, for example <c>project</c> for project nodes or the object type name for
/// ordinary nodes; informational.
/// </param>
/// <param name="ArtifactId">
/// Identifier of the external record the node is bound to, for example the project id of a project node; null when
/// the node has no external record.
/// </param>
/// <param name="MediaRelativePath">
/// Location of the node's stored file within the application's managed storage; empty when the node has no file. In a
/// structure read, null unless <c>includeAssets</c> was true. Not a path on the client's machine.
/// </param>
/// <param name="MediaContentType">
/// Content type of the stored file, for example <c>application/pdf</c>; empty when there is none. In a structure read,
/// null unless <c>includeAssets</c> was true.
/// </param>
/// <param name="MediaOriginalFileName">
/// Original file name of the stored file; empty when there is none. In a structure read, null unless
/// <c>includeAssets</c> was true.
/// </param>
/// <param name="Badges">
/// Display labels derived from the node, for example Synced (kept in sync with another record), Scheduled (has a
/// planned start), a subtype label, Uploaded (has an uploaded file), Subproject, Parent or Shared parent. For display
/// only; do not parse them.
/// </param>
/// <param name="ProgressMode">
/// How progress is expressed: <c>complete</c>, <c>started</c>, <c>progress</c> or <c>na</c> (not applicable); empty
/// when progress is not tracked, as for project hierarchy nodes. A status change resets the mode and percentage from
/// the status text.
/// </param>
/// <param name="ProgressPercent">
/// Progress from 0 through 100, or -1 when it is not tracked. Unless set explicitly, it follows the status: for example
/// 28 for draft, planned, pending or queued, 62 for active, running or blocked, 78 for review or testing, and 100 for
/// done, complete, approved, ready or final.
/// </param>
/// <param name="MarkerIcon">Icon token of the node's primary marker; empty when the node has no marker.</param>
/// <param name="MarkerTone">Tone (color) token of the node's primary marker; empty when there is none.</param>
/// <param name="MarkerLabel">
/// Label of the node's primary marker; empty when there is none. A node can carry several markers; only the primary one
/// is returned.
/// </param>
/// <param name="Priority">Priority set on the node itself: 0 means none; smaller positive values are more
/// urgent.</param>
/// <param name="EffectivePriority">
/// Priority used for ordering and filtering, from 0 (none) through 6: the node's own priority or, unless the node is
/// finished, paused or stopped, the most urgent nonzero priority among it and its descendants. Node creation and change
/// responses return the node's own priority here.
/// </param>
/// <param name="StartUtc">Planned start as an instant with offset; null when the node is not scheduled.</param>
/// <param name="EndUtc">Planned end as an instant with offset; null when the node is not scheduled.</param>
/// <param name="MetadataJson">
/// Node metadata as a JSON string that contains a JSON object; parse it before use. In a structure read, null unless
/// <c>includeMetadata</c> was true. Its sections are named after the object type, for example <c>workItem</c> for tasks
/// (with <c>expectedEffortHours</c>, <c>expectedEffortUnit</c>, <c>executionState</c> and
/// <c>directAssignmentRevision</c>) or <c>projectBlock</c>. Enum values inside it are camel-case strings such as
/// <c>"hours"</c> or <c>"notStarted"</c>, unlike the integers used in request bodies.
/// </param>
/// <param name="ProjectRole">
/// Role of the node in the project hierarchy, as a JSON integer: 0 None, 1 ActiveProject, 2 Subproject,
/// 3 ParentProject, 4 AdditionalParentProject.
/// </param>
/// <param name="RelatedProjectId">
/// Project id that a project hierarchy node stands for (project root, subproject or parent project); null for other
/// nodes.
/// </param>
/// <param name="ParentProjectCount">
/// For a subproject node, the number of project nodes in this structure that contain it; 0 for other nodes.
/// </param>
/// <param name="X">
/// Horizontal canvas position in the structure editor; in a structure read, null unless <c>includeLayout</c> was true.
/// </param>
/// <param name="Y">
/// Vertical canvas position in the structure editor; in a structure read, null unless <c>includeLayout</c> was true.
/// </param>
/// <param name="DurationSeconds">Stored planned duration in seconds; null when none is stored.</param>
/// <param name="ActionCapabilities">What the server can do with the node on its own machine; null when nothing
/// applies.</param>
public sealed record ProjectStructureNodeSummary(
    string Id,
    string? ParentId,
    ProjectObjectType ObjectType,
    string ObjectSubtype,
    string Title,
    string Subtitle,
    string Status,
    string? Notes,
    string Route,
    string ArtifactKind,
    Guid? ArtifactId,
    string? MediaRelativePath,
    string? MediaContentType,
    string? MediaOriginalFileName,
    IReadOnlyList<string> Badges,
    string ProgressMode,
    int ProgressPercent,
    string MarkerIcon,
    string MarkerTone,
    string MarkerLabel,
    int Priority,
    int EffectivePriority,
    DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc,
    string? MetadataJson,
    ProjectStructureProjectRole ProjectRole,
    Guid? RelatedProjectId,
    int ParentProjectCount,
    double? X,
    double? Y,
    int? DurationSeconds = null,
    ProjectStructureNodeActionCapabilities? ActionCapabilities = null) : IAgentToolInvocationResultEvidence
{
    AgentToolInvocationOutcome IAgentToolInvocationResultEvidence.Outcome =>
        AgentToolInvocationOutcome.Succeeded;

    AgentToolEffectState IAgentToolInvocationResultEvidence.EffectState =>
        AgentToolEffectState.Committed;

    string IAgentToolInvocationResultEvidence.FailureCode => string.Empty;

    string IAgentToolInvocationResultEvidence.SafeMessage => string.Empty;

    bool IAgentToolInvocationResultEvidence.CanRetryWithCorrectedInput => false;

    /// <summary>
    /// Receipt that ties the node to the process step that created it as an asset. Only process runs executing inside
    /// the application set it; the Project Structure HTTP API omits this member.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProjectProcessAssetReceiptObservation? ProcessAssetReceipt { get; init; }
}

/// <summary>
/// One directed link between two nodes of a project structure, returned by the structure read when
/// <c>includeLinks</c> is true.
/// </summary>
/// <param name="SourceId">Identifier of the node the link starts from, as in <c>nodes[].id</c>.</param>
/// <param name="TargetId">Identifier of the node the link points to, as in <c>nodes[].id</c>.</param>
/// <param name="Kind">
/// Meaning of the link, as a JSON integer: 0 Contains, 1 DependsOn, 2 Uses, 3 Validates, 4 Tests, 5 Blocks,
/// 6 DerivedFrom, 7 BelongsTo. For DependsOn the source depends on the target.
/// </param>
/// <param name="IsUserAuthored">
/// True when a caller created the link; false for links the owner maintains, such as the containment links between a
/// parent and its children.
/// </param>
public sealed record ProjectStructureLinkSummary(
    string SourceId,
    string TargetId,
    ProjectObjectLinkKind Kind,
    bool IsUserAuthored);

/// <summary>
/// Result of a structure read: the selected nodes of the project, the links between them when requested, warnings,
/// and the project write admission to send back with admission-checked writes.
/// </summary>
/// <param name="ProjectId">Identifier of the project that was read.</param>
/// <param name="ProjectName">Name of the project at the time of the read.</param>
/// <param name="Nodes">Selected nodes, ordered by title ignoring case; empty when nothing matched.</param>
/// <param name="Links">
/// Links whose source and target are both in <c>nodes</c>; empty unless <c>includeLinks</c> was true.
/// </param>
/// <param name="Warnings">
/// Human-readable notes about the result, for example that it was cut to <c>take</c> nodes; empty when there are none.
/// </param>
public sealed record ProjectStructureReadResponse(
    Guid ProjectId,
    string ProjectName,
    IReadOnlyList<ProjectStructureNodeSummary> Nodes,
    IReadOnlyList<ProjectStructureLinkSummary> Links,
    IReadOnlyList<string> Warnings) {
    /// <summary>
    /// Project write admission for the project lifetime that was read. Send it back unchanged as
    /// <c>expectedProjectAdmission</c> with task creation, task updates, task resource attachment and node deletions
    /// that remove or retain managed files; those writes are rejected with HTTP 409 when the admission is missing,
    /// names another project or no longer matches the project's current lifetime. Obtain a fresh one by reading again;
    /// never construct it. Always present in a successful structure read.
    /// </summary>
    public ProjectWriteAdmission? ExpectedProjectAdmission { get; init; }

}

/// <summary>
/// Catalog of the node kinds, object types and link kinds that the structure editor offers, with guidance for choosing
/// them. It is the same for every project. Use it to pick <c>objectType</c>, <c>objectSubtype</c> and link
/// <c>kind</c> values for creation requests.
/// </summary>
/// <param name="Items">Node kinds that can be added to a structure, each with its object type and subtype.</param>
/// <param name="ObjectTypes">Every object type with its label and the subtypes callers can create.</param>
/// <param name="LinkKinds">Every link kind with its label and usage guidance.</param>
/// <param name="Guidance">
/// Plain-text advice for building nodes, for example which object type and subtype to use for a task or a folder and
/// which metadata members to set.
/// </param>
public sealed record ProjectStructureNodeCatalogResponse(
    IReadOnlyList<ProjectStructureNodeCatalogItem> Items,
    IReadOnlyList<ProjectStructureNodeCatalogObjectType> ObjectTypes,
    IReadOnlyList<ProjectStructureLinkKindCatalogItem> LinkKinds,
    IReadOnlyList<string> Guidance);

/// <summary>
/// One node kind the structure editor can add, described by its object type and subtype, labels and input fields.
/// Create most kinds with <c>POST /api/project-structure/projects/{projectId}/nodes</c>, sending this entry's
/// <c>objectType</c> and <c>objectSubtype</c>. Exceptions: canonical tasks (WorkItem with subtype <c>task</c>) are
/// created with <c>POST /api/project-structure/projects/{projectId}/tasks</c>, and File nodes and <c>mermaid</c>
/// subtypes with <c>POST /api/project-structure/projects/{projectId}/assets</c>.
/// </summary>
/// <param name="ActionId">Stable identifier of the catalog entry, for example <c>add-block-delivery</c>.</param>
/// <param name="ObjectType">Object type of the node this entry creates.</param>
/// <param name="ObjectSubtype">Subtype of the node this entry creates; empty when the entry has none.</param>
/// <param name="GroupKey">
/// Group the editor shows the entry in, for example <c>assets</c>, <c>blocks</c>, <c>work</c> or <c>runtime</c>.
/// </param>
/// <param name="Label">Display name of the node kind.</param>
/// <param name="Description">What the node kind is used for, as display text.</param>
/// <param name="DefaultTitle">Title the editor proposes for a new node of this kind.</param>
/// <param name="TitleLabel">Editor label for the node's title field.</param>
/// <param name="SubtitleLabel">Editor label for the node's subtitle field.</param>
/// <param name="NotesLabel">Editor label for the node's notes field.</param>
/// <param name="RequiresFile">True when a node of this kind needs uploaded file content.</param>
/// <param name="AcceptedFileTypes">
/// Comma-separated file extensions and media types accepted for the file, for example
/// <c>.zip,.rar,.7z,application/zip</c>; empty when the kind has no file.
/// </param>
/// <param name="InputFields">Additional input fields the editor shows for this kind; empty when none.</param>
/// <param name="DefaultInputValues">Values the editor pre-fills for some input fields; empty when none.</param>
/// <param name="Aliases">
/// Alternative names and phrases for finding this entry. Not every alias is accepted as <c>objectType</c>; send the
/// entry's <c>objectType</c> and <c>objectSubtype</c> instead.
/// </param>
public sealed record ProjectStructureNodeCatalogItem(
    string ActionId,
    ProjectObjectType ObjectType,
    string ObjectSubtype,
    string GroupKey,
    string Label,
    string Description,
    string DefaultTitle,
    string TitleLabel,
    string SubtitleLabel,
    string NotesLabel,
    bool RequiresFile,
    string AcceptedFileTypes,
    IReadOnlyList<ProjectStructureNodeCatalogField> InputFields,
    IReadOnlyList<ProjectStructureNodeCatalogDefaultValue> DefaultInputValues,
    IReadOnlyList<string> Aliases);

/// <summary>
/// One additional input field the structure editor shows for a node kind, such as an output folder for a delivery
/// block or an image size for a generated image.
/// </summary>
/// <param name="Key">
/// Key of the input value, for example <c>outputRoot</c>; the catalog guidance explains which metadata member to set
/// for each kind.
/// </param>
/// <param name="Label">Display label of the field.</param>
/// <param name="InputMode">
/// Kind of editor control: <c>text</c>, <c>textarea</c>, <c>select</c>, <c>number</c>, <c>url</c>, <c>email</c>,
/// <c>tel</c> or <c>datetime-local</c>.
/// </param>
/// <param name="Placeholder">Placeholder or example text shown in the empty field.</param>
/// <param name="IsRequired">True when the editor requires a value for the field.</param>
/// <param name="Options">
/// Values offered for a <c>select</c> field; empty for other fields and for select fields whose values the catalog does
/// not list.
/// </param>
public sealed record ProjectStructureNodeCatalogField(
    string Key,
    string Label,
    string InputMode,
    string Placeholder,
    bool IsRequired,
    IReadOnlyList<ProjectStructureNodeCatalogOption> Options);

/// <summary>One allowed value of a <c>select</c> input field in the node catalog.</summary>
/// <param name="Value">Value to use.</param>
/// <param name="Label">Display label of the value.</param>
public sealed record ProjectStructureNodeCatalogOption(
    string Value,
    string Label);

/// <summary>Value the structure editor pre-fills for one input field of a node kind.</summary>
/// <param name="Key">Key of the input field, as in <c>inputFields[].key</c>.</param>
/// <param name="Value">Pre-filled value.</param>
public sealed record ProjectStructureNodeCatalogDefaultValue(
    string Key,
    string Value);

/// <summary>One project object type in the node catalog, with the subtypes callers can create.</summary>
/// <param name="ObjectType">The object type.</param>
/// <param name="Label">Display name of the object type.</param>
/// <param name="IsUserCreatable">
/// False for types only the owner creates, such as the project root and process or workflow definitions and runs.
/// </param>
/// <param name="CreatableSubtypes">
/// Lower-case subtypes callers can create for this type, for example <c>task</c> or <c>issue</c> for WorkItem; empty
/// when the type has no predefined subtypes.
/// </param>
public sealed record ProjectStructureNodeCatalogObjectType(
    ProjectObjectType ObjectType,
    string Label,
    bool IsUserCreatable,
    IReadOnlyList<string> CreatableSubtypes);

/// <summary>One link kind in the node catalog, with guidance on when to use it.</summary>
/// <param name="Kind">
/// The link kind, as a JSON integer: 0 Contains, 1 DependsOn, 2 Uses, 3 Validates, 4 Tests, 5 Blocks, 6 DerivedFrom,
/// 7 BelongsTo.
/// </param>
/// <param name="Label">Name of the link kind, for example <c>DependsOn</c>.</param>
/// <param name="Guidance">
/// When to use the link kind and its direction, for example that the source of a DependsOn link depends on its target.
/// </param>
public sealed record ProjectStructureLinkKindCatalogItem(
    ProjectObjectLinkKind Kind,
    string Label,
    string Guidance);

[JsonConverter(typeof(ProjectStructureNodeCreateInputJsonConverter))]
public sealed record ProjectStructureNodeCreateInput(
    ProjectObjectType ObjectType,
    string Title,
    string Subtitle,
    string Notes,
    string? ParentNodeKey,
    double? X = null,
    double? Y = null,
    DateTimeOffset? StartUtc = null,
    DateTimeOffset? EndUtc = null,
    string? ObjectSubtype = null,
    ProjectObjectMediaPayload? Media = null,
    string? MetadataJson = null,
    string? LeaseToken = null,
    int? DurationSeconds = null);

[JsonConverter(typeof(ProjectStructureNodeEditInputJsonConverter))]
public sealed record ProjectStructureNodeEditInput(
    string Title,
    string Subtitle,
    string Notes,
    ProjectObjectType? ObjectType = null,
    string? ObjectSubtype = null,
    DateTimeOffset? StartUtc = null,
    DateTimeOffset? EndUtc = null,
    string? MetadataJson = null,
    string? LeaseToken = null,
    int? DurationSeconds = null);

/// <summary>
/// Metadata change for one generic node: replaces its metadata and optionally its notes and status. Canonical tasks
/// are rejected; their values change through the task update.
/// </summary>
/// <param name="MetadataJson">
/// New metadata as a JSON string that contains a JSON object, for example
/// <c>"{\"projectBlock\":{\"outputRoot\":\"\"}}"</c>. It replaces the stored metadata; a blank string or exactly
/// <c>{}</c> keeps it. Script, Environment and Infrastructure metadata is validated (HTTP 400
/// <c>InvalidRuntimeMetadata</c>, or HTTP 403 <c>RuntimePathNotAuthorized</c> for a path outside the managed
/// workspace).
/// </param>
/// <param name="Notes">New notes, trimmed; null or omitted keeps the current notes, an empty string clears
/// them.</param>
/// <param name="Status">
/// New free-text status; null, omitted or blank keeps the current status. A new status also resets the progress mode
/// and percentage from the status text.
/// </param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on this project, from <c>POST
/// /api/project-structure/leases/acquire</c>. When omitted, the operation uses the project lease the caller's identity
/// already holds, or else takes a five-minute project lease for its own duration, waiting for another identity's lease
/// that ends within 30 seconds (a longer one fails with HTTP 409 <c>LeaseConflict</c>); when sent, it must match the
/// caller's active project lease, otherwise HTTP 409 <c>LeaseMissing</c> or <c>LeaseConflict</c>.
/// </param>
public sealed record ProjectStructureNodeMetadataInput(
    string MetadataJson,
    string? Notes = null,
    string? Status = null,
    string? LeaseToken = null);

/// <summary>
/// Status change for several nodes of one project. Nodes kept in sync with other records (badge Synced) are skipped.
/// </summary>
/// <param name="NodeIds">
/// Identifiers of the nodes to change, as returned in <c>nodes[].id</c>; blank entries and unknown identifiers are
/// ignored.
/// </param>
/// <param name="Status">
/// New free-text status, trimmed, for example Active or Done. It also resets each node's progress mode and percentage
/// from the status text. A blank status changes nothing.
/// </param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on this project, from <c>POST
/// /api/project-structure/leases/acquire</c>. When omitted, the operation uses the project lease the caller's identity
/// already holds, or else takes a five-minute project lease for its own duration, waiting for another identity's lease
/// that ends within 30 seconds (a longer one fails with HTTP 409 <c>LeaseConflict</c>); when sent, it must match the
/// caller's active project lease, otherwise HTTP 409 <c>LeaseMissing</c> or <c>LeaseConflict</c>.
/// </param>
public sealed record ProjectStructureStatusBatchInput(
    IReadOnlyList<string> NodeIds,
    string Status,
    string? LeaseToken = null);

/// <summary>Status change for the node named in the route.</summary>
/// <param name="Status">
/// New free-text status, trimmed, for example Active or Done. It also resets the node's progress mode and percentage
/// from the status text. A blank status changes nothing.
/// </param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on this project, from <c>POST
/// /api/project-structure/leases/acquire</c>. When omitted, the operation uses the project lease the caller's identity
/// already holds, or else takes a five-minute project lease for its own duration, waiting for another identity's lease
/// that ends within 30 seconds (a longer one fails with HTTP 409 <c>LeaseConflict</c>); when sent, it must match the
/// caller's active project lease, otherwise HTTP 409 <c>LeaseMissing</c> or <c>LeaseConflict</c>.
/// </param>
public sealed record ProjectStructureStatusInput(
    string Status,
    string? LeaseToken = null);

/// <summary>Explicit progress for several nodes of one project, independent of their status.</summary>
/// <param name="NodeIds">
/// Identifiers of the nodes to change, as returned in <c>nodes[].id</c>; blank entries and unknown identifiers are
/// ignored.
/// </param>
/// <param name="ProgressMode">
/// Progress mode: <c>complete</c>, <c>started</c>, <c>progress</c> or <c>na</c>, ignoring case; any other value is
/// stored as <c>progress</c>.
/// </param>
/// <param name="ProgressPercent">Progress percentage; values outside 0 through 100 are clamped.</param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on this project, from <c>POST
/// /api/project-structure/leases/acquire</c>. When omitted, the operation uses the project lease the caller's identity
/// already holds, or else takes a five-minute project lease for its own duration, waiting for another identity's lease
/// that ends within 30 seconds (a longer one fails with HTTP 409 <c>LeaseConflict</c>); when sent, it must match the
/// caller's active project lease, otherwise HTTP 409 <c>LeaseMissing</c> or <c>LeaseConflict</c>.
/// </param>
public sealed record ProjectStructureProgressBatchInput(
    IReadOnlyList<string> NodeIds,
    string ProgressMode,
    int ProgressPercent,
    string? LeaseToken = null);

/// <summary>Explicit progress for the node named in the route, independent of its status.</summary>
/// <param name="ProgressMode">
/// Progress mode: <c>complete</c>, <c>started</c>, <c>progress</c> or <c>na</c>, ignoring case; any other value is
/// stored as <c>progress</c>.
/// </param>
/// <param name="ProgressPercent">Progress percentage; values outside 0 through 100 are clamped.</param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on this project, from <c>POST
/// /api/project-structure/leases/acquire</c>. When omitted, the operation uses the project lease the caller's identity
/// already holds, or else takes a five-minute project lease for its own duration, waiting for another identity's lease
/// that ends within 30 seconds (a longer one fails with HTTP 409 <c>LeaseConflict</c>); when sent, it must match the
/// caller's active project lease, otherwise HTTP 409 <c>LeaseMissing</c> or <c>LeaseConflict</c>.
/// </param>
public sealed record ProjectStructureProgressInput(
    string ProgressMode,
    int ProgressPercent,
    string? LeaseToken = null);

/// <summary>
/// Marker for several nodes of one project: every current marker of each node is replaced by this one marker, or
/// removed when <c>markerIcon</c> is blank.
/// </summary>
/// <param name="NodeIds">
/// Identifiers of the nodes to change, as returned in <c>nodes[].id</c>; blank entries and unknown identifiers are
/// ignored.
/// </param>
/// <param name="MarkerIcon">
/// Icon token that identifies the marker, stored lower-case; a blank icon removes all markers of the nodes.
/// </param>
/// <param name="MarkerTone">Tone (color) token, stored lower-case; blank means <c>accent</c>.</param>
/// <param name="MarkerLabel">Display label; blank means the icon token.</param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on this project, from <c>POST
/// /api/project-structure/leases/acquire</c>. When omitted, the operation uses the project lease the caller's identity
/// already holds, or else takes a five-minute project lease for its own duration, waiting for another identity's lease
/// that ends within 30 seconds (a longer one fails with HTTP 409 <c>LeaseConflict</c>); when sent, it must match the
/// caller's active project lease, otherwise HTTP 409 <c>LeaseMissing</c> or <c>LeaseConflict</c>.
/// </param>
public sealed record ProjectStructureMarkerBatchInput(
    IReadOnlyList<string> NodeIds,
    string MarkerIcon,
    string MarkerTone,
    string MarkerLabel,
    string? LeaseToken = null);

/// <summary>
/// How a marker change treats the node's existing markers, as a JSON integer. Markers are identified by their icon,
/// ignoring case. 0 Replace (the node keeps only the given marker; a blank icon removes all markers), 1 Add (adds the
/// marker or replaces the one with the same icon), 2 Toggle (removes the marker with this icon if present, otherwise
/// adds it), 3 Remove (removes the marker with this icon), 4 Clear (removes all markers; icon, tone and label are
/// ignored).
/// </summary>
public enum ProjectStructureMarkerMutationMode
{
    Replace,
    Add,
    Toggle,
    Remove,
    Clear
}

/// <summary>
/// Marker change for the node named in the route. The node's primary marker, returned in structure reads, is the one
/// added last.
/// </summary>
/// <param name="Mode">
/// How to treat the existing markers, as a JSON integer: 0 Replace (default), 1 Add, 2 Toggle, 3 Remove, 4 Clear.
/// </param>
/// <param name="MarkerIcon">
/// Icon token that identifies the marker, stored lower-case. A blank icon makes Replace remove all markers and makes
/// Add, Toggle and Remove change nothing.
/// </param>
/// <param name="MarkerTone">Tone (color) token, stored lower-case; blank means <c>accent</c>.</param>
/// <param name="MarkerLabel">Display label; blank means the icon token.</param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on this project, from <c>POST
/// /api/project-structure/leases/acquire</c>. When omitted, the operation uses the project lease the caller's identity
/// already holds, or else takes a five-minute project lease for its own duration, waiting for another identity's lease
/// that ends within 30 seconds (a longer one fails with HTTP 409 <c>LeaseConflict</c>); when sent, it must match the
/// caller's active project lease, otherwise HTTP 409 <c>LeaseMissing</c> or <c>LeaseConflict</c>.
/// </param>
public sealed record ProjectStructureMarkerInput(
    ProjectStructureMarkerMutationMode Mode = ProjectStructureMarkerMutationMode.Replace,
    string MarkerIcon = "",
    string MarkerTone = "",
    string MarkerLabel = "",
    string? LeaseToken = null);

/// <summary>Priority for several nodes of one project.</summary>
/// <param name="NodeIds">
/// Identifiers of the nodes to change, as returned in <c>nodes[].id</c>; blank entries and unknown identifiers are
/// ignored.
/// </param>
/// <param name="Priority">
/// New priority: 0 removes the priority, 1 is the most urgent and 6 the least; values outside 0 through 6 are clamped.
/// </param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on this project, from <c>POST
/// /api/project-structure/leases/acquire</c>. When omitted, the operation uses the project lease the caller's identity
/// already holds, or else takes a five-minute project lease for its own duration, waiting for another identity's lease
/// that ends within 30 seconds (a longer one fails with HTTP 409 <c>LeaseConflict</c>); when sent, it must match the
/// caller's active project lease, otherwise HTTP 409 <c>LeaseMissing</c> or <c>LeaseConflict</c>.
/// </param>
public sealed record ProjectStructurePriorityBatchInput(
    IReadOnlyList<string> NodeIds,
    int Priority,
    string? LeaseToken = null);

/// <summary>Priority for the node named in the route.</summary>
/// <param name="Priority">
/// New priority: 0 removes the priority, 1 is the most urgent and 6 the least; values outside 0 through 6 are clamped.
/// </param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on this project, from <c>POST
/// /api/project-structure/leases/acquire</c>. When omitted, the operation uses the project lease the caller's identity
/// already holds, or else takes a five-minute project lease for its own duration, waiting for another identity's lease
/// that ends within 30 seconds (a longer one fails with HTTP 409 <c>LeaseConflict</c>); when sent, it must match the
/// caller's active project lease, otherwise HTTP 409 <c>LeaseMissing</c> or <c>LeaseConflict</c>.
/// </param>
public sealed record ProjectStructurePriorityInput(
    int Priority,
    string? LeaseToken = null);

/// <summary>
/// Type change for one generic node: the new object type and subtype. The node keeps its title, subtitle, notes,
/// metadata and schedule. Only some changes are supported: a subtype change within the same family of node kinds,
/// promoting a Note to a kind that accepts promotion, and switching between runnable runtime kinds (script, runtime
/// environment, Docker); others are rejected with HTTP 400 <c>NodeReclassificationUnavailable</c>. Changing a node to
/// or from a canonical task (WorkItem with subtype <c>task</c>) is rejected with HTTP 409. Sending the node's current
/// type and subtype clears its planned start, end and duration, so send only real changes.
/// </summary>
/// <param name="ObjectType">New object type, as its name in any casing (for example <c>WorkItem</c>) or its
/// integer.</param>
/// <param name="ObjectSubtype">
/// New lower-case subtype, for example <c>feature</c> for a ProjectBlock; known synonyms are normalized. Null or
/// omitted keeps the current subtype when the type stays the same and clears it when the type changes.
/// </param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on this project, from <c>POST
/// /api/project-structure/leases/acquire</c>. When omitted, the operation uses the project lease the caller's identity
/// already holds, or else takes a five-minute project lease for its own duration, waiting for another identity's lease
/// that ends within 30 seconds (a longer one fails with HTTP 409 <c>LeaseConflict</c>); when sent, it must match the
/// caller's active project lease, otherwise HTTP 409 <c>LeaseMissing</c> or <c>LeaseConflict</c>.
/// </param>
public sealed record ProjectStructureNodeTypeInput(
    ProjectObjectType ObjectType,
    string? ObjectSubtype = null,
    string? LeaseToken = null);

/// <summary>New canvas position for one node in the structure editor. Only the layout changes.</summary>
/// <param name="NodeId">
/// Identifier of the node to move, as returned in <c>nodes[].id</c>. An unknown identifier is ignored and the response
/// is still <c>{ "ok": true }</c>.
/// </param>
/// <param name="X">Horizontal canvas coordinate.</param>
/// <param name="Y">Vertical canvas coordinate.</param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on this project, from <c>POST
/// /api/project-structure/leases/acquire</c>. When omitted, the operation uses the project lease the caller's identity
/// already holds, or else takes a five-minute project lease for its own duration, waiting for another identity's lease
/// that ends within 30 seconds (a longer one fails with HTTP 409 <c>LeaseConflict</c>); when sent, it must match the
/// caller's active project lease, otherwise HTTP 409 <c>LeaseMissing</c> or <c>LeaseConflict</c>.
/// </param>
public sealed record ProjectStructureNodeMoveInput(
    string NodeId,
    double X,
    double Y,
    string? LeaseToken = null);

/// <summary>Automatic re-layout of the descendants of one node on the structure canvas.</summary>
/// <param name="RootNodeId">
/// Identifier of the node whose descendants are repositioned, as returned in <c>nodes[].id</c>; the node itself keeps
/// its position.
/// </param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on this project, from <c>POST
/// /api/project-structure/leases/acquire</c>. When omitted, the operation uses the project lease the caller's identity
/// already holds, or else takes a five-minute project lease for its own duration, waiting for another identity's lease
/// that ends within 30 seconds (a longer one fails with HTTP 409 <c>LeaseConflict</c>); when sent, it must match the
/// caller's active project lease, otherwise HTTP 409 <c>LeaseMissing</c> or <c>LeaseConflict</c>.
/// </param>
public sealed record ProjectStructureNodeRecomposeInput(
    string RootNodeId,
    string? LeaseToken = null);

/// <summary>Parent change for one node, named in the body, within its project.</summary>
/// <param name="NodeId">Identifier of the node to move in the hierarchy, as returned in <c>nodes[].id</c>.</param>
/// <param name="ParentNodeKey">
/// Identifier of the new parent node; null or blank makes the node a child of the project root
/// (<c>project:{projectId}</c>). The parent cannot be the node itself or one of its descendants.
/// </param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on this project, from <c>POST
/// /api/project-structure/leases/acquire</c>. When omitted, the operation uses the project lease the caller's identity
/// already holds, or else takes a five-minute project lease for its own duration, waiting for another identity's lease
/// that ends within 30 seconds (a longer one fails with HTTP 409 <c>LeaseConflict</c>); when sent, it must match the
/// caller's active project lease, otherwise HTTP 409 <c>LeaseMissing</c> or <c>LeaseConflict</c>.
/// </param>
public sealed record ProjectStructureNodeReparentInput(
    string NodeId,
    string? ParentNodeKey,
    string? LeaseToken = null);

/// <summary>
/// Copy of one or more subtrees under a destination parent in the same project. Each source node is copied with all
/// its descendants and the caller-created links between copied nodes; caller-created links that cross the boundary of
/// the copied set are left out and reported. Subtrees that contain a canonical task are rejected.
/// </summary>
/// <param name="SourceNodeIds">
/// Identifiers of the roots of the subtrees to copy, as returned in <c>nodes[].id</c>; at least one, none blank.
/// Duplicates are ignored.
/// </param>
/// <param name="DestinationParentNodeId">
/// Identifier of the editable node that receives the copies; required. Project nodes kept in sync with other projects
/// cannot receive copies.
/// </param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on this project, from <c>POST
/// /api/project-structure/leases/acquire</c>. When omitted, the operation uses the project lease the caller's identity
/// already holds, or else takes a five-minute project lease for its own duration, waiting for another identity's lease
/// that ends within 30 seconds (a longer one fails with HTTP 409 <c>LeaseConflict</c>); when sent, it must match the
/// caller's active project lease, otherwise HTTP 409 <c>LeaseMissing</c> or <c>LeaseConflict</c>.
/// </param>
public sealed record ProjectStructureNodesCopyInput(
    IReadOnlyList<string> SourceNodeIds,
    string DestinationParentNodeId,
    string? LeaseToken = null);

/// <summary>Correspondence between one copied source node and its new copy.</summary>
/// <param name="SourceNodeId">Identifier of the original node.</param>
/// <param name="CopiedNodeId">Identifier of the new node created as its copy.</param>
public sealed record ProjectStructureNodeCopyMapping(
    string SourceNodeId,
    string CopiedNodeId);

/// <summary>
/// Result of a committed subtree copy: the new root nodes, the identifier of every copied node and the links that were
/// not copied because they crossed the copied set.
/// </summary>
/// <param name="ProjectId">Identifier of the project that contains the source and the copies.</param>
/// <param name="DestinationParentNodeId">Identifier of the node that received the copies.</param>
/// <param name="SourceRootNodeIds">Identifiers of the copied subtree roots, in the order of the new roots.</param>
/// <param name="CopiedRootNodeIds">Identifiers of the new root nodes created under the destination.</param>
/// <param name="NodeMappings">One entry per copied node, ordered by source identifier.</param>
/// <param name="OmittedBoundaryLinks">
/// Links between a copied node and a node outside the copied set; they were not copied. Empty when there were none.
/// </param>
public sealed record ProjectStructureNodesCopyResult(
    Guid ProjectId,
    string DestinationParentNodeId,
    IReadOnlyList<string> SourceRootNodeIds,
    IReadOnlyList<string> CopiedRootNodeIds,
    IReadOnlyList<ProjectStructureNodeCopyMapping> NodeMappings,
    IReadOnlyList<ProjectStructureCopyOmittedLink> OmittedBoundaryLinks)
{
    /// <summary>Number of nodes created by the copy, equal to the number of <c>nodeMappings</c>.</summary>
    public int CopiedNodeCount => NodeMappings.Count;
}

/// <summary>Parent change for the node named in the route.</summary>
/// <param name="ParentNodeKey">
/// Identifier of the new parent node; null or blank makes the node a child of the project root
/// (<c>project:{projectId}</c>). The parent cannot be the node itself or one of its descendants.
/// </param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on this project, from <c>POST
/// /api/project-structure/leases/acquire</c>. When omitted, the operation uses the project lease the caller's identity
/// already holds, or else takes a five-minute project lease for its own duration, waiting for another identity's lease
/// that ends within 30 seconds (a longer one fails with HTTP 409 <c>LeaseConflict</c>); when sent, it must match the
/// caller's active project lease, otherwise HTTP 409 <c>LeaseMissing</c> or <c>LeaseConflict</c>.
/// </param>
public sealed record ProjectStructureNodeParentInput(
    string? ParentNodeKey,
    string? LeaseToken = null);

/// <summary>
/// Deletion of the node named in the route with all its descendants, or a retry of the cleanup of an earlier deletion
/// of that node when <c>durableMutationId</c> is sent.
/// </summary>
/// <param name="ManagedStorageDisposition">
/// What to do with the managed files of the deleted nodes, as its name (any casing) or integer:
/// <c>RetainManagedFiles</c> (1) or <c>DeleteOwnedManagedFiles</c> (2). Required; <c>Unspecified</c> (0) or an omitted
/// value is rejected with HTTP 400 <c>ProjectStructureManagedStorageDispositionRequired</c>. A cleanup retry must send
/// the value recorded for that deletion.
/// </param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on this project, from <c>POST
/// /api/project-structure/leases/acquire</c>. When omitted, the operation uses the project lease the caller's identity
/// already holds, or else takes a five-minute project lease for its own duration, waiting for another identity's lease
/// that ends within 30 seconds (a longer one fails with HTTP 409 <c>LeaseConflict</c>); when sent, it must match the
/// caller's active project lease, otherwise HTTP 409 <c>LeaseMissing</c> or <c>LeaseConflict</c>.
/// </param>
/// <param name="DurableMutationId">
/// Omit it for a new deletion. To finish an earlier deletion whose cleanup is pending, send its
/// <c>durableMutationId</c> from <c>GET /api/project-structure/projects/{projectId}/deletion-cleanups</c> or from
/// the <c>details</c> of HTTP 409 <c>ProjectStructureDeletionPartialCommit</c>.
/// </param>
public sealed record ProjectStructureNodeDeleteInput(
    ProjectStructureManagedStorageDisposition ManagedStorageDisposition,
    string? LeaseToken = null,
    Guid? DurableMutationId = null) {
    /// <summary>
    /// Project write admission returned as <c>expectedProjectAdmission</c> by the structure read, sent back unchanged.
    /// Required for a new deletion (no <c>durableMutationId</c>): when it is omitted, null or names another project,
    /// the request is rejected with HTTP 409 <c>ProjectLifetimeRefreshRequired</c> and nothing is deleted. Not needed
    /// for a cleanup retry.
    /// </summary>
    public ProjectWriteAdmission? ExpectedProjectAdmission { get; init; }
}

/// <summary>
/// Deletion of several nodes of one project, each with all its descendants. Listed nodes inside another listed node's
/// subtree are deleted with it. Listed nodes that are already deleted but whose cleanup is pending get that cleanup
/// resumed.
/// </summary>
/// <param name="NodeIds">
/// Identifiers of the nodes to delete, as returned in <c>nodes[].id</c>; blank entries are ignored and at least one
/// identifier is required.
/// </param>
/// <param name="ManagedStorageDisposition">
/// What to do with the managed files of the deleted nodes, as its name (any casing) or integer:
/// <c>RetainManagedFiles</c> (1) or <c>DeleteOwnedManagedFiles</c> (2). Required; <c>Unspecified</c> (0) or an omitted
/// value is rejected with HTTP 400 <c>ProjectStructureManagedStorageDispositionRequired</c>.
/// </param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on this project, from <c>POST
/// /api/project-structure/leases/acquire</c>. When omitted, the operation uses the project lease the caller's identity
/// already holds, or else takes a five-minute project lease for its own duration, waiting for another identity's lease
/// that ends within 30 seconds (a longer one fails with HTTP 409 <c>LeaseConflict</c>); when sent, it must match the
/// caller's active project lease, otherwise HTTP 409 <c>LeaseMissing</c> or <c>LeaseConflict</c>.
/// </param>
public sealed record ProjectStructureNodeDeleteBatchInput(
    IReadOnlyList<string> NodeIds,
    ProjectStructureManagedStorageDisposition ManagedStorageDisposition,
    string? LeaseToken = null) {
    /// <summary>
    /// Project write admission returned as <c>expectedProjectAdmission</c> by the structure read, sent back unchanged.
    /// Required: when it is omitted, null or names another project, the request is rejected with HTTP 409
    /// <c>ProjectLifetimeRefreshRequired</c> and nothing is deleted.
    /// </summary>
    public ProjectWriteAdmission? ExpectedProjectAdmission { get; init; }
}

/// <summary>
/// Directed link between two nodes of one project, to create or remove. The dependency operations always use kind
/// DependsOn and ignore <c>kind</c>.
/// </summary>
/// <param name="SourceNodeId">
/// Identifier of the node the link starts from, as returned in <c>nodes[].id</c>; required. For DependsOn it is the
/// node that depends on the target.
/// </param>
/// <param name="TargetNodeId">
/// Identifier of the node the link points to, as returned in <c>nodes[].id</c>; required and different from the source.
/// </param>
/// <param name="Kind">
/// Meaning of the link, as a JSON integer: 0 Contains, 1 DependsOn (default), 2 Uses, 3 Validates, 4 Tests, 5 Blocks,
/// 6 DerivedFrom, 7 BelongsTo. Contains and BelongsTo are hierarchy links and cannot be created here; change the parent
/// instead. A Uses link from a canonical task is rejected: attach task resources with the task resource operation.
/// </param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on this project, from <c>POST
/// /api/project-structure/leases/acquire</c>. When omitted, the operation uses the project lease the caller's identity
/// already holds, or else takes a five-minute project lease for its own duration, waiting for another identity's lease
/// that ends within 30 seconds (a longer one fails with HTTP 409 <c>LeaseConflict</c>); when sent, it must match the
/// caller's active project lease, otherwise HTTP 409 <c>LeaseMissing</c> or <c>LeaseConflict</c>.
/// </param>
public sealed record ProjectStructureLinkInput(
    string SourceNodeId,
    string TargetNodeId,
    ProjectObjectLinkKind Kind = ProjectObjectLinkKind.DependsOn,
    string? LeaseToken = null);

/// <summary>Result of a committed link creation or removal.</summary>
/// <param name="Changed">
/// For a removal, true when a matching link was removed and false when none existed. For a creation it is always true,
/// also when the same link already existed.
/// </param>
/// <param name="Link">The link that was requested, echoed back with <c>isUserAuthored</c> true.</param>
public sealed record ProjectStructureLinkChangeResult(
    bool Changed,
    ProjectStructureLinkSummary Link);

/// <summary>
/// Link from a node to a process definition, so that the node's process start uses that definition.
/// </summary>
/// <param name="ProcessDefinitionId">
/// Identifier of the process definition, a GUID; required, the empty GUID is rejected with HTTP 400
/// <c>ProcessDefinitionRequired</c>. Its existence is not checked when the link is created.
/// </param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on this project, from <c>POST
/// /api/project-structure/leases/acquire</c>. When omitted, the operation uses the project lease the caller's identity
/// already holds, or else takes a five-minute project lease for its own duration, waiting for another identity's lease
/// that ends within 30 seconds (a longer one fails with HTTP 409 <c>LeaseConflict</c>); when sent, it must match the
/// caller's active project lease, otherwise HTTP 409 <c>LeaseMissing</c> or <c>LeaseConflict</c>.
/// </param>
public sealed record ProjectStructureProcessDefinitionLinkInput(
    Guid ProcessDefinitionId,
    string? LeaseToken = null);

/// <summary>
/// Kind of an additional input source for a workflow node, as a JSON integer: 0 ParentNode, 1 ParentSubtree, 2
/// SelectedNode, 3 FilePath (a file path passed as input), 4 FolderPath (a folder path passed as input), 5 ManualJson.
/// </summary>
public enum ProjectStructureWorkflowInputSourceKind
{
    ParentNode,
    ParentSubtree,
    SelectedNode,
    FilePath,
    FolderPath,
    ManualJson
}

/// <summary>One additional named value included in a workflow node's run input.</summary>
/// <param name="Kind">
/// Kind of source, as a JSON integer: 0 ParentNode, 1 ParentSubtree, 2 SelectedNode, 3 FilePath, 4 FolderPath,
/// 5 ManualJson.
/// </param>
/// <param name="Key">
/// Name of the value in the run input; required when enabled and unique per kind, ignoring case (HTTP 400
/// <c>WorkflowInputSourceInvalid</c> or <c>WorkflowInputSourceDuplicate</c>).
/// </param>
/// <param name="Label">Display label of the source; may be empty.</param>
/// <param name="Value">Value to include; required when enabled.</param>
/// <param name="IsEnabled">False to keep the source out of the run input; disabled sources are dropped. Defaults to
/// true.</param>
public sealed record ProjectStructureWorkflowInputSource(
    ProjectStructureWorkflowInputSourceKind Kind,
    string Key,
    string Label,
    string Value,
    bool IsEnabled = true);

/// <summary>
/// What a workflow node puts into the input of its runs: project and parent-node details (always), optionally the
/// parent's subtree, assets, selected nodes, additional named sources and manual JSON. Invalid settings are rejected
/// with HTTP 400.
/// </summary>
public sealed class ProjectStructureWorkflowInputSettings
{
    /// <summary>Include the project's details; must be true (HTTP 400 <c>WorkflowInputProjectRequired</c>).</summary>
    public bool IncludeProject { get; set; } = true;

    /// <summary>Include the parent node; must be true (HTTP 400 <c>WorkflowInputParentRequired</c>).</summary>
    public bool IncludeParentNode { get; set; } = true;

    /// <summary>
    /// Include the parent node's details; must be true (HTTP 400 <c>WorkflowInputParentRequired</c>).
    /// </summary>
    public bool IncludeParentNodeDetails { get; set; } = true;

    /// <summary>Also include the parent's subtree. Defaults to false.</summary>
    public bool IncludeParentSubtree { get; set; }

    /// <summary>Also include asset information. Defaults to true.</summary>
    public bool IncludeAssets { get; set; } = true;

    /// <summary>
    /// Identifiers of further nodes to include, as in <c>nodes[].id</c>; blank entries and duplicates are removed.
    /// </summary>
    public IReadOnlyList<string> SelectedNodeIds { get; set; } = [];

    /// <summary>Additional named sources; only enabled ones are kept.</summary>
    public IReadOnlyList<ProjectStructureWorkflowInputSource> AdditionalSources { get; set; } = [];

    /// <summary>
    /// Manual input as a JSON string that contains JSON; blank means <c>{}</c>. Invalid JSON is rejected with HTTP 400
    /// <c>WorkflowManualInputInvalid</c>.
    /// </summary>
    public string ManualInputJson { get; set; } = "{}";

    public static ProjectStructureWorkflowInputSettings Default() => new();
}

/// <summary>
/// Workflow node creation request: which workflow definition (and optionally which version) a new WorkflowDefinition
/// node under the route's parent node runs, and what its runs receive as input.
/// </summary>
/// <param name="WorkflowId">
/// Identifier of the workflow definition, a GUID string as listed by the workflow add options; required and active.
/// </param>
/// <param name="VersionId">
/// Workflow version to pin, a GUID string; null or omitted selects the version the workflow catalog returns for the
/// definition.
/// </param>
/// <param name="Title">Title of the node; blank uses the workflow's name.</param>
/// <param name="Subtitle">Subtitle of the node; blank generates one from the workflow.</param>
/// <param name="Notes">Notes of the node; blank generates them from the workflow and the parent node.</param>
/// <param name="InputSettings">Run input settings; null uses the defaults.</param>
/// <param name="X">Preferred horizontal canvas position; the node is placed near its parent automatically.</param>
/// <param name="Y">Preferred vertical canvas position; the node is placed near its parent automatically.</param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on this project, from <c>POST
/// /api/project-structure/leases/acquire</c>. When omitted, the operation uses the project lease the caller's identity
/// already holds, or else takes a five-minute project lease for its own duration, waiting for another identity's lease
/// that ends within 30 seconds (a longer one fails with HTTP 409 <c>LeaseConflict</c>); when sent, it must match the
/// caller's active project lease, otherwise HTTP 409 <c>LeaseMissing</c> or <c>LeaseConflict</c>.
/// </param>
public sealed record ProjectStructureWorkflowNodeCreateInput(
    WorkflowId WorkflowId,
    WorkflowVersionId? VersionId = null,
    string? Title = null,
    string? Subtitle = null,
    string? Notes = null,
    ProjectStructureWorkflowInputSettings? InputSettings = null,
    double? X = null,
    double? Y = null,
    string? LeaseToken = null);

/// <summary>Result of a committed workflow node creation.</summary>
/// <param name="ProjectId">Identifier of the project that contains the node.</param>
/// <param name="Node">
/// The created WorkflowDefinition node; its <c>metadataJson</c> holds the workflow and the input settings.
/// </param>
/// <param name="WorkflowId">Identifier of the workflow definition the node runs, a GUID string.</param>
/// <param name="WorkflowVersionId">Workflow version pinned by the node, a GUID string.</param>
/// <param name="Warnings">Human-readable notes about the creation; empty when there are none.</param>
public sealed record ProjectStructureWorkflowNodeCreateResult(
    Guid ProjectId,
    ProjectStructureNodeSummary Node,
    WorkflowId WorkflowId,
    WorkflowVersionId WorkflowVersionId,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Workflow add-options query: optional preselection and input settings for previewing a workflow node under the
/// route's parent node. Every member is optional.
/// </summary>
/// <param name="WorkflowId">Workflow definition to preselect, a GUID string; null leaves the choice to the
/// owner.</param>
/// <param name="VersionId">Workflow version to preselect, a GUID string; null selects the definition's version.</param>
/// <param name="InputSettings">Input settings to preview; null uses the defaults.</param>
/// <param name="SelectedNodeIds">
/// Node identifiers to include in the preview's input, as in <c>nodes[].id</c>; when sent it replaces
/// <c>inputSettings.selectedNodeIds</c>.
/// </param>
public sealed record ProjectStructureWorkflowAddOptionsInput(
    WorkflowId? WorkflowId = null,
    WorkflowVersionId? VersionId = null,
    ProjectStructureWorkflowInputSettings? InputSettings = null,
    IReadOnlyList<string>? SelectedNodeIds = null);

/// <summary>One workflow definition that can be added to a project structure.</summary>
/// <param name="WorkflowId">Identifier of the workflow definition, a GUID string.</param>
/// <param name="VersionId">Version identifier of the definition, a GUID string.</param>
/// <param name="DisplayName">Display name of the workflow.</param>
/// <param name="Description">Description of the workflow.</param>
/// <param name="Status">
/// Lifecycle status of the definition, as a JSON integer: 0 Draft, 1 Active, 2 Suspended, 3 Archived.
/// </param>
/// <param name="PreferredBackend">
/// Runtime backend the definition prefers, as a JSON integer: 0 InProcess, 1 DurableTask, 2 AzureFunctions.
/// </param>
/// <param name="IsSelectable">True when a workflow node can be created for this definition now.</param>
/// <param name="DisabledReason">Why the definition cannot be selected; empty when it can.</param>
public sealed record ProjectStructureWorkflowDefinitionOption(
    WorkflowId WorkflowId,
    WorkflowVersionId VersionId,
    string DisplayName,
    string Description,
    WorkflowLifecycleStatus Status,
    WorkflowRuntimeBackendKind PreferredBackend,
    bool IsSelectable,
    string DisabledReason);

/// <summary>One labeled value in a section of a workflow input preview.</summary>
/// <param name="Label">Display label.</param>
/// <param name="Value">Displayed value.</param>
public sealed record ProjectStructureWorkflowInputPreviewRow(
    string Label,
    string Value);

/// <summary>One section of a workflow input preview, such as the project or the parent node.</summary>
/// <param name="Title">Section title.</param>
/// <param name="Summary">Short description of the section.</param>
/// <param name="Rows">Labeled values of the section.</param>
public sealed record ProjectStructureWorkflowInputPreviewSection(
    string Title,
    string Summary,
    IReadOnlyList<ProjectStructureWorkflowInputPreviewRow> Rows);

/// <summary>Preview of the input a workflow node's run would receive with the given settings.</summary>
/// <param name="Summary">Short description of the input.</param>
/// <param name="InputJson">The run input as a JSON string that contains JSON; parse it before use.</param>
/// <param name="Sections">The input broken into display sections.</param>
public sealed record ProjectStructureWorkflowInputPreview(
    string Summary,
    string InputJson,
    IReadOnlyList<ProjectStructureWorkflowInputPreviewSection> Sections);

public sealed record ProjectStructureWorkflowPreviewSimulationOption(
    string NodeId,
    string NodeName,
    string ExecutorId,
    WorkflowProjectStructureOperation Operation,
    string Description);

public sealed record ProjectStructureWorkflowStartBackendOption(
    WorkflowRuntimeBackendKind Backend,
    string Label,
    string Notes,
    bool IsSelected);

public sealed record ProjectStructureWorkflowStartOptionsResult(
    IReadOnlyList<ProjectStructureWorkflowPreviewSimulationOption> SimulationOptions,
    WorkflowRuntimeBackendKind PreferredBackend,
    WorkflowRuntimeBackendKind RequestedBackend,
    IReadOnlyList<ProjectStructureWorkflowStartBackendOption> BackendOptions,
    string BackendWarning);

/// <summary>
/// Choices for adding a workflow node under a parent node: the available workflow definitions, the preselected one,
/// the normalized input settings and a preview of the run input. Nothing is created.
/// </summary>
/// <param name="ProjectId">Identifier of the project.</param>
/// <param name="ParentNode">The parent node the workflow node would be created under.</param>
/// <param name="Workflows">Every workflow definition, active ones first and then by name.</param>
/// <param name="SelectedWorkflowId">Preselected workflow definition, a GUID string; null when none.</param>
/// <param name="SelectedVersionId">Preselected workflow version, a GUID string; null when none.</param>
/// <param name="InputSettings">The input settings after normalization.</param>
/// <param name="Preview">Preview of the run input with these settings.</param>
/// <param name="Warnings">Human-readable notes, for example that no workflow definitions are available.</param>
public sealed record ProjectStructureWorkflowAddOptionsResult(
    Guid ProjectId,
    ProjectStructureNodeSummary ParentNode,
    IReadOnlyList<ProjectStructureWorkflowDefinitionOption> Workflows,
    WorkflowId? SelectedWorkflowId,
    WorkflowVersionId? SelectedVersionId,
    ProjectStructureWorkflowInputSettings InputSettings,
    ProjectStructureWorkflowInputPreview Preview,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Workflow node start request. Send a new <c>intentId</c> for every intended launch and the same one to retry it:
/// a retry with the same intent returns the launch it created instead of starting another run.
/// </summary>
/// <param name="RequestedBackend">
/// Runtime backend to request, as a JSON integer: 0 InProcess, 1 DurableTask, 2 AzureFunctions; null uses the
/// definition's preferred backend. When the requested backend cannot be used, the result carries a warning.
/// </param>
/// <param name="RequestedBy">Display name of who requested the run, recorded with it. Defaults to
/// <c>project-structure</c>.</param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on this project, from <c>POST
/// /api/project-structure/leases/acquire</c>. When omitted, the operation uses the project lease the caller's identity
/// already holds, or else takes a five-minute project lease for its own duration, waiting for another identity's lease
/// that ends within 30 seconds (a longer one fails with HTTP 409 <c>LeaseConflict</c>); when sent, it must match the
/// caller's active project lease, otherwise HTTP 409 <c>LeaseMissing</c> or <c>LeaseConflict</c>.
/// </param>
/// <param name="SimulatedNodeIds">
/// Identifiers of workflow steps (workflow graph nodes, not structure nodes) to simulate instead of executing; null or
/// empty executes every step.
/// </param>
/// <param name="IntentId">
/// Caller-generated launch intent, a GUID; the empty GUID is rejected with HTTP 400 <c>WorkflowIntentRequired</c>. When
/// omitted, a new intent is generated, so repeating the request starts another run.
/// </param>
public sealed record ProjectStructureWorkflowNodeStartInput(
    WorkflowRuntimeBackendKind? RequestedBackend = null,
    string RequestedBy = "project-structure",
    string? LeaseToken = null,
    IReadOnlyList<string>? SimulatedNodeIds = null,
    Guid? IntentId = null);

/// <summary>One recorded event of a workflow run.</summary>
/// <param name="Kind">
/// Kind of event, as a JSON integer: 0 Started, 1 ExecutorInvoked, 2 ExecutorCompleted, 3 ExecutorFailed, 4 SuperStep,
/// 5 Output, 6 Warning, 7 Error, 8 WaitingForInput, 9 Completed, 10 Cancelled, 11 Unknown, 12 ProviderReadEvidence.
/// </param>
/// <param name="Message">Human-readable event message.</param>
/// <param name="NodeId">Identifier of the workflow step (workflow graph node) the event belongs to; may be
/// empty.</param>
/// <param name="CreatedAtUtc">Instant (with offset) the event was recorded.</param>
/// <param name="PayloadJson">Event payload as a JSON string that contains JSON; empty when there is none.</param>
public sealed record ProjectStructureWorkflowRunEventSummary(
    WorkflowEventKind Kind,
    string Message,
    string NodeId,
    DateTimeOffset CreatedAtUtc,
    string PayloadJson = "");

/// <summary>One artifact a workflow run produced.</summary>
/// <param name="Kind">
/// Kind of artifact, as a JSON integer: 0 Text, 1 Json, 2 File, 3 Image, 4 Binary, 5 ToolReceipt, 6 PreviewSimulation.
/// </param>
/// <param name="Name">Name of the artifact.</param>
/// <param name="ContentType">Content type of the artifact.</param>
/// <param name="StoragePath">Where the artifact is stored in the application's storage; empty when it is not
/// stored.</param>
/// <param name="Summary">Short description of the artifact.</param>
public sealed record ProjectStructureWorkflowRunArtifactSummary(
    WorkflowArtifactKind Kind,
    string Name,
    string ContentType,
    string StoragePath,
    string Summary);

/// <summary>What a workflow node's latest run has done so far: its state, steps, artifacts and created items.</summary>
/// <param name="RunId">Identifier of the run, a GUID string; null when the node has no run.</param>
/// <param name="State">
/// Run state, as a JSON integer: 0 NotStarted, 1 Running, 2 WaitingForInput, 3 Idle, 4 Completed, 5 Failed,
/// 6 Cancelled.
/// </param>
/// <param name="WorkflowName">Name of the workflow definition.</param>
/// <param name="RunSummary">Human-readable summary of the run.</param>
/// <param name="CurrentStepIndex">Index of the current step, from 0.</param>
/// <param name="StepCount">Number of steps of the workflow, at least 1.</param>
/// <param name="Artifacts">Artifacts the run produced, oldest first.</param>
/// <param name="CreatedNodeIds">Identifiers of structure nodes the run created, as in <c>nodes[].id</c>.</param>
/// <param name="CreatedAssetIds">Identifiers of assets the run created, as GUID strings.</param>
/// <param name="CreatedFilePaths">Storage paths of files the run created.</param>
public sealed record ProjectStructureWorkflowExecutionSummary(
    WorkflowRunId? RunId,
    WorkflowRunState State,
    string WorkflowName,
    string RunSummary,
    int CurrentStepIndex,
    int StepCount,
    IReadOnlyList<ProjectStructureWorkflowRunArtifactSummary> Artifacts,
    IReadOnlyList<string> CreatedNodeIds,
    IReadOnlyList<string> CreatedAssetIds,
    IReadOnlyList<string> CreatedFilePaths);

/// <summary>
/// Status snapshot of a workflow node's latest run, as shown on the node: run state, derived node status, progress and
/// marker, step position, message, execution summary and recent events. A snapshot, not a completion signal: poll
/// <c>GET /api/project-structure/projects/{projectId}/nodes/{nodeId}/workflow/status</c> until the state is final.
/// </summary>
/// <param name="RunId">Identifier of the run, a GUID string; null when the node has no run yet.</param>
/// <param name="State">
/// Run state, as a JSON integer: 0 NotStarted, 1 Running, 2 WaitingForInput, 3 Idle, 4 Completed, 5 Failed,
/// 6 Cancelled.
/// </param>
/// <param name="Status">Status text the node shows for this run state.</param>
/// <param name="ProgressMode">Progress mode the node shows for this run state.</param>
/// <param name="ProgressPercent">Progress from 0 through 100, derived from the run state and the current step.</param>
/// <param name="MarkerIcon">Marker icon token the node shows for this run state.</param>
/// <param name="MarkerTone">Marker tone token the node shows for this run state.</param>
/// <param name="MarkerLabel">Marker label the node shows for this run state.</param>
/// <param name="CurrentStepIndex">Index of the current step, from 0.</param>
/// <param name="StepCount">Number of steps of the workflow, at least 1.</param>
/// <param name="Message">Human-readable status message.</param>
/// <param name="Summary">Execution summary of the run.</param>
/// <param name="RecentEvents">The run's last 12 events at most, oldest first.</param>
public sealed record ProjectStructureWorkflowRunStatus(
    WorkflowRunId? RunId,
    WorkflowRunState State,
    string Status,
    string ProgressMode,
    int ProgressPercent,
    string MarkerIcon,
    string MarkerTone,
    string MarkerLabel,
    int CurrentStepIndex,
    int StepCount,
    string Message,
    ProjectStructureWorkflowExecutionSummary Summary,
    IReadOnlyList<ProjectStructureWorkflowRunEventSummary> RecentEvents) {
    /// <summary>
    /// How far the run's result has been delivered to the structure node, as a JSON integer: 0 Prepared, 1 Pending,
    /// 2 Applied, 3 Superseded, 4 TargetChanged, 5 TargetDeleted, 6 LegacyObservation (a run started before launch
    /// admissions were recorded), 7 AuthorityBlocked.
    /// </summary>
    public ProjectWorkflowDeliveryState Delivery { get; init; } = ProjectWorkflowDeliveryState.LegacyObservation;

    /// <summary>
    /// Launch intent of the run, a GUID; null for a run started before launch admissions were recorded.
    /// </summary>
    public Guid? IntentId { get; init; }

    /// <summary>
    /// Order number of the launch admission among the node's launches; null for a run started before launch admissions
    /// were recorded.
    /// </summary>
    public long? AdmissionSequence { get; init; }
}

/// <summary>
/// Result of an admitted workflow node launch: the run reserved for the launch intent and its current status. It
/// means the launch was admitted, not that the run finished; poll the node's workflow status. When the launch was
/// admitted but a later step failed, the result still reports the retained run, with <c>status.delivery</c> Pending
/// and a warning that it needs reconciliation.
/// </summary>
/// <param name="ProjectId">Identifier of the project.</param>
/// <param name="NodeId">Identifier of the workflow node, as in <c>nodes[].id</c>.</param>
/// <param name="WorkflowId">Identifier of the workflow definition, a GUID string.</param>
/// <param name="WorkflowVersionId">Workflow version that runs, a GUID string.</param>
/// <param name="RunId">Identifier of the run reserved for this launch, a GUID string.</param>
/// <param name="Route">Application route that shows the run in the web UI; informational.</param>
/// <param name="Status">Status snapshot of the run at the time of the response.</param>
/// <param name="Warnings">
/// Human-readable notes, for example that the request had no <c>intentId</c> or that an observer failed after
/// admission.
/// </param>
public sealed record ProjectStructureWorkflowNodeStartResult(
    Guid ProjectId,
    string NodeId,
    WorkflowId WorkflowId,
    WorkflowVersionId WorkflowVersionId,
    WorkflowRunId RunId,
    string Route,
    ProjectStructureWorkflowRunStatus Status,
    IReadOnlyList<string> Warnings) {
    /// <summary>Launch intent of this start, a GUID: the one sent, or the one generated when none was sent.</summary>
    public Guid IntentId { get; init; }

    /// <summary>True when the caller sent the intent; false when the owner generated it.</summary>
    public bool CallerSuppliedIntent { get; init; }

    /// <summary>
    /// True when the run could be read back after the launch; false when its admission awaits observation.
    /// </summary>
    public bool RunAdmissionObserved { get; init; }
    [JsonIgnore]
    public Exception? ObservationException { get; init; }
    [JsonIgnore]
    public Exception? ReceiptObservationException { get; init; }
}

/// <summary>Command to resolve for the node named in the route.</summary>
/// <param name="CommandKind">
/// Command, as a JSON integer: 0 Open and 1 Wizard (the node's page; for prompt nodes their Prompt Gallery prompt,
/// created when missing), 2 Branch, 4 Skip and 5 MarkUsed (the same result as Open, without creating a prompt), 3 Test
/// (the project's Test Lab).
/// </param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on this project, from <c>POST
/// /api/project-structure/leases/acquire</c>. When omitted, the operation uses the project lease the caller's identity
/// already holds, or else takes a five-minute project lease for its own duration, waiting for another identity's lease
/// that ends within 30 seconds (a longer one fails with HTTP 409 <c>LeaseConflict</c>); when sent, it must match the
/// caller's active project lease, otherwise HTTP 409 <c>LeaseMissing</c> or <c>LeaseConflict</c>.
/// </param>
public sealed record ProjectStructureNodeCommandInput(
    ProjectStructureCommandKind CommandKind,
    string? LeaseToken = null);

/// <summary>
/// Transfer of a node's descendants into another existing project, which does not have to be a subproject. The node
/// itself stays in the source project; its former children become children of the target project's root.
/// </summary>
/// <param name="TargetProjectId">Identifier of the project that receives the descendants; required and different from
/// the source.</param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on the source project. When omitted, the operation uses the
/// lease the caller's identity already holds on it, or else takes a five-minute lease on it for its own duration,
/// waiting for another identity's lease that ends within 30 seconds (a longer one fails with HTTP 409
/// <c>LeaseConflict</c>); when sent, it must match the caller's active lease on the source project, otherwise HTTP 409
/// <c>LeaseMissing</c> or <c>LeaseConflict</c>.
/// </param>
/// <param name="TargetLeaseToken">
/// Optional token of the caller's active Project lease on the target project, handled like <c>leaseToken</c>.
/// </param>
public sealed record ProjectStructureSubtreeTransferInput(
    Guid TargetProjectId,
    string? LeaseToken = null,
    string? TargetLeaseToken = null);

/// <summary>
/// Extraction of selected nodes into a new subproject of the route's project: the owner creates the project and moves
/// the nodes into its structure.
/// </summary>
/// <param name="Name">Name of the new subproject; required, not blank.</param>
/// <param name="NodeIds">
/// Identifiers of the nodes to move, as returned in <c>nodes[].id</c>; at least one. Blank and duplicate entries are
/// removed. Identifiers that are not editable nodes of the source project (unknown, project or synced nodes) are
/// ignored with a warning.
/// </param>
/// <param name="Description">Description of the new subproject; blank generates one that names the source
/// project.</param>
/// <param name="Objective">Objective of the new subproject; blank generates one.</param>
/// <param name="CurrentPhase">Current phase of the new subproject; blank means Execution.</param>
/// <param name="Status">
/// Status of the new subproject, as a JSON integer: 0 Draft, 1 Active, 2 OnHold, 3 Completed, 4 Archived. Defaults to
/// Active.
/// </param>
/// <param name="IncludeDescendants">
/// True (the default) to move each selected node with all its descendants; false moves only the selected nodes.
/// </param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on the source project. When omitted, the operation uses the
/// lease the caller's identity already holds on it, or else takes a five-minute lease on it for its own duration,
/// waiting for another identity's lease that ends within 30 seconds (a longer one fails with HTTP 409
/// <c>LeaseConflict</c>); when sent, it must match the caller's active lease on the source project, otherwise HTTP 409
/// <c>LeaseMissing</c> or <c>LeaseConflict</c>.
/// </param>
public sealed record ProjectStructureNodesToSubprojectInput(
    string Name,
    IReadOnlyList<string> NodeIds,
    string? Description = null,
    string? Objective = null,
    string? CurrentPhase = null,
    ProjectStatus Status = ProjectStatus.Active,
    bool IncludeDescendants = true,
    string? LeaseToken = null);

/// <summary>Result of a committed extraction of nodes into a new subproject.</summary>
/// <param name="SourceProjectId">Identifier of the project the nodes came from.</param>
/// <param name="TargetProjectId">Identifier of the new subproject.</param>
/// <param name="TargetProjectName">Name of the new subproject.</param>
/// <param name="RequestedNodeIds">The node identifiers that were requested, after removing blanks and
/// duplicates.</param>
/// <param name="MovedNodeIds">
/// Identifiers of every node that moved, sorted ordinally; node identifiers do not change when nodes move.
/// </param>
/// <param name="MovedRootCount">
/// Number of moved nodes whose parent did not move; they are now children of the new subproject's root.
/// </param>
/// <param name="MovedLinkCount">Number of links between moved nodes; they moved with the nodes.</param>
/// <param name="RemovedBoundaryLinks">
/// Links that were deleted because one end moved and the other stayed.
/// </param>
/// <param name="Warnings">
/// Human-readable notes, for example about ignored identifiers or removed boundary links.
/// </param>
public sealed record ProjectStructureNodesToSubprojectResult(
    Guid SourceProjectId,
    Guid TargetProjectId,
    string TargetProjectName,
    IReadOnlyList<string> RequestedNodeIds,
    IReadOnlyList<string> MovedNodeIds,
    int MovedRootCount,
    int MovedLinkCount,
    IReadOnlyList<ProjectStructureBoundaryLinkRemoval> RemovedBoundaryLinks,
    IReadOnlyList<string> Warnings)
{
    /// <summary>Number of nodes that moved, equal to the number of <c>movedNodeIds</c>.</summary>
    public int MovedNodeCount => MovedNodeIds.Count;
}

/// <summary>
/// Asset creation request: a new File, ImageAsset or VideoAsset node under an explicit parent, with its content taken
/// from exactly one source, in this order of precedence: <c>media</c> (inline base64), <c>sourceWorkspacePath</c> (a
/// file in the server's managed workspace) or <c>sourceUrl</c> (a download from a public web address). The content is
/// stored as a managed file of the project.
/// </summary>
/// <param name="ObjectType">Asset type: File, ImageAsset or VideoAsset; other types are rejected.</param>
/// <param name="Title">Title of the new asset node.</param>
/// <param name="Subtitle">Secondary display text; may be empty.</param>
/// <param name="Notes">Free-text notes; may be empty.</param>
/// <param name="Media">
/// Inline file content. When sent, <c>sourceWorkspacePath</c> and <c>sourceUrl</c> are ignored.
/// </param>
/// <param name="ParentNodeKey">
/// Identifier of the parent node, as returned in <c>nodes[].id</c>; required (use <c>project:{projectId}</c> for the
/// project root). A missing value is rejected with HTTP 400 <c>AssetParentRequired</c>.
/// </param>
/// <param name="ObjectSubtype">
/// Lower-case subtype, for example <c>pdf</c>, <c>markdown</c> or <c>mermaid</c> for a File; known synonyms are
/// normalized.
/// </param>
/// <param name="MetadataJson">Optional metadata as a JSON string that contains a JSON object.</param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on this project, from <c>POST
/// /api/project-structure/leases/acquire</c>. When omitted, the operation uses the project lease the caller's identity
/// already holds, or else takes a five-minute project lease for its own duration, waiting for another identity's lease
/// that ends within 30 seconds (a longer one fails with HTTP 409 <c>LeaseConflict</c>); when sent, it must match the
/// caller's active project lease, otherwise HTTP 409 <c>LeaseMissing</c> or <c>LeaseConflict</c>.
/// </param>
/// <param name="SourceWorkspacePath">
/// Used when <c>media</c> is absent: path, relative to the server's managed workspace, of an existing file to copy
/// into the asset, without <c>.</c> or <c>..</c> segments. A path outside the workspace is rejected with HTTP 400
/// <c>SourceWorkspacePathInvalid</c>, a path under another project's managed folder with HTTP 403
/// <c>SourceWorkspaceScopeDenied</c>, and a missing file with HTTP 404 <c>SourceWorkspaceFileNotFound</c>. An http
/// or https URL here is downloaded like <c>sourceUrl</c>.
/// </param>
/// <param name="SourceFileName">
/// File name to store for a workspace or URL source; when omitted, the name is taken from the source.
/// </param>
/// <param name="SourceContentType">
/// Media type to store for a workspace or URL source, for example <c>application/pdf</c>; when omitted, it is derived
/// from the file name.
/// </param>
/// <param name="SourceUrl">
/// Used when <c>media</c> and <c>sourceWorkspacePath</c> are absent: absolute http or https URL of a public host to
/// download the content from, without embedded credentials. At most 5 redirects, 60 seconds and 25 MiB are allowed.
/// </param>
public sealed record ProjectStructureAssetCreateInput(
    ProjectObjectType ObjectType,
    string Title,
    string Subtitle,
    string Notes,
    ProjectObjectMediaPayload? Media,
    string? ParentNodeKey = null,
    string? ObjectSubtype = null,
    string? MetadataJson = null,
    string? LeaseToken = null,
    string? SourceWorkspacePath = null,
    string? SourceFileName = null,
    string? SourceContentType = null,
    string? SourceUrl = null);

public sealed record ProjectStructureAgentAssetCreateInput(
    ProjectObjectType ObjectType,
    string Title,
    string Subtitle = "",
    string Notes = "",
    ProjectObjectMediaPayload? Media = null,
    [property: JsonRequired] string? ParentNodeKey = null,
    string? ObjectSubtype = null,
    string? LeaseToken = null,
    string? SourceWorkspacePath = null,
    string? SourceFileName = null,
    string? SourceContentType = null,
    string? SourceUrl = null)
{
    public ProjectStructureAssetCreateInput ToServiceRequest()
    {
        return new ProjectStructureAssetCreateInput(
            ObjectType,
            Title,
            Subtitle,
            Notes,
            Media,
            ParentNodeKey,
            ObjectSubtype,
            MetadataJson: null,
            LeaseToken,
            SourceWorkspacePath,
            SourceFileName,
            SourceContentType,
            SourceUrl);
    }
}

/// <summary>
/// Approval request to record in a project structure: a Decision node with subtype <c>approval-request</c> that
/// describes an operation waiting for someone's approval. Creating it blocks or starts nothing by itself.
/// </summary>
/// <param name="Title">Title of the request; required, not blank.</param>
/// <param name="Subtitle">Secondary display text; may be empty.</param>
/// <param name="Notes">Free-text details for the approver; may be empty.</param>
/// <param name="RequestedOperation">
/// Description of the operation that needs approval; required, not blank. It is stored in the generated metadata.
/// </param>
/// <param name="ParentNodeKey">
/// Identifier of the parent node, as returned in <c>nodes[].id</c>; null or blank places the request under the project
/// root.
/// </param>
/// <param name="EstimatedMinutes">
/// Optional estimate, in minutes, of how long the requested operation takes; stored in the generated metadata without
/// validation.
/// </param>
/// <param name="MetadataJson">
/// Optional metadata as a JSON string that contains a JSON object. When sent, it is stored instead of the generated
/// <c>approvalRequest</c> metadata (requested operation, estimated minutes, requesting identity and time), so those
/// values are then not recorded.
/// </param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on this project, from <c>POST
/// /api/project-structure/leases/acquire</c>. When omitted, the operation uses the project lease the caller's identity
/// already holds, or else takes a five-minute project lease for its own duration, waiting for another identity's lease
/// that ends within 30 seconds (a longer one fails with HTTP 409 <c>LeaseConflict</c>); when sent, it must match the
/// caller's active project lease, otherwise HTTP 409 <c>LeaseMissing</c> or <c>LeaseConflict</c>.
/// </param>
public sealed record ProjectStructureApprovalRequestCreateInput(
    string Title,
    string Subtitle,
    string Notes,
    string RequestedOperation,
    string? ParentNodeKey = null,
    int? EstimatedMinutes = null,
    string? MetadataJson = null,
    string? LeaseToken = null);

/// <summary>
/// Metadata of a stored asset node (File, ImageAsset or VideoAsset): what it is, where its file is stored and which
/// asset it revises. It does not contain the file content and does not prove that the content is still readable; read
/// the content with <c>GET /api/project-structure/projects/{projectId}/assets/{nodeId}/content</c>.
/// </summary>
/// <param name="ProjectId">Identifier of the project that contains the asset.</param>
/// <param name="NodeId">Identifier of the asset node, as in <c>nodes[].id</c>.</param>
/// <param name="ObjectType">Asset type: File, ImageAsset or VideoAsset.</param>
/// <param name="ObjectSubtype">Lower-case subtype, for example <c>pdf</c>; empty when none.</param>
/// <param name="Title">Title of the asset node.</param>
/// <param name="Subtitle">Secondary display text; may be empty.</param>
/// <param name="Route">Application route that shows the asset in the web UI; informational.</param>
/// <param name="MediaRelativePath">
/// Location of the stored file within the application's managed storage; empty for content held only by a storage
/// provider reference. Not a path on the client's machine.
/// </param>
/// <param name="MediaContentType">Content type recorded for the file, for example <c>image/png</c>.</param>
/// <param name="MediaOriginalFileName">Original file name recorded for the file.</param>
/// <param name="MetadataJson">
/// Asset metadata as a JSON string that contains a JSON object; <c>"{}"</c> when there is none.
/// </param>
/// <param name="IsReadonly">
/// Always true: stored asset content is never changed in place; create a revision instead.
/// </param>
/// <param name="RevisionParentNodeId">
/// Identifier of the asset this one revises (its parent node, linked with DerivedFrom); null when it is not a revision.
/// </param>
public sealed record ProjectStructureAssetDescriptor(
    Guid ProjectId,
    string NodeId,
    ProjectObjectType ObjectType,
    string ObjectSubtype,
    string Title,
    string Subtitle,
    string Route,
    string MediaRelativePath,
    string MediaContentType,
    string MediaOriginalFileName,
    string MetadataJson,
    bool IsReadonly,
    string? RevisionParentNodeId) {
    /// <summary>
    /// Receipt that ties the asset to the process step that created it. Only process runs executing inside the
    /// application set it; the Project Structure HTTP API omits this member.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProjectProcessAssetReceiptObservation? ProcessAssetReceipt { get; init; }
}

/// <summary>
/// Content of a stored asset: its metadata and the complete file content as base64 text.
/// </summary>
/// <param name="Asset">Metadata of the asset whose content was read.</param>
/// <param name="ContentLength">Size of the file content in bytes (before base64 encoding).</param>
/// <param name="Base64Data">The complete file content, base64-encoded (standard alphabet, with padding).</param>
public sealed record ProjectStructureAssetContentDescriptor(
    ProjectStructureAssetDescriptor Asset,
    long ContentLength,
    string Base64Data)
{
    /// <summary>
    /// True when <c>base64Data</c> was left out on purpose. Always false in this API, which returns the complete
    /// content.
    /// </summary>
    public bool Base64DataOmitted { get; init; }

    /// <summary>
    /// Short description of the content used when <c>base64Data</c> is left out; always empty in this API.
    /// </summary>
    public string ContentSummary { get; init; } = string.Empty;
}

internal sealed record ProjectStructureAssetBinaryContent(
    ProjectStructureAssetDescriptor Asset,
    byte[] Bytes);

public sealed record ProjectStructureAssetTextDescriptor(
    ProjectStructureAssetDescriptor Asset,
    long ContentLength,
    int CharacterCount,
    string TextContent,
    bool IsTruncated);

public sealed record ProjectStructureAssetImageAnalysisDescriptor(
    ProjectStructureAssetDescriptor Asset,
    string Model,
    string Analysis,
    int InputTokens,
    int OutputTokens);

/// <summary>
/// Asset revision request: new content for an existing asset, stored as a new asset node of the same type under the
/// original and linked to it with DerivedFrom. The original asset and its content stay unchanged.
/// </summary>
/// <param name="Title">Title of the new revision node.</param>
/// <param name="Subtitle">Secondary display text; may be empty.</param>
/// <param name="Notes">Free-text notes; may be empty.</param>
/// <param name="Media">
/// Inline content of the revision; always send it. A null value is not rejected up front: the revision node is created
/// without content and the request then fails with HTTP 400 <c>AssetRequired</c>, leaving that node in the structure.
/// </param>
/// <param name="ObjectSubtype">
/// Lower-case subtype of the revision; null or blank keeps the original's subtype.
/// </param>
/// <param name="MetadataJson">
/// Metadata of the revision as a JSON string that contains a JSON object; null or blank copies the original's metadata.
/// </param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on this project, from <c>POST
/// /api/project-structure/leases/acquire</c>. When omitted, the operation uses the project lease the caller's identity
/// already holds, or else takes a five-minute project lease for its own duration, waiting for another identity's lease
/// that ends within 30 seconds (a longer one fails with HTTP 409 <c>LeaseConflict</c>); when sent, it must match the
/// caller's active project lease, otherwise HTTP 409 <c>LeaseMissing</c> or <c>LeaseConflict</c>.
/// </param>
public sealed record ProjectStructureAssetRevisionRequest(
    string Title,
    string Subtitle,
    string Notes,
    ProjectObjectMediaPayload Media,
    string? ObjectSubtype = null,
    string? MetadataJson = null,
    string? LeaseToken = null);

public sealed record ProjectStructureAgentAssetRevisionRequest(
    string Title,
    string Subtitle,
    string Notes,
    ProjectObjectMediaPayload Media,
    string? ObjectSubtype = null,
    string? LeaseToken = null)
{
    public ProjectStructureAssetRevisionRequest ToServiceRequest()
    {
        return new ProjectStructureAssetRevisionRequest(
            Title,
            Subtitle,
            Notes,
            Media,
            ObjectSubtype,
            MetadataJson: null,
            LeaseToken);
    }
}

/// <summary>
/// Checklist query: filters for the list of open work in a project. Every member is optional; the empty object
/// <c>{}</c> returns every unfinished node except project nodes (the project root, subprojects and parent projects)
/// and paused or stopped nodes, most urgent first.
/// </summary>
/// <param name="MaxPriority">
/// Keep only items whose effective priority is from 1 through this value (1 is the most urgent); items without a
/// priority (0) are dropped. Null applies no priority filter.
/// </param>
/// <param name="ObjectTypes">
/// Object types to keep, each a case-insensitive object type symbol or its integer; null or empty keeps every type.
/// </param>
/// <param name="IncludePaused">
/// True to also return paused or stopped nodes: nodes whose status contains paused, on hold, onhold, stopped or cancel,
/// or whose marker label contains pause, hold, stop or wait. Defaults to false.
/// </param>
/// <param name="Take">
/// Maximum number of items, applied after ordering; a cut result adds a warning. A value below 1 is treated as 1. Null
/// returns every item.
/// </param>
public sealed record ProjectStructureChecklistRequest(
    int? MaxPriority = null,
    IReadOnlyList<ProjectObjectType>? ObjectTypes = null,
    bool IncludePaused = false,
    int? Take = null);

/// <summary>One unfinished prerequisite of a checklist item.</summary>
/// <param name="NodeId">Identifier of the prerequisite node, as in <c>nodes[].id</c> of the structure read.</param>
/// <param name="Title">Title of the prerequisite node.</param>
/// <param name="Status">Free-text status of the prerequisite node.</param>
/// <param name="EffectivePriority">Effective priority of the prerequisite node, from 0 (none) through 6.</param>
/// <param name="Reason">
/// Why it is a prerequisite: <c>parent</c> (an unfinished ancestor below the project root), <c>depends-on</c> (the item
/// has a DependsOn link to it) or <c>blocked-by</c> (it has a Blocks link to the item).
/// </param>
public sealed record ProjectStructureChecklistPrerequisite(
    string NodeId,
    string Title,
    string Status,
    int EffectivePriority,
    string Reason);

/// <summary>
/// One open item of a project checklist: an unfinished node with its status, progress, priority and unfinished
/// prerequisites.
/// </summary>
/// <param name="NodeId">Identifier of the node, as in <c>nodes[].id</c> of the structure read.</param>
/// <param name="ParentNodeId">Identifier of the node's parent; null for a top-level node.</param>
/// <param name="ObjectType">Kind of project object the node represents.</param>
/// <param name="ObjectSubtype">Lower-case subtype of the node; empty when none.</param>
/// <param name="Title">Title of the node.</param>
/// <param name="Status">Free-text status of the node.</param>
/// <param name="ProgressMode">
/// How progress is expressed: <c>complete</c>, <c>started</c>, <c>progress</c>, <c>na</c>, or empty when not tracked.
/// </param>
/// <param name="ProgressPercent">Progress from 0 through 100, or -1 when not tracked.</param>
/// <param name="MarkerLabel">Label of the node's primary marker; empty when none.</param>
/// <param name="Priority">Priority set on the node itself: 0 means none; smaller positive values are more
/// urgent.</param>
/// <param name="EffectivePriority">
/// Priority used for ordering, from 0 (none) through 6, including the most urgent priority of unfinished descendants.
/// </param>
/// <param name="Route">Application route that shows the node in the web UI; informational.</param>
/// <param name="Prerequisites">Prerequisites of the node that are not finished yet; empty when it can start.</param>
public sealed record ProjectStructureChecklistItem(
    string NodeId,
    string? ParentNodeId,
    ProjectObjectType ObjectType,
    string ObjectSubtype,
    string Title,
    string Status,
    string ProgressMode,
    int ProgressPercent,
    string MarkerLabel,
    int Priority,
    int EffectivePriority,
    string Route,
    IReadOnlyList<ProjectStructureChecklistPrerequisite> Prerequisites);

/// <summary>
/// Checklist of a project: its unfinished nodes ordered by effective priority (nodes without a priority last) and then
/// by title.
/// </summary>
/// <param name="ProjectId">Identifier of the project.</param>
/// <param name="ProjectName">Name of the project at the time of the query.</param>
/// <param name="Items">Open items after filtering; empty when nothing is open.</param>
/// <param name="Warnings">Human-readable notes, for example that the list was cut to <c>take</c> items.</param>
public sealed record ProjectStructureChecklistResponse(
    Guid ProjectId,
    string ProjectName,
    IReadOnlyList<ProjectStructureChecklistItem> Items,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Dependency query: which nodes to analyze and how to fill missing durations. Every member is optional; the empty
/// object <c>{}</c> analyzes every node of the project, finished ones included.
/// </summary>
/// <param name="NodeIds">
/// Node identifiers to return, as in <c>nodes[].id</c>, matched exactly; blank entries are ignored. Null or omitted
/// returns every node; an empty array (or one with only blank entries) returns no items.
/// </param>
/// <param name="IncludeFinished">
/// True (the default) to include finished nodes; false drops nodes with progress mode <c>complete</c> or progress 100,
/// or whose status contains done, complete, approved, ready, final or archived.
/// </param>
/// <param name="DefaultDurationSeconds">
/// Duration in seconds assumed for nodes without a stored duration or planned interval; null, zero or negative means
/// 3600 (one hour).
/// </param>
/// <param name="Take">
/// Maximum number of items, applied after ordering by title; a cut result adds a warning. A value below 1 is treated
/// as 1. Null returns every item.
/// </param>
public sealed record ProjectStructureDependencyQueryRequest(
    IReadOnlyList<string>? NodeIds = null,
    bool IncludeFinished = true,
    int? DefaultDurationSeconds = null,
    int? Take = null);

/// <summary>One node related to an analyzed node as its prerequisite or its dependent.</summary>
/// <param name="NodeId">Identifier of the related node, as in <c>nodes[].id</c> of the structure read.</param>
/// <param name="Title">Title of the related node.</param>
/// <param name="Status">Free-text status of the related node.</param>
/// <param name="EffectivePriority">Effective priority of the related node, from 0 (none) through 6.</param>
/// <param name="IsFinished">True when the related node counts as finished.</param>
/// <param name="Reason">
/// How the nodes are related. Prerequisites: <c>parent</c> (an ancestor below the project root), <c>depends-on</c> (the
/// analyzed node has a DependsOn link to it) or <c>blocked-by</c> (it has a Blocks link to the analyzed node).
/// Dependents: <c>required-for</c> (it has a DependsOn link to the analyzed node) or <c>blocks</c> (the analyzed node
/// has a Blocks link to it).
/// </param>
public sealed record ProjectStructureDependencyRelationSummary(
    string NodeId,
    string Title,
    string Status,
    int EffectivePriority,
    bool IsFinished,
    string Reason);

/// <summary>
/// Dependency analysis of one node: its state, whether it can start now, its duration and its prerequisites and
/// dependents.
/// </summary>
/// <param name="NodeId">Identifier of the node, as in <c>nodes[].id</c> of the structure read.</param>
/// <param name="ParentNodeId">Identifier of the node's parent; null for a top-level node.</param>
/// <param name="ObjectType">Kind of project object the node represents.</param>
/// <param name="ObjectSubtype">Lower-case subtype of the node; empty when none.</param>
/// <param name="Title">Title of the node.</param>
/// <param name="Status">Free-text status of the node.</param>
/// <param name="ProgressMode">
/// How progress is expressed: <c>complete</c>, <c>started</c>, <c>progress</c>, <c>na</c>, or empty when not tracked.
/// </param>
/// <param name="ProgressPercent">Progress from 0 through 100, or -1 when not tracked.</param>
/// <param name="MarkerLabel">Label of the node's primary marker; empty when none.</param>
/// <param name="Priority">Priority set on the node itself: 0 means none; smaller positive values are more
/// urgent.</param>
/// <param name="EffectivePriority">Priority used for ordering, from 0 (none) through 6.</param>
/// <param name="IsFinished">
/// True when the node counts as finished: progress mode <c>complete</c> or progress 100, or a status containing done,
/// complete, approved, ready, final or archived.
/// </param>
/// <param name="IsPausedOrStopped">
/// True when the status contains paused, on hold, onhold, stopped or cancel, or the marker label contains pause, hold,
/// stop or wait.
/// </param>
/// <param name="CanExecute">
/// True when the node is neither finished nor paused or stopped and every prerequisite, including its ancestors below
/// the project root, is finished.
/// </param>
/// <param name="DurationSeconds">
/// Known duration in seconds: the stored duration, or else the length of the planned interval (at least 1); null when
/// neither exists.
/// </param>
/// <param name="EffectiveDurationSeconds">
/// Duration used for planning: <c>durationSeconds</c>, or the response's <c>defaultDurationSeconds</c> when it is null.
/// </param>
/// <param name="StartUtc">Planned start as an instant with offset; null when not scheduled.</param>
/// <param name="EndUtc">Planned end as an instant with offset; null when not scheduled.</param>
/// <param name="Route">Application route that shows the node in the web UI; informational.</param>
/// <param name="Prerequisites">Nodes the analyzed node waits for, finished or not.</param>
/// <param name="Dependents">Nodes that wait for the analyzed node.</param>
public sealed record ProjectStructureDependencyItem(
    string NodeId,
    string? ParentNodeId,
    ProjectObjectType ObjectType,
    string ObjectSubtype,
    string Title,
    string Status,
    string ProgressMode,
    int ProgressPercent,
    string MarkerLabel,
    int Priority,
    int EffectivePriority,
    bool IsFinished,
    bool IsPausedOrStopped,
    bool CanExecute,
    int? DurationSeconds,
    int EffectiveDurationSeconds,
    DateTimeOffset? StartUtc,
    DateTimeOffset? EndUtc,
    string Route,
    IReadOnlyList<ProjectStructureDependencyRelationSummary> Prerequisites,
    IReadOnlyList<ProjectStructureDependencyRelationSummary> Dependents);

/// <summary>
/// Dependency analysis of a project: the selected nodes ordered by title, each with its prerequisites, dependents and
/// durations.
/// </summary>
/// <param name="ProjectId">Identifier of the project.</param>
/// <param name="ProjectName">Name of the project at the time of the query.</param>
/// <param name="DefaultDurationSeconds">Duration in seconds assumed for nodes without a known duration.</param>
/// <param name="Items">Analyzed nodes; empty when nothing matched.</param>
/// <param name="Warnings">Human-readable notes, for example that the result was cut to <c>take</c> nodes.</param>
public sealed record ProjectStructureDependencyResponse(
    Guid ProjectId,
    string ProjectName,
    int DefaultDurationSeconds,
    IReadOnlyList<ProjectStructureDependencyItem> Items,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Process start request for a project structure node: which process definition to launch for the node and how.
/// Every member is optional.
/// </summary>
/// <param name="ProcessDefinitionId">
/// Process definition to launch, a GUID; null uses the definition the node represents (a
/// <c>process-definition:</c> node) or the one linked from the node with
/// <c>POST /api/project-structure/projects/{projectId}/nodes/{nodeId}/process-definition</c>.
/// </param>
/// <param name="RunHrMatch">
/// True (the default) to let readiness findings, such as missing staffing matches, block the launch; blocking
/// assignment and mandatory findings block it either way.
/// </param>
/// <param name="Execute">
/// True to also queue the created run for execution by the process runtime; false (the default) creates the run
/// without dispatching its steps.
/// </param>
/// <param name="IncludeLaunchPlan">True to return the prepared launch plan in <c>launchPlan</c>; defaults to
/// false.</param>
/// <param name="RequestedBy">
/// Display name of who requested the launch, recorded with the run. Defaults to <c>project-structure-api</c>.
/// </param>
/// <param name="LeaseToken">Not used by this operation; process starts do not take a project lease.</param>
public sealed record ProjectStructureProcessNodeStartInput(
    Guid? ProcessDefinitionId = null,
    bool RunHrMatch = true,
    bool Execute = false,
    bool IncludeLaunchPlan = false,
    string RequestedBy = "project-structure-api",
    string? LeaseToken = null);

/// <summary>
/// Result of a process launch for a project structure node. The launch is accepted, not finished: follow the run with
/// <c>GET /api/processes/runs/{runId}</c>.
/// </summary>
/// <param name="ProjectId">Identifier of the project.</param>
/// <param name="NodeId">Identifier of the node the start was requested for, as in <c>nodes[].id</c>.</param>
/// <param name="ProcessDefinitionId">Identifier of the process definition that was launched.</param>
/// <param name="LaunchPlanId">Identifier of the prepared launch plan.</param>
/// <param name="RunId">Identifier of the created process run; null when no run was created, for example when
/// blocked.</param>
/// <param name="Stage">
/// Launch stage as text: <c>Planned</c>, <c>Blocked</c> (readiness findings stopped the launch), <c>Running</c>,
/// <c>Completed</c> or <c>Failed</c>.
/// </param>
/// <param name="Route">Application route of the live process view; informational.</param>
/// <param name="LaunchPlan">
/// The prepared launch plan as a JSON object, only when <c>includeLaunchPlan</c> was true; otherwise null.
/// </param>
/// <param name="Warnings">
/// Human-readable notes from the launch, for example that the run could not be linked back to the node.
/// </param>
public sealed record ProjectStructureProcessNodeStartResult(
    Guid ProjectId,
    string NodeId,
    Guid ProcessDefinitionId,
    Guid LaunchPlanId,
    Guid? RunId,
    string Stage,
    string Route,
    object? LaunchPlan,
    IReadOnlyList<string> Warnings) : IAgentToolOwnerObservationEvidence {
    /// <summary>
    /// Durable launch observation used by process launches inside the application; null for launches through this HTTP
    /// API.
    /// </summary>
    public ProcessLaunchObservation? Observation { get; init; }

    [JsonIgnore]
    public bool RequiresOwnerReconciliation => Observation?.ContinuationState == ProcessLaunchContinuationState.ReconciliationRequired;
}

public sealed record ProjectStructureProcessSubprocessLaunchInput(
    string DefinitionKey,
    string? LiveRunProfileKey = null,
    string? ParentProjectNodeId = null,
    IReadOnlyDictionary<string, object?>? Variables = null,
    bool RunHrMatch = true,
    bool Execute = true,
    bool IncludeLaunchPlan = true,
    string RequestedBy = "project-structure-process",
    string? LeaseToken = null);

public sealed record ProjectStructureProcessSubprocessLaunchResult(
    Guid ProjectId,
    string ProjectNodeId,
    string ParentProcessRunId,
    string ParentProcessStepId,
    string ParentProcessStepKey,
    string DefinitionKey,
    Guid ProcessDefinitionId,
    Guid LaunchPlanId,
    Guid? RunId,
    string Stage,
    string Route,
    object? LaunchPlan,
    string ChildManagedArtifactRoot,
    string ChildStepsArtifactRoot,
    string ChildLiveProcessesRoute,
    IReadOnlyList<string> ExpectedChildEvidenceRefs,
    IReadOnlyList<string> Warnings)
{
    public string ParentDeferredOutcomeInstruction { get; init; } = string.Empty;

    public string ParentDeferredOutcomeJson { get; init; } = string.Empty;
}

/// <summary>
/// Format of an imported outline, as a JSON integer. 0 Mermaid: <c>mindmap</c> text, where indentation gives the
/// hierarchy, or <c>flowchart</c> or <c>graph</c> text, where every <c>A --> B</c> line adds items A and B side by side
/// and a DependsOn link from A to B (other lines are skipped with a warning). 1 DocxOutline: a Word .docx file whose
/// Heading 1, Heading 2 and deeper heading paragraphs give the hierarchy; other non-empty paragraphs also become items.
/// 2 XmindMap: an XMind file (its content.json or content.xml topics). 3 JsonOutline: a JSON object or array of
/// objects, each with <c>title</c> (or <c>name</c>), optional <c>notes</c> and optional <c>children</c>.
/// </summary>
public enum ProjectStructureImportSourceKind
{
    Mermaid,
    DocxOutline,
    XmindMap,
    JsonOutline
}

/// <summary>
/// Outline import request: the target project, where to put the imported nodes and the source outline. The import
/// creates a container ProjectBlock, stores the source file in it when one is sent, and creates one node per outline
/// item under the container: items with children become ProjectBlock nodes and leaves become WorkItem nodes.
/// </summary>
/// <param name="ProjectId">Identifier of the project to import into; required.</param>
/// <param name="ParentNodeKey">
/// Identifier of an existing node that receives the container, as returned in <c>nodes[].id</c>; null or blank uses the
/// project root.
/// </param>
/// <param name="SourceKind">
/// Format of the source, as a JSON integer: 0 Mermaid, 1 DocxOutline, 2 XmindMap, 3 JsonOutline.
/// </param>
/// <param name="Title">Title of the container block that holds the import; required, not blank.</param>
/// <param name="SourceText">
/// Source text for Mermaid and JSON outlines; when blank, <c>sourceAsset</c> is decoded as UTF-8 text instead. Ignored
/// for DOCX and XMind sources.
/// </param>
/// <param name="SourceAsset">
/// Source file as inline base64, required for DOCX and XMind sources and optional for the others. When sent, it is
/// also stored as a File node in the container.
/// </param>
/// <param name="ContainerBlockSubtype">
/// ProjectBlock subtype for the container and for items with children. Defaults to <c>delivery</c> when omitted.
/// </param>
/// <param name="LeafWorkItemSubtype">
/// WorkItem subtype for leaf items. Defaults to <c>task</c> when omitted, which makes every leaf a canonical task.
/// </param>
/// <param name="LeaseToken">
/// Optional token of the caller's active Project lease on the target project, from <c>POST
/// /api/project-structure/leases/acquire</c>. When omitted, the import uses the project lease the caller's identity
/// already holds, or else takes a five-minute project lease for its own duration, waiting for another identity's lease
/// that ends within 30 seconds (a longer one fails with HTTP 409 <c>LeaseConflict</c>); when sent, it must match the
/// caller's active project lease, otherwise HTTP 409 <c>LeaseMissing</c> or <c>LeaseConflict</c>.
/// </param>
public sealed record ProjectStructureImportRequest(
    Guid ProjectId,
    string? ParentNodeKey,
    ProjectStructureImportSourceKind SourceKind,
    string Title,
    string? SourceText = null,
    ProjectObjectMediaPayload? SourceAsset = null,
    string ContainerBlockSubtype = "delivery",
    string LeafWorkItemSubtype = "task",
    string? LeaseToken = null);

/// <summary>Result of a committed outline import.</summary>
/// <param name="ProjectId">Identifier of the project the outline was imported into.</param>
/// <param name="ContainerNodeId">Identifier of the container block that holds the imported nodes.</param>
/// <param name="SourceNodeId">Identifier of the File node that stores the source file; null when none was sent.</param>
/// <param name="CreatedNodeIds">
/// Identifiers of every created node: the container, the source file node when present, then the imported items.
/// </param>
/// <param name="Warnings">
/// Human-readable notes about how the source was interpreted: how a Mermaid source was read, skipped lines and links,
/// or an XMind layout that could not be read (then nothing was imported besides the container and source file).
/// </param>
public sealed record ProjectStructureImportResult(
    Guid ProjectId,
    string ContainerNodeId,
    string? SourceNodeId,
    IReadOnlyList<string> CreatedNodeIds,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Filters for the Project Structure operation log. Every member is optional; the filters combine, and <c>{}</c>
/// returns the 50 most recent entries of all projects.
/// </summary>
/// <param name="ProjectId">Keep only entries of this project; null keeps every project.</param>
/// <param name="OperationName">
/// Keep only entries of this operation, compared exactly, for example <c>structure.read</c>, <c>tasks.create</c> or
/// <c>leases.acquire</c>; null or blank keeps every operation.
/// </param>
/// <param name="AgentId">
/// Keep only calls by this caller identity, compared exactly (see <c>agentId</c> of the entries); null or blank keeps
/// every caller.
/// </param>
/// <param name="Succeeded">True for successful calls only, false for failed calls only, null for both.</param>
/// <param name="Take">Maximum number of entries, newest first; clamped to 1 through 200. Defaults to 50.</param>
public sealed record ProjectStructureAnalyticsQueryRequest(
    Guid? ProjectId = null,
    string? OperationName = null,
    string? AgentId = null,
    bool? Succeeded = null,
    int Take = 50);

/// <summary>
/// One recorded call of a Project Structure operation, successful or failed, made over this HTTP API or by an
/// in-process agent tool. Entries contain the complete request and response as JSON text and can therefore hold
/// anything those contained, such as notes, metadata, inline file content and lease tokens.
/// </summary>
/// <param name="Id">Identifier of the log entry.</param>
/// <param name="OperationName">
/// Name of the operation, for example <c>structure.read</c>, <c>tasks.create</c> or <c>leases.acquire</c>. Agent tools
/// use the same names for the same operations.
/// </param>
/// <param name="ProjectId">
/// Project named by the call's route or body; null for calls without one, including lease calls (see
/// <c>scopeKey</c>).
/// </param>
/// <param name="NodeKey">Node identifier named by the call's route; null when it had none.</param>
/// <param name="ScopeKind">
/// Lease scope the call is attributed to, as a JSON integer: 0 Project, 1 ProjectNode, 2 RepoBranch. Lease calls
/// record the requested scope and project mutations record their project; null for reads.
/// </param>
/// <param name="ScopeKey">Lease scope key the call is attributed to, as sent; null for reads.</param>
/// <param name="AgentId">
/// Caller identity: for HTTP calls, the bearer token subject or <c>local-api-operator</c> when API authorization is
/// disabled; for agent tool calls, the agent's identifier.
/// </param>
/// <param name="AgentName">Display name of the caller, for example <c>Local API operator</c>.</param>
/// <param name="MachineName">Machine name recorded for the caller; for HTTP calls, the name of the server
/// machine.</param>
/// <param name="RepositoryRoot">
/// Repository root recorded for the caller: empty for HTTP calls; for agent tool calls, a directory path on the server.
/// </param>
/// <param name="BranchName">Branch name recorded for the caller; empty for HTTP calls.</param>
/// <param name="Succeeded">True when the call succeeded.</param>
/// <param name="DurationMs">Duration of the call's work in milliseconds.</param>
/// <param name="WarningCount">Number of warnings the call returned; 0 for a failed call.</param>
/// <param name="ErrorCode">Error code of a failed call, as returned in <c>error.errorCode</c>; null for a
/// success.</param>
/// <param name="ErrorMessage">
/// Error message of a failed call; null for a success. For <c>UnhandledError</c> it records the internal failure
/// message rather than the generic message returned to the caller.
/// </param>
/// <param name="RequestSummaryJson">
/// The call's request body as a JSON string that contains JSON; <c>{}</c> for calls without a body or whose body could
/// not be read. The current-lease lookup records its query values instead.
/// </param>
/// <param name="ResponseSummaryJson">
/// The call's response as a JSON string that contains JSON; a plan summary is recorded in shortened form. For a
/// failed call, the error details, or <c>{}</c> when there were none.
/// </param>
/// <param name="WarningsJson">The call's warnings as a JSON string that contains an array of strings.</param>
/// <param name="OccurredAtUtc">Time the call finished and was recorded, in UTC.</param>
public sealed record ProjectStructureAnalyticsEntry(
    Guid Id,
    string OperationName,
    Guid? ProjectId,
    string? NodeKey,
    ProjectStructureLeaseScopeKind? ScopeKind,
    string? ScopeKey,
    string AgentId,
    string AgentName,
    string MachineName,
    string RepositoryRoot,
    string BranchName,
    bool Succeeded,
    long DurationMs,
    int WarningCount,
    string? ErrorCode,
    string? ErrorMessage,
    string RequestSummaryJson,
    string ResponseSummaryJson,
    string WarningsJson,
    DateTimeOffset OccurredAtUtc);

/// <summary>Entries of the Project Structure operation log, newest first.</summary>
/// <param name="Entries">Matching entries; empty when none matched.</param>
public sealed record ProjectStructureAnalyticsResponse(
    IReadOnlyList<ProjectStructureAnalyticsEntry> Entries);

/// <summary>
/// Category of project-management guidance, as a JSON integer: 0 Mission, 1 Planning, 2 Estimation, 3 Approval,
/// 4 Reporting, 5 Risk, 6 Collaboration.
/// </summary>
public enum ProjectManagementGuidanceCategory
{
    Mission,
    Planning,
    Estimation,
    Approval,
    Reporting,
    Risk,
    Collaboration
}

/// <summary>Filters for the project-management guidance query. Every member is optional.</summary>
/// <param name="Categories">
/// Categories to keep, as JSON integers (0 Mission, 1 Planning, 2 Estimation, 3 Approval, 4 Reporting, 5 Risk,
/// 6 Collaboration); an entry matches when it has any of them. Null or empty keeps every category.
/// </param>
/// <param name="Query">
/// Text to search for in titles, summaries, guidance and tags, ignoring case (substring match); null or blank matches
/// everything.
/// </param>
/// <param name="Take">Maximum number of entries; clamped to 1 through 50. Defaults to 10.</param>
public sealed record ProjectManagementGuidanceQueryRequest(
    IReadOnlyList<ProjectManagementGuidanceCategory>? Categories = null,
    string? Query = null,
    int Take = 10);

/// <summary>One piece of project-management guidance.</summary>
/// <param name="Id">Stable identifier of the entry, for example <c>planning-split-work</c>.</param>
/// <param name="Category">
/// Category, as a JSON integer: 0 Mission, 1 Planning, 2 Estimation, 3 Approval, 4 Reporting, 5 Risk, 6 Collaboration.
/// </param>
/// <param name="Title">Short title of the guidance.</param>
/// <param name="Summary">One-sentence summary.</param>
/// <param name="Guidance">The full guidance text.</param>
/// <param name="Tags">Keywords of the entry.</param>
/// <param name="IsMissionAnchor">True for the entry that states the mission behind all project work; listed
/// first.</param>
public sealed record ProjectManagementGuidanceEntry(
    string Id,
    ProjectManagementGuidanceCategory Category,
    string Title,
    string Summary,
    string Guidance,
    IReadOnlyList<string> Tags,
    bool IsMissionAnchor);

/// <summary>Result of a project-management guidance query.</summary>
/// <param name="Entries">
/// Matching entries: mission anchors first, then by category and title; empty when nothing matched.
/// </param>
public sealed record ProjectManagementGuidanceResponse(
    IReadOnlyList<ProjectManagementGuidanceEntry> Entries);

public class ProjectStructureAgentException : Exception, IAgentToolFailureEffectEvidence
{
    private readonly bool isSafeToExpose;
    private readonly bool canRetryWithCorrectedInput;
    private readonly AgentToolEffectState effectState;

    public ProjectStructureAgentException(int statusCode, string errorCode, string message, object? details = null)
        : this(
            statusCode,
            errorCode,
            message,
            details,
            isSafeToExpose: false,
            canRetryWithCorrectedInput: false,
            effectState: AgentToolEffectState.Unknown)
    {
    }

    protected ProjectStructureAgentException(
        int statusCode,
        string errorCode,
        string message,
        object? details,
        bool isSafeToExpose,
        bool canRetryWithCorrectedInput,
        AgentToolEffectState effectState = AgentToolEffectState.Unknown,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        Details = details;
        this.isSafeToExpose = isSafeToExpose;
        this.canRetryWithCorrectedInput = canRetryWithCorrectedInput;
        this.effectState = effectState;
    }

    public static ProjectStructureAgentException CreateAgentVisible(
        int statusCode,
        string errorCode,
        string safeMessage,
        bool canRetryWithCorrectedInput,
        object? diagnosticDetails = null,
        AgentToolEffectState effectState = AgentToolEffectState.Unknown)
    {
        return new ProjectStructureAgentException(
            statusCode,
            errorCode,
            safeMessage,
            diagnosticDetails,
            isSafeToExpose: true,
            canRetryWithCorrectedInput,
            effectState);
    }

    internal static ProjectStructureAgentException CreateMapped(
        int statusCode,
        string errorCode,
        string message,
        object? details,
        bool isSafeToExpose,
        bool canRetryWithCorrectedInput,
        Exception innerException,
        AgentToolEffectState effectState = AgentToolEffectState.Unknown)
    {
        return new ProjectStructureAgentException(
            statusCode,
            errorCode,
            message,
            details,
            isSafeToExpose,
            canRetryWithCorrectedInput,
            effectState,
            innerException);
    }

    public int StatusCode { get; }

    public string ErrorCode { get; }

    public object? Details { get; }

    public string SafeMessage => Message;

    public bool IsSafeToExpose => isSafeToExpose;

    public bool CanRetryWithCorrectedInput => canRetryWithCorrectedInput;

    public AgentToolEffectState EffectState => effectState;
}

public sealed class ProjectStructureLeaseConflictException : ProjectStructureAgentException
{
    public ProjectStructureLeaseConflictException(ProjectStructureLeaseConflict conflict)
        : this(conflict, AgentToolEffectState.Unknown)
    {
    }

    public ProjectStructureLeaseConflictException(ProjectStructureLeaseConflict conflict, AgentToolEffectState effectState)
        : base(
            409,
            "LeaseConflict",
            "Another active operation currently holds the project-structure mutation lease. Wait for that operation to finish or for its lease to expire, then retry.",
            details: null,
            isSafeToExpose: true,
            canRetryWithCorrectedInput: false,
            effectState)
    {
        Conflict = conflict;
    }

    public ProjectStructureLeaseConflict Conflict { get; }
}
