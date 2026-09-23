using System.Text.Json.Serialization;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Web.Api;

/// <summary>
/// How a memory provider query runs, as a PascalCase string token: <c>Synchronous</c> (the provider answers in the
/// same request; requires the <c>context.query.sync</c> capability) or <c>Asynchronous</c> (the provider accepts an
/// operation to poll; requires <c>context.query.async</c>).
/// </summary>
internal enum MemoryProviderQueryMode
{
    Synchronous = 0,
    Asynchronous = 1
}

/// <summary>
/// Driver that connects the host to a memory provider, as a PascalCase string token: <c>Http</c> (a provider
/// speaking the host's HTTP memory protocol), <c>Mcp</c> (a remote Model Context Protocol server), <c>NativeRemote</c>
/// (a remote Cognitive Memory service, configured with the HTTP transport) or <c>Mock</c> (a deterministic built-in
/// provider for tests and demonstrations).
/// </summary>
internal enum MemoryProviderDriverKindApiRequest
{
    Http = 0,
    Mcp = 1,
    NativeRemote = 2,
    Mock = 3
}

/// <summary>
/// Whether a memory provider may be used as the implicit default, as a PascalCase string token:
/// <c>DenyImplicitFallback</c> (only when it is chosen explicitly or by an assignment) or
/// <c>AllowDefaultProviderWhenNoAssignment</c> (also as the default provider when no assignment applies). Queries
/// through <c>POST /api/memory-providers/{providerId}/queries</c> always name the provider, so this setting does not
/// affect them.
/// </summary>
internal enum MemoryProviderFallbackBehaviorApiRequest
{
    DenyImplicitFallback = 0,
    AllowDefaultProviderWhenNoAssignment = 1
}

/// <summary>
/// Complete configuration of a memory provider profile, sent to <c>PUT /api/memory-providers/{providerId}</c>. The
/// request replaces the configuration it covers; send every member, using null for <c>selectionTags</c>, <c>http</c>
/// or <c>mcp</c> when they do not apply. Unknown members are rejected. The driver kind decides which transport is
/// required: <c>http</c> for <c>Http</c> and <c>NativeRemote</c>, <c>mcp</c> for <c>Mcp</c>, neither for
/// <c>Mock</c>.
/// </summary>
/// <param name="DisplayName">
/// Human-readable name of the provider. Surrounding whitespace is removed; must not be blank.
/// </param>
/// <param name="DriverKind">
/// Driver of the provider, as a PascalCase string token: <c>Http</c>, <c>Mcp</c>, <c>NativeRemote</c> or <c>Mock</c>.
/// It decides the required transport and which capabilities can be enabled.
/// </param>
/// <param name="IsEnabled">
/// True to make the provider selectable. A disabled provider keeps its configuration but queries to it are refused.
/// </param>
/// <param name="FallbackBehavior">
/// Whether the provider may serve as the implicit default provider, as a PascalCase string token:
/// <c>DenyImplicitFallback</c> or <c>AllowDefaultProviderWhenNoAssignment</c>. It has no effect on queries through this
/// API, which always name the provider.
/// </param>
/// <param name="ProviderKind">
/// Kind of memory service behind the provider, as a lowercase dotted token such as <c>memory.mock</c>: a lowercase
/// letter followed by lowercase letters and digits, in segments separated by <c>.</c> or <c>-</c>. Surrounding
/// whitespace is removed.
/// </param>
/// <param name="SelectionTags">
/// Free-form labels stored with the profile and shown in provider management; provider selection does not use them.
/// Blank entries are dropped and the others trimmed. Null means no tags.
/// </param>
/// <param name="Capabilities">
/// The query and status capabilities to advertise. Required; they must be supported by the driver kind.
/// </param>
/// <param name="Http">
/// HTTP transport settings. Required for the <c>Http</c> and <c>NativeRemote</c> drivers and rejected for the others;
/// send null when not required.
/// </param>
/// <param name="Mcp">
/// Model Context Protocol transport settings. Required for the <c>Mcp</c> driver and rejected for the others; send null
/// when not required.
/// </param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record MemoryProviderProfileApiRequest(
    string DisplayName,
    [property: JsonConverter(typeof(JsonStringEnumConverter<MemoryProviderDriverKindApiRequest>))]
    MemoryProviderDriverKindApiRequest DriverKind,
    bool IsEnabled,
    [property: JsonConverter(typeof(JsonStringEnumConverter<MemoryProviderFallbackBehaviorApiRequest>))]
    MemoryProviderFallbackBehaviorApiRequest FallbackBehavior,
    string ProviderKind,
    IReadOnlyList<string>? SelectionTags,
    MemoryProviderCapabilitiesApiRequest Capabilities,
    MemoryProviderHttpTransportApiModel? Http,
    MemoryProviderMcpTransportApiModel? Mcp);

/// <summary>
/// A memory provider profile as stored by the host, returned by the memory-provider operations. It carries the
/// configuration, the advertised capabilities and limits, and the last recorded health. Credentials appear only as
/// environment-variable names, never as values.
/// </summary>
/// <param name="ProviderId">
/// Identifier of the profile, chosen when it was saved with <c>PUT /api/memory-providers/{providerId}</c>. Use it in
/// the other memory-provider routes.
/// </param>
/// <param name="DisplayName">Human-readable name of the provider.</param>
/// <param name="DriverKind">
/// Driver of the provider, as a PascalCase string token: <c>Http</c>, <c>Mcp</c>, <c>NativeRemote</c>, <c>Mock</c> or
/// <c>InProcessMigration</c> (an internal migration driver that this API cannot configure).
/// </param>
/// <param name="IsEnabled">True when the provider can be selected; false when queries to it are refused.</param>
/// <param name="HealthState">
/// Last recorded health of the provider, as a PascalCase string token: <c>Unknown</c> (not checked yet),
/// <c>Healthy</c>, <c>Degraded</c> or <c>Unreachable</c>. Reading the profile does not check the provider.
/// </param>
/// <param name="WorkspaceScope">
/// Workspaces the provider serves, as a PascalCase string token: <c>AllWorkspaces</c> or <c>SingleWorkspace</c>.
/// Profiles saved through this API always use <c>AllWorkspaces</c>; the host refuses to select a
/// <c>SingleWorkspace</c> provider because it cannot verify the workspace.
/// </param>
/// <param name="FallbackBehavior">
/// Whether the provider may serve as the implicit default provider, as a PascalCase string token:
/// <c>DenyImplicitFallback</c> or <c>AllowDefaultProviderWhenNoAssignment</c>.
/// </param>
/// <param name="ProviderKind">Kind of memory service behind the provider, as a lowercase dotted token.</param>
/// <param name="ProtocolVersion">Memory protocol version the profile uses, currently <c>memory-protocol.v1</c>.</param>
/// <param name="SelectionTags">Free-form labels of the profile; provider selection does not use them.</param>
/// <param name="Capabilities">Capabilities the profile advertises.</param>
/// <param name="InteractionSupport">Interaction styles derived from the advertised capabilities.</param>
/// <param name="Limits">Limits the host applies to results and operations of this provider.</param>
/// <param name="Http">
/// HTTP transport settings of an <c>Http</c> or <c>NativeRemote</c> provider; null for other drivers.
/// </param>
/// <param name="Mcp">
/// Model Context Protocol transport settings of an <c>Mcp</c> provider; null for other drivers.
/// </param>
internal sealed record MemoryProviderProfileApiResponse(
    string ProviderId,
    string DisplayName,
    [property: JsonConverter(typeof(JsonStringEnumConverter<MemoryProviderDriverKind>))]
    MemoryProviderDriverKind DriverKind,
    bool IsEnabled,
    [property: JsonConverter(typeof(JsonStringEnumConverter<MemoryProviderHealthState>))]
    MemoryProviderHealthState HealthState,
    [property: JsonConverter(typeof(JsonStringEnumConverter<MemoryProviderWorkspaceScope>))]
    MemoryProviderWorkspaceScope WorkspaceScope,
    [property: JsonConverter(typeof(JsonStringEnumConverter<MemoryProviderFallbackBehavior>))]
    MemoryProviderFallbackBehavior FallbackBehavior,
    string ProviderKind,
    string ProtocolVersion,
    IReadOnlyList<string> SelectionTags,
    MemoryProviderCapabilitiesApiResponse Capabilities,
    MemoryProviderInteractionSupportApiResponse InteractionSupport,
    MemoryProviderLimitsApiResponse Limits,
    MemoryProviderHttpTransportApiModel? Http,
    MemoryProviderMcpTransportApiModel? Mcp);

/// <summary>
/// Query and status capabilities to advertise for a memory provider profile. Each driver supports only some of them:
/// <c>Http</c>, <c>NativeRemote</c> and <c>Mock</c> only synchronous queries; <c>Mcp</c> all three. Asynchronous
/// queries require operation status. Send all three members.
/// </summary>
/// <param name="SupportsSynchronousQueries">
/// True to advertise synchronous context queries (<c>context.query.sync</c>).
/// </param>
/// <param name="SupportsAsynchronousQueries">
/// True to advertise asynchronous context queries (<c>context.query.async</c>). Requires
/// <c>supportsOperationStatus</c> and the <c>Mcp</c> driver.
/// </param>
/// <param name="SupportsOperationStatus">
/// True to advertise operation status (<c>operations.status</c>), which the host needs to poll asynchronous
/// operations. Only the <c>Mcp</c> driver supports it.
/// </param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record MemoryProviderCapabilitiesApiRequest(
    bool SupportsSynchronousQueries,
    bool SupportsAsynchronousQueries,
    bool SupportsOperationStatus);

/// <summary>
/// Capabilities a memory provider profile advertises, as stored by the host.
/// </summary>
/// <param name="SupportsSynchronousQueries">True when the profile advertises <c>context.query.sync</c>.</param>
/// <param name="SupportsAsynchronousQueries">True when the profile advertises <c>context.query.async</c>.</param>
/// <param name="SupportsOperationStatus">True when the profile advertises <c>operations.status</c>.</param>
/// <param name="SupportsRclUi">
/// True when the profile advertises an embedded user-interface panel (<c>ui.rcl</c>). This API does not change it.
/// </param>
/// <param name="SupportsIframeUi">
/// True when the profile advertises an embedded provider console page (<c>ui.iframe</c>). This API does not change it.
/// </param>
internal sealed record MemoryProviderCapabilitiesApiResponse(
    bool SupportsSynchronousQueries,
    bool SupportsAsynchronousQueries,
    bool SupportsOperationStatus,
    bool SupportsRclUi,
    bool SupportsIframeUi);

/// <summary>
/// Interaction styles of a memory provider, derived from its advertised capabilities.
/// </summary>
/// <param name="SupportsSynchronousQueries">True when the provider answers queries within the request.</param>
/// <param name="SupportsAsynchronousOperations">
/// True when the provider accepts operations that are completed later.
/// </param>
internal sealed record MemoryProviderInteractionSupportApiResponse(
    bool SupportsSynchronousQueries,
    bool SupportsAsynchronousOperations);

/// <summary>
/// Limits the host applies to a memory provider's results and operations. This API does not change them; a new
/// profile gets the host defaults (12 sections, 100 source items, 4 operations in flight, 120 seconds).
/// </summary>
/// <param name="MaxContextSections">
/// Largest number of sections the host accepts in one context pack from this provider; a larger pack fails the query.
/// </param>
/// <param name="MaxSourceItems">
/// Largest total number of citations the host accepts in one context pack from this provider.
/// </param>
/// <param name="MaxInFlightOperations">
/// Declared largest number of concurrent operations of the provider. Informational: the host does not enforce it.
/// </param>
/// <param name="OperationTimeoutSeconds">
/// Seconds after creation at which the host's memory background workers record an unfinished operation of the
/// provider as timed out; may have a fractional part.
/// </param>
internal sealed record MemoryProviderLimitsApiResponse(
    int MaxContextSections,
    int MaxSourceItems,
    int MaxInFlightOperations,
    double OperationTimeoutSeconds);

/// <summary>
/// HTTP transport of a memory provider that uses the <c>Http</c> or <c>NativeRemote</c> driver. In a request, send
/// every member. The credential is referenced by the name of an environment variable of the host; the secret value
/// itself is never sent or returned.
/// </summary>
/// <param name="BaseUrl">
/// Base address of the provider, for example <c>https://memory.example.com/api</c>: an absolute HTTPS address, or HTTP
/// only to a loopback host, without user information, query or fragment.
/// </param>
/// <param name="QueryPath">
/// Path of the query endpoint relative to <c>baseUrl</c>, for example <c>/memory/query</c>: it starts with
/// <c>/</c> and has no <c>//</c>, backslash, query, fragment or control character.
/// </param>
/// <param name="HealthPath">
/// Path of the health endpoint relative to <c>baseUrl</c>, for example <c>/memory/health</c>, with the same rules
/// as <c>queryPath</c>.
/// </param>
/// <param name="ApiKeyEnvironmentVariable">
/// Name of the host environment variable that holds the API key, for example <c>MEMORY_PROVIDER_API_KEY</c>: ASCII
/// letters, digits and underscores, not starting with a digit. Empty means the provider is called without a
/// credential. A value that is not a variable name, such as a key itself, is rejected.
/// </param>
/// <param name="AuthHeaderName">
/// Name of the request header that carries the credential, for example <c>Authorization</c>; must be a valid HTTP
/// header name.
/// </param>
/// <param name="AuthScheme">
/// Authorization scheme placed before the credential when <c>authHeaderName</c> is <c>Authorization</c>, for example
/// <c>Bearer</c>; must then be a valid HTTP token.
/// </param>
/// <param name="TimeoutMilliseconds">Timeout of one provider request in milliseconds; at least 1.</param>
/// <param name="MaxRetryAttempts">Number of retries after a failed provider request; 0 or more.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record MemoryProviderHttpTransportApiModel(
    string BaseUrl,
    string QueryPath,
    string HealthPath,
    string ApiKeyEnvironmentVariable,
    string AuthHeaderName,
    string AuthScheme,
    int TimeoutMilliseconds,
    int MaxRetryAttempts);

/// <summary>
/// Model Context Protocol transport of a memory provider that uses the <c>Mcp</c> driver: the remote MCP server and
/// the tools the host calls. In a request, send every member, using an empty string for values that do not apply. The
/// credential is referenced by the name of an environment variable of the host; the secret value itself is never
/// sent or returned.
/// </summary>
/// <param name="DescriptorKind">Kind of MCP server; only <c>remote-http</c> is accepted.</param>
/// <param name="ServerKey">Key that identifies the MCP server within the host. Required.</param>
/// <param name="DisplayName">Display name of the MCP server. Empty means the profile's display name.</param>
/// <param name="Description">Description of the MCP server. Empty means a generic description.</param>
/// <param name="RemoteEndpoint">
/// Address of the MCP server, for example <c>https://mcp.example.com/memory</c>: an absolute HTTPS address, or HTTP
/// only to a loopback host, without user information, query or fragment.
/// </param>
/// <param name="AuthHeaderName">
/// Name of the request header that carries the credential, for example <c>Authorization</c>; must be a valid HTTP
/// header name. It is stored only when <c>authHeaderEnvironmentVariable</c> is set.
/// </param>
/// <param name="AuthHeaderEnvironmentVariable">
/// Name of the host environment variable whose value is sent in <c>authHeaderName</c>: ASCII letters, digits and
/// underscores, not starting with a digit. Empty means no credential header. A value that is not a variable name is
/// rejected.
/// </param>
/// <param name="ContextQueryTool">
/// Name of the MCP tool that answers context queries. Required when a query capability is enabled.
/// </param>
/// <param name="OperationStatusTool">
/// Name of the MCP tool that reports operation status. Required when asynchronous queries or operation status are
/// enabled.
/// </param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record MemoryProviderMcpTransportApiModel(
    string DescriptorKind,
    string ServerKey,
    string DisplayName,
    string Description,
    string RemoteEndpoint,
    string AuthHeaderName,
    string AuthHeaderEnvironmentVariable,
    string ContextQueryTool,
    string OperationStatusTool);

/// <summary>
/// Request of <c>POST /api/memory-providers/{providerId}/queries</c>: the text to look up in the provider's memory and
/// how to run the query. Send both members; unknown members are rejected.
/// </summary>
/// <param name="Query">
/// Text to look up. Surrounding whitespace is removed; the rest must be 1 to 32,768 characters (UTF-16 code units).
/// </param>
/// <param name="Mode">
/// How to run the query, as a PascalCase string token: <c>Synchronous</c> (answer within the request) or
/// <c>Asynchronous</c> (start an operation to poll).
/// </param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record MemoryProviderQueryApiRequest(
    string Query,
    [property: JsonConverter(typeof(JsonStringEnumConverter<MemoryProviderQueryMode>))]
    MemoryProviderQueryMode Mode);

/// <summary>
/// Result of <c>POST /api/memory-providers/{providerId}/queries</c>. The HTTP status follows <c>status</c>; which of
/// <c>operation</c>, <c>contextPack</c> and <c>acceptedOperation</c> are present depends on how far the query got.
/// </summary>
/// <param name="Status">
/// Outcome, as a PascalCase string token; see the <c>MemoryOperationHandlerStatus</c> schema for every value.
/// <c>Completed</c> (HTTP 200) and <c>Accepted</c> (HTTP 202) are the successes. Selection outcomes such as
/// <c>ProviderNotFound</c> (404), <c>ProviderDenied</c> (403) or <c>ProviderDisabled</c> and
/// <c>CapabilityUnavailable</c> (409) mean nothing was dispatched; <c>DriverUnavailable</c>, <c>DriverFailed</c> and
/// <c>Failed</c> (502) and <c>TimedOut</c> (504) mean dispatch failed.
/// </param>
/// <param name="Diagnostic">
/// Human-readable explanation of the outcome. Its wording can change and must not be parsed.
/// </param>
/// <param name="Selection">How the host selected, or refused, the provider.</param>
/// <param name="Operation">
/// The operation recorded in the host's ledger for this query; null when the query was refused before an operation
/// was recorded (selection failures).
/// </param>
/// <param name="ContextPack">The retrieved context when <c>status</c> is <c>Completed</c>; otherwise null.</param>
/// <param name="AcceptedOperation">
/// How to poll the operation when <c>status</c> is <c>Accepted</c>; otherwise null.
/// </param>
/// <param name="FeedbackHandle">
/// The same value as <c>contextPack.feedbackHandle</c>; null when no context was delivered or the provider does not
/// accept feedback.
/// </param>
/// <param name="DriverDispatchAttempted">
/// True when the host called the provider. False means the query was refused before dispatch and the provider was not
/// contacted.
/// </param>
internal sealed record MemoryProviderQueryApiResponse(
    [property: JsonConverter(typeof(JsonStringEnumConverter<MemoryOperationHandlerStatus>))]
    MemoryOperationHandlerStatus Status,
    string Diagnostic,
    MemoryProviderSelectionApiResponse Selection,
    MemoryProviderOperationApiResponse? Operation,
    MemoryContextPackApiResponse? ContextPack,
    MemoryAcceptedOperationApiResponse? AcceptedOperation,
    string? FeedbackHandle,
    bool DriverDispatchAttempted);

/// <summary>
/// Result of <c>GET /api/memory-providers/operations/{operationId}</c>: whether the caller may read the operation
/// and, if so, its ledger record. The HTTP status follows <c>status</c>.
/// </summary>
/// <param name="Status">
/// Outcome of the read, as a PascalCase string token; see the <c>MemoryOperationHandlerStatus</c> schema for every
/// value. <c>Completed</c> (HTTP 200) means the record was read, whatever state the operation itself is in; read
/// <c>operation.status</c> for that. <c>AccessDenied</c> or <c>ProviderDenied</c> (403), <c>NotFound</c> or
/// <c>ProviderNotFound</c> (404) and outcomes such as <c>ProviderDisabled</c> or <c>CapabilityUnavailable</c> (409)
/// mean it could not be read.
/// </param>
/// <param name="Diagnostic">
/// Human-readable explanation of the outcome. Its wording can change and must not be parsed.
/// </param>
/// <param name="Selection">How the host checked the provider that ran the operation.</param>
/// <param name="Operation">
/// The operation's ledger record when <c>status</c> is <c>Completed</c>; otherwise null.
/// </param>
internal sealed record MemoryProviderOperationStatusApiResponse(
    [property: JsonConverter(typeof(JsonStringEnumConverter<MemoryOperationHandlerStatus>))]
    MemoryOperationHandlerStatus Status,
    string Diagnostic,
    MemoryProviderSelectionApiResponse Selection,
    MemoryProviderOperationApiResponse? Operation);

/// <summary>
/// How the host selected, or refused, the memory provider for an operation. <c>dispatchAllowed</c> is true only when
/// <c>status</c> is <c>Selected</c>.
/// </summary>
/// <param name="Status">
/// Selection result, as a PascalCase string token: <c>Selected</c>, <c>NoProviderConfigured</c>,
/// <c>NoEnabledProvider</c>, <c>ProviderNotFound</c>, <c>ProviderDisabled</c>, <c>CapabilityUnavailable</c> (the
/// provider does not advertise the required capability), <c>CapabilityDenied</c>, <c>ProviderDenied</c> (for example a
/// single-workspace provider, or an operation owned by another caller), <c>ProviderSelectionRequired</c> or
/// <c>ProviderConfigurationFailed</c> (the provider configuration could not be loaded).
/// </param>
/// <param name="Reason">
/// Why this provider was considered, as a PascalCase string token: <c>None</c>, <c>ExplicitProvider</c> (named by the
/// request; the usual value for this API), <c>AssignmentOverride</c> or <c>DefaultProvider</c>.
/// </param>
/// <param name="RequiredCapability">
/// Capability the operation needed, for example <c>context.query.sync</c>, <c>context.query.async</c> or
/// <c>operations.status</c>.
/// </param>
/// <param name="DispatchAllowed">True when the host may call the selected provider.</param>
/// <param name="Diagnostic">
/// Human-readable explanation of the selection. Its wording can change and must not be parsed.
/// </param>
/// <param name="SelectedProviderId">Identifier of the selected provider profile; null when none was selected.</param>
/// <param name="CandidateProviderIds">
/// Identifiers of the provider profiles the selection considered; can be empty.
/// </param>
internal sealed record MemoryProviderSelectionApiResponse(
    [property: JsonConverter(typeof(JsonStringEnumConverter<MemoryProviderSelectionStatus>))]
    MemoryProviderSelectionStatus Status,
    [property: JsonConverter(typeof(JsonStringEnumConverter<MemoryProviderSelectionReason>))]
    MemoryProviderSelectionReason Reason,
    string RequiredCapability,
    bool DispatchAllowed,
    string Diagnostic,
    string? SelectedProviderId,
    IReadOnlyList<string> CandidateProviderIds);

/// <summary>
/// A memory operation as recorded in the host's operation ledger. The record reflects what the host knows; it is
/// updated by the host, not read live from the provider.
/// </summary>
/// <param name="OperationId">
/// Identifier (GUID) of the operation; use it with <c>GET /api/memory-providers/operations/{operationId}</c>.
/// </param>
/// <param name="ProviderId">Identifier of the provider profile that runs the operation.</param>
/// <param name="RequestedCapability">Capability the operation used, for example <c>context.query.async</c>.</param>
/// <param name="OperationKind">
/// Kind of operation, as a PascalCase string token: <c>ContextQuery</c> for the queries of this API; the other values
/// (<c>Ingestion</c>, <c>Feedback</c>, <c>SourceRequest</c>, <c>EventAcknowledge</c>, <c>OperationStatus</c>,
/// <c>CapabilityExchange</c>, <c>Health</c>) come from other memory features.
/// </param>
/// <param name="Status">
/// Ledger state, as a PascalCase string token: <c>Pending</c>, <c>Accepted</c> and <c>Running</c> are in progress;
/// <c>Completed</c>, <c>Failed</c>, <c>TimedOut</c>, <c>Cancelled</c>, <c>Expired</c> and <c>Forgotten</c> are
/// final.
/// </param>
/// <param name="RetryCount">Number of times the host retried the operation.</param>
/// <param name="TransitionCount">Number of state changes recorded for the operation.</param>
/// <param name="CreatedAtUtc">Instant (UTC, with offset) at which the operation was recorded.</param>
/// <param name="UpdatedAtUtc">Instant (UTC, with offset) of the last change to the record.</param>
/// <param name="CompletedAtUtc">
/// Instant (UTC, with offset) at which the operation reached a final state; null before.
/// </param>
/// <param name="StatusReason">
/// Human-readable reason for the current state. Its wording can change and must not be parsed.
/// </param>
internal sealed record MemoryProviderOperationApiResponse(
    Guid OperationId,
    string ProviderId,
    string RequestedCapability,
    [property: JsonConverter(typeof(JsonStringEnumConverter<MemoryOperationKind>))]
    MemoryOperationKind OperationKind,
    [property: JsonConverter(typeof(JsonStringEnumConverter<MemoryLedgerStatus>))]
    MemoryLedgerStatus Status,
    int RetryCount,
    int TransitionCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string StatusReason);

/// <summary>
/// Context retrieved from a memory provider by a completed query: a summary and sections with citations, as
/// validated by the host against the provider's limits.
/// </summary>
/// <param name="ContextPackId">Identifier (GUID) of this context pack.</param>
/// <param name="Summary">Summary of the retrieved context; never blank.</param>
/// <param name="Sections">Sections of retrieved context; at most <c>maxContextSections</c> of the provider.</param>
/// <param name="Warnings">
/// Warnings about the result, for example partial or policy-limited context; can be empty.
/// </param>
/// <param name="ProviderConfidence">The provider's confidence in the whole pack, from 0 through 1.</param>
/// <param name="FeedbackHandle">
/// Host-issued opaque handle for later feedback about this context. Null when the provider does not accept feedback,
/// which is always the case for a profile saved through <c>PUT /api/memory-providers/{providerId}</c>.
/// </param>
internal sealed record MemoryContextPackApiResponse(
    Guid ContextPackId,
    string Summary,
    IReadOnlyList<MemoryContextSectionApiResponse> Sections,
    IReadOnlyList<MemoryWarningApiResponse> Warnings,
    decimal ProviderConfidence,
    string? FeedbackHandle);

/// <summary>
/// One section of retrieved memory context with its sources.
/// </summary>
/// <param name="Title">Title of the section; never blank.</param>
/// <param name="Text">Text of the section; never blank.</param>
/// <param name="Citations">Sources that support the section; can be empty.</param>
/// <param name="Confidence">The provider's confidence in this section, from 0 through 1.</param>
internal sealed record MemoryContextSectionApiResponse(
    string Title,
    string Text,
    IReadOnlyList<MemoryCitationApiResponse> Citations,
    decimal Confidence);

/// <summary>
/// A source cited by a section of memory context.
/// </summary>
/// <param name="SourceRef">Provider-defined reference to the source; never blank. Treat it as opaque.</param>
/// <param name="Label">Human-readable label of the source; can be empty.</param>
internal sealed record MemoryCitationApiResponse(
    string SourceRef,
    string Label);

/// <summary>
/// A warning attached to a memory context pack.
/// </summary>
/// <param name="Kind">
/// Kind of warning, as a PascalCase string token: <c>PolicyLimited</c> (policy limited the context),
/// <c>ProviderPartial</c> (the provider returned partial context), <c>CapabilityUnavailable</c> or
/// <c>SourceUnavailable</c>.
/// </param>
/// <param name="Message">Human-readable warning; never blank. Its wording can change and must not be parsed.</param>
internal sealed record MemoryWarningApiResponse(
    [property: JsonConverter(typeof(JsonStringEnumConverter<MemoryWarningKind>))]
    MemoryWarningKind Kind,
    string Message);

/// <summary>
/// How to follow an asynchronous memory operation that a provider accepted.
/// </summary>
/// <param name="OperationId">
/// Identifier (GUID) of the accepted operation; read it with <c>GET /api/memory-providers/operations/{operationId}</c>.
/// </param>
/// <param name="StatusPath">
/// The operation identifier in text form (the same value as <c>operationId</c>), not an address. Build the polling
/// address from <c>operationId</c>.
/// </param>
/// <param name="ExpiresAtUtc">
/// Instant (UTC, with offset) at which the provider's acceptance expires; after it the host's memory background
/// workers record an unfinished operation as timed out.
/// </param>
/// <param name="PollAfterSeconds">
/// Minimum number of seconds to wait between status reads, as requested by the provider; greater than 0 and may have a
/// fractional part.
/// </param>
/// <param name="CallbackAvailable">Always false: this API delivers no callbacks; poll instead.</param>
internal sealed record MemoryAcceptedOperationApiResponse(
    Guid OperationId,
    string StatusPath,
    DateTimeOffset ExpiresAtUtc,
    double PollAfterSeconds,
    bool CallbackAvailable);
