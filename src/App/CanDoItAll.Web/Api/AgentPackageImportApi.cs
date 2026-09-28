using System.ComponentModel;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;

internal static class AgentPackageImportApi
{
    private const long MaximumRequestBytes = AgentPackageReadOptions.DefaultMaximumPackageBytes + (1024 * 1024);

    public static RouteGroupBuilder MapAgentPackageImportApi(this RouteGroupBuilder agents)
    {
        agents.MapPost("/import-package", ImportAsync)
            .WithName("ImportAgentPackage")
            .Accepts<AgentPackageImportApiForm>("multipart/form-data")
            .Produces<AgentPackageImportReceipt>(StatusCodes.Status200OK)
            .Produces<AgentPackageImportReceipt>(StatusCodes.Status201Created)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status412PreconditionFailed)
            .ProducesApiErrors(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden)
            .WithMetadata(
                new RequestSizeLimitAttribute(MaximumRequestBytes),
                new RequestFormLimitsAttribute
                {
                    MultipartBodyLengthLimit = MaximumRequestBytes
                });

        return agents;
    }

    /// <summary>
    /// Import an agent package file to create, replace or clone an agent bound to an external identity.
    /// </summary>
    /// <remarks>
    /// Reads a ZIP agent package, as produced by the agent export, and adds its agent to the current workspace in one
    /// of three modes. The resulting agent is bound to an external identity (namespace and key) that
    /// <c>GET /api/agents/by-external-key/{externalNamespace}/{key}</c> resolves afterwards. To provision an agent
    /// from a JSON definition instead of a package, use
    /// <c>PUT /api/agents/by-external-key/{externalNamespace}/{key}</c>.
    ///
    /// Send <c>multipart/form-data</c> with the file part <c>package</c> and the text fields <c>mode</c>,
    /// <c>externalKey</c> and, optionally, <c>externalNamespace</c>, <c>expectedPackageSha256</c> and
    /// <c>expectedAgentVersion</c>, together with the <c>Idempotency-Key</c> header. The package may have at most 32
    /// MiB (33554432 bytes), expand to at most 128 MiB and contain only the entries <c>manifest.json</c> (package
    /// schema version 1.0, at most 8 MiB), <c>agent.md</c>, <c>instructions.txt</c>, <c>memory.json</c>,
    /// <c>sessions.json</c> and <c>metrics.json</c>. Symbolic links, executable entries and raw secret values in the
    /// manifest (a non-empty <c>apiKey</c>, <c>accessToken</c>, <c>bearerToken</c>, <c>clientSecret</c>,
    /// <c>password</c>, <c>privateKey</c> or <c>refreshToken</c> property) are rejected. A whole request larger than
    /// 33 MiB is rejected before the operation runs, without an error envelope.
    ///
    /// Modes:
    ///
    /// - <c>create</c>: adds the packaged agent under its own identifier, together with its packaged chat sessions,
    /// execution runs, memory and metrics. Fails if an agent with that identifier exists.
    /// - <c>replace-exact-version</c>: replaces the existing agent with the packaged identifier, whose current revision
    /// must equal <c>expectedAgentVersion</c>. Its chat sessions, execution runs, memory, metrics and other run
    /// records are deleted and replaced by the packaged ones.
    /// - <c>clone</c>: adds only the packaged definition, under a new identifier, as a non-template agent with a new
    /// template key. No history is copied.
    ///
    /// Provider and capability references are matched to this workspace by identifier or by a unique identity;
    /// unmatched ones are dropped and listed in the receipt's <c>unresolvedPrerequisites</c>. Secret references are
    /// always removed. Read the agent with <c>GET /api/agents/{agentId}</c> afterwards and configure what is missing.
    /// A rejected request imports nothing. An HTTP 500 can follow a completed import when the CRM/HR directory refresh
    /// after the save fails; resend the identical request with the same <c>Idempotency-Key</c> to receive the stored
    /// receipt.
    ///
    /// Idempotency: repeating the request with the same <c>Idempotency-Key</c>, package, mode, external key and
    /// expected version returns the stored receipt with <c>replayed</c> true and imports nothing again; the same key
    /// with different values is rejected with <c>agent-package.idempotency-conflict</c>. The last 2048 imports of the
    /// workspace are remembered.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy.
    /// </remarks>
    /// <param name="request">
    /// The multipart form: the package file part <c>package</c> and the import mode, external identity and optional
    /// preconditions as text fields.
    /// </param>
    /// <response code="200">
    /// The agent was replaced (<c>replace-exact-version</c>), or the request repeated an earlier import with the same
    /// <c>Idempotency-Key</c> (<c>replayed</c> is true and nothing was imported again). The body is the receipt.
    /// </response>
    /// <response code="201">
    /// A new agent was imported (<c>create</c> or <c>clone</c>). The body is the receipt; the <c>Location</c> header
    /// is <c>/api/agents/{agentId}</c> of the new agent.
    /// </response>
    /// <response code="400">
    /// Nothing was imported. The request is incomplete or invalid: <c>agent-package.file-required</c>,
    /// <c>agent-package.too-large</c>, <c>agent-package.mode-invalid</c>,
    /// <c>agent-package.idempotency-key-invalid</c> (missing, blank or longer than 200 characters),
    /// <c>agent-package.external-key-invalid</c>, <c>agent-package.external-identity-invalid</c> (namespace or key
    /// format) or <c>agent-package.expected-version-required</c>. Or the package is not acceptable:
    /// <c>agent-package.expected-hash-invalid</c>, <c>agent-package.hash-mismatch</c>,
    /// <c>agent-package.invalid-archive</c>, <c>agent-package.empty</c>, <c>agent-package.too-many-entries</c>,
    /// <c>agent-package.entry-not-allowed</c>, <c>agent-package.duplicate-entry</c>,
    /// <c>agent-package.symlink-not-allowed</c>, <c>agent-package.executable-not-allowed</c>,
    /// <c>agent-package.expanded-size-exceeded</c>, <c>agent-package.manifest-missing</c>,
    /// <c>agent-package.manifest-too-large</c>, <c>agent-package.manifest-invalid</c>,
    /// <c>agent-package.schema-version-unsupported</c>, <c>agent-package.raw-secret-material</c>,
    /// <c>agent-package.runtime-admission-not-portable</c> (a packaged run carries a tool admission journal) or
    /// <c>agent-package.agent-invalid</c> (the packaged agent has no identifier or name).
    /// </response>
    /// <response code="409">
    /// Nothing was imported because it conflicts with the workspace: <c>agent-package.idempotency-conflict</c> (the
    /// key was used with different values), <c>agent-package.external-key-conflict</c> (the external identity is
    /// already bound and this is not a <c>replace-exact-version</c> of that same agent),
    /// <c>agent-package.agent-already-exists</c> (<c>create</c> of an existing agent),
    /// <c>agent-package.agent-not-found</c> (<c>replace-exact-version</c> without an agent to replace) or
    /// <c>agent-package.template-key-conflict</c> (another agent uses the packaged template key).
    /// </response>
    /// <response code="412">
    /// Nothing was imported: <c>expectedAgentVersion</c> does not equal the current revision of the agent to replace
    /// (<c>agent-package.version-conflict</c>). Read the agent again and decide whether to replace it.
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// An authorization policy rejected the authenticated caller (<c>api.authorization-forbidden</c>). This operation
    /// itself requires only a valid bearer token.
    /// </response>
    internal static async Task<IResult> ImportAsync(
        [FromForm] AgentPackageImportApiForm request,
        [FromHeader(Name = "Idempotency-Key")]
        [Description(
            "Required. Client-chosen key of this import, 1 to 200 characters after trimming, compared exactly. " +
            "Repeating the same request with the same key returns the stored receipt instead of importing again; " +
            "reusing the key with different values is rejected with agent-package.idempotency-conflict.")]
        string? idempotencyKey,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken)
    {
        if (request.Package is null || request.Package.Length == 0)
        {
            return ApiEndpointResults.BadRequest(
                "A non-empty package file is required.",
                "agent-package.file-required");
        }

        if (request.Package.Length > AgentPackageReadOptions.DefaultMaximumPackageBytes)
        {
            return ApiEndpointResults.BadRequest(
                $"The package exceeds the {AgentPackageReadOptions.DefaultMaximumPackageBytes}-byte limit.",
                "agent-package.too-large");
        }

        if (!TryParseMode(request.Mode, out var mode))
        {
            return ApiEndpointResults.BadRequest(
                "Mode must be create, replace-exact-version, or clone.",
                "agent-package.mode-invalid");
        }

        try
        {
            await using var package = request.Package.OpenReadStream();
            var receipt = await workspaceService.ImportAgentPackageAsync(
                package,
                new AgentPackageImportCommand(
                    mode,
                    idempotencyKey ?? string.Empty,
                    request.ExternalKey ?? string.Empty,
                    request.ExpectedPackageSha256,
                    request.ExpectedAgentVersion,
                    request.ExternalNamespace ?? AgentExternalIdentityNormalizer.PackageImportNamespace),
                cancellationToken);

            return receipt.Replayed || mode == AgentPackageImportMode.ReplaceExactVersion
                ? Results.Ok(receipt)
                : Results.Created($"/api/agents/{receipt.AgentId:D}", receipt);
        }
        catch (AgentPackageValidationException exception)
        {
            return Error(StatusCodes.Status400BadRequest, exception.Code, exception.Message);
        }
        catch (AgentPackageImportException exception)
        {
            var statusCode = exception.Kind switch
            {
                AgentPackageImportFailureKind.Conflict => StatusCodes.Status409Conflict,
                AgentPackageImportFailureKind.PreconditionFailed => StatusCodes.Status412PreconditionFailed,
                _ => StatusCodes.Status400BadRequest
            };
            return Error(statusCode, exception.Code, exception.Message);
        }
    }

    private static bool TryParseMode(string? value, out AgentPackageImportMode mode)
    {
        mode = value?.Trim().ToLowerInvariant() switch
        {
            "create" => AgentPackageImportMode.Create,
            "replace-exact-version" => AgentPackageImportMode.ReplaceExactVersion,
            "clone" => AgentPackageImportMode.Clone,
            _ => (AgentPackageImportMode)(-1)
        };
        return Enum.IsDefined(mode);
    }

    private static IResult Error(int statusCode, string code, string message)
    {
        return Results.Json(
            new ApiErrorResponse([new ApiErrorItem(code, message, ErrorSeverity.Error)]),
            statusCode: statusCode);
    }
}

/// <summary>
/// Multipart form of an agent package import: the package file part and text fields that choose the import mode,
/// the external identity to bind and optional preconditions.
/// </summary>
internal sealed class AgentPackageImportApiForm
{
    /// <summary>
    /// The agent package (required, not empty): a ZIP archive as produced by the agent export, at most 32 MiB
    /// (33554432 bytes).
    /// </summary>
    public IFormFile? Package { get; set; }

    /// <summary>
    /// Import mode (required), case-insensitive: <c>create</c> (add the packaged agent under its own identifier, with
    /// its history), <c>replace-exact-version</c> (replace the existing agent with that identifier and its history;
    /// requires <c>expectedAgentVersion</c>) or <c>clone</c> (add only the definition, under a new identifier).
    /// </summary>
    public string? Mode { get; set; }

    /// <summary>
    /// Key of the external identity to bind to the imported agent (required): after trimming and lowering, 1 to 100
    /// characters of lowercase letters, digits, <c>.</c>, <c>_</c> or <c>-</c>, starting and ending with a letter or
    /// digit.
    /// </summary>
    public string? ExternalKey { get; set; }

    /// <summary>
    /// Namespace of that external identity, in the same format as <c>externalKey</c>; <c>package-import</c> when
    /// omitted.
    /// </summary>
    public string? ExternalNamespace { get; set; }

    /// <summary>
    /// Optional SHA-256 of the package file, 64 hexadecimal digits in any case. When set, a package with a different
    /// hash is rejected (<c>agent-package.hash-mismatch</c>).
    /// </summary>
    public string? ExpectedPackageSha256 { get; set; }

    /// <summary>
    /// Current revision of the agent to replace, an instant with offset. Required for <c>replace-exact-version</c>
    /// and not used as a precondition by the other modes. Send the <c>expectedUpdatedAtUtc</c> value returned by
    /// <c>GET /api/agents/{agentId}</c>, or the <c>importedVersion</c> of the previous import receipt, unchanged; it
    /// must equal the stored revision exactly (<c>agent-package.version-conflict</c>, HTTP 412).
    /// </summary>
    public DateTimeOffset? ExpectedAgentVersion { get; set; }
}
