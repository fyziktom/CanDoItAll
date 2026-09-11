using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class ImageGenerationResultDisclosureService(
    IAgentToolAdmissionVerifier admissions,
    IAgentExecutionAuthorityResolver authorityResolver,
    IAgentCatalogReadLeaseStore catalogLeases,
    ICanonicalRuntimeDatabase database,
    IDatabaseRuntimeState runtimeState,
    IProviderRuntimeProfileSource providers,
    IWorkspacePathResolutionService workspacePaths,
    ProjectWriteAdmissionService projects,
    IPhysicalFileSystemPathPolicyFactory pathPolicyFactory) {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        Converters = { new JsonStringEnumConverter() }
    };

    internal static bool UsesJournal(AgentRuntimeToolProviderContext context)
        => context.AdmittedToolSession is not null && context.ToolAdmissionSupport == AgentToolAdmissionSupport.Recoverable;

    internal IReadOnlyList<AITool> Wrap(AgentRuntimeToolProviderContext context, IReadOnlyList<AITool> tools)
        => tools.Select(tool => tool is AIFunction function
            ? (AITool)new DisclosureFunction(function, this, context) : tool).ToArray();

    internal async ValueTask<IAsyncDisposable?> AuthorizeAsync(AgentRuntimeToolProviderContext context,
        AgentToolResultDisclosure disclosure, CancellationToken cancellationToken) {
        var session = await RequireSessionAsync(context, cancellationToken);
        if (disclosure.Payload.ToolName != ImageGenerationToolPolicy.ImageGenerationCreate || disclosure.Evidence is null) {
            throw Unavailable();
        }
        ImageGenerationDisclosureEvidence evidence;
        ImageGenerationCreateResult result;
        try {
            evidence = ImageGenerationDisclosureEvidenceCodec.Read(disclosure.Evidence);
            result = disclosure.Result.Deserialize<ImageGenerationCreateResult>(Json) ?? throw Unavailable();
        } catch (Exception exception) when (exception is InvalidDataException or JsonException or ArgumentException) {
            throw Unavailable();
        }
        if (evidence.State != ImageGenerationDisclosureState.Complete) {
            throw Unavailable();
        }
        if (evidence.AgentId != session.AgentId || evidence.DatabaseProfileId != session.Profile.ProfileId ||
                evidence.Session != session.Reference || evidence.ProviderProfileId != result.ProviderProfileId ||
                evidence.Output.RelativePath != NormalizePath(result.OutputWorkspacePath) || !result.Success) {
            throw Denied();
        }
        var provider = await providers.GetProviderAsync(evidence.ProviderProfileId, cancellationToken);
        if (provider is not { IsEnabled: true, Purpose: ProviderProfilePurpose.ImageGeneration }) {
            throw Denied();
        }
        await RequireSessionAsync(context, cancellationToken);
        var held = await catalogLeases.AcquireAgentReadLeaseAsync(session.AgentId, cancellationToken);
        try {
            var actor = held.Agent;
            if (held.Scope != WorkspaceScopeDescriptor.Organization(session.Profile.ProfileId.ToString("N")) ||
                    actor is null || actor.Id != session.AgentId || actor.IsTemplate || actor.Status != AgentLifecycleStatus.Active ||
                    !actor.Permissions.CanUseTools || !AgentImageGenerationAccessMetadata.Read(actor.ConfigurationJson).CanGenerateImages) {
                throw Denied();
            }
            var access = AgentProjectStructureAccessMetadata.Read(actor.ConfigurationJson);
            foreach (var original in evidence.SourceProjects) {
                if (!access.CanRead || !access.AllowAllProjects &&
                        (!access.AllowedProjectIds.Contains(original.ProjectId) || !access.AllowedProjectLifetimes.Contains(
                            new(original.DatabaseProfileId, original.ProjectId, original.LifetimeId))) ||
                        await projects.CaptureAsync(original.ProjectId, cancellationToken) != original) {
                    throw Denied();
                }
            }
            foreach (var original in evidence.SourcePaths.Append(evidence.Output)) {
                WorkspaceResolvedPath current;
                try {
                    current = workspacePaths.ResolveFilePath(original.RelativePath, allowMissing: true);
                } catch (WorkspacePathResolutionException) {
                    throw Denied();
                }
                if (!current.IsWorkspacePath || NormalizePath(current.RelativePath) != original.RelativePath ||
                        !PathEquals(current.FullPath, original.FullPath)) {
                    throw Denied();
                }
            }
            return held;
        } catch {
            await held.DisposeAsync();
            throw;
        }
    }

    private async Task<ImageGenerationDisclosureCapture> CaptureAsync(AgentRuntimeToolProviderContext context,
        AIFunctionArguments arguments, CancellationToken cancellationToken) {
        var session = await RequireSessionAsync(context, cancellationToken);
        if (!arguments.TryGetValue("request", out var value) || value is null) {
            throw Unavailable();
        }
        var request = value as ImageGenerationCreateInput ?? (value is JsonElement element ? element
            : JsonSerializer.SerializeToElement(value, Json)).Deserialize<ImageGenerationCreateInput>(Json) ?? throw Unavailable();
        var sourceIds = (request.SourceProjectAssets ?? []).Select(source => source.ProjectId).Distinct().ToArray();
        var paths = (request.SourceWorkspacePaths ?? []).Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.Ordinal).ToArray();
        if (sourceIds.Length + paths.Length > ImageGenerationDisclosureEvidenceCodec.MaximumSources) {
            return new(session, ImageGenerationDisclosureState.SourceLimitExceeded, [], []);
        }
        var sourceProjects = new List<ProjectWriteAdmission>();
        var state = ImageGenerationDisclosureState.Complete;
        foreach (var id in sourceIds) {
            var admission = id == Guid.Empty ? null : await projects.CaptureAsync(id, cancellationToken);
            if (admission is null || admission.DatabaseProfileId != session.Profile.ProfileId) {
                state = ImageGenerationDisclosureState.MissingOriginalLifetime;
            } else {
                sourceProjects.Add(admission);
            }
        }
        var resolvedPaths = new List<ImageGenerationDisclosurePath>();
        foreach (var path in paths) {
            try {
                var resolved = workspacePaths.ResolveFilePath(path, allowMissing: false);
                if (resolved.IsWorkspacePath) {
                    resolvedPaths.Add(new(NormalizePath(resolved.RelativePath), Path.GetFullPath(resolved.FullPath)));
                } else {
                    state = ImageGenerationDisclosureState.MissingOriginalPath;
                }
            } catch (WorkspacePathResolutionException) {
                state = ImageGenerationDisclosureState.MissingOriginalPath;
            }
        }
        return new(session, state, sourceProjects.ToImmutableArray(), resolvedPaths.ToImmutableArray());
    }

    private async Task CompleteAsync(ImageGenerationDisclosureCapture capture, ImageGenerationCreateResult result,
        CancellationToken cancellationToken) {
        var state = capture.State;
        foreach (var original in capture.SourceProjects) {
            if (await projects.CaptureAsync(original.ProjectId, cancellationToken) != original) {
                state = ImageGenerationDisclosureState.LifetimeChangedDuringOperation;
            }
        }
        var evidence = new ImageGenerationDisclosureEvidence(capture.Session.Reference, capture.Session.Profile.ProfileId,
            capture.Session.AgentId, state, result.ProviderProfileId, CapturePath(result.OutputWorkspacePath, allowMissing: true),
            capture.SourceProjects, capture.SourcePaths);
        AgentToolInvocationEffectScope.RecordDisclosureEvidence(ImageGenerationDisclosureEvidenceCodec.Write(evidence));
    }

    private ImageGenerationDisclosurePath CapturePath(string path, bool allowMissing) {
        var resolution = workspacePaths.ResolveFilePath(path, allowMissing);
        if (!resolution.IsWorkspacePath) {
            throw Denied();
        }
        return new(NormalizePath(resolution.RelativePath), Path.GetFullPath(resolution.FullPath));
    }

    private async Task<AgentToolSessionAdmission> RequireSessionAsync(AgentRuntimeToolProviderContext context,
        CancellationToken cancellationToken) {
        if (!UsesJournal(context)) {
            throw Denied();
        }
        var observation = await admissions.RequireSessionObservationAsync(context.AdmittedToolSession!, cancellationToken);
        var session = observation.Session;
        var profile = runtimeState.GetSnapshot();
        if (session.Reference != context.AdmittedToolSession || session.AgentId != context.Agent.Id ||
                session.Profile.ProfileId != database.Profile.Profile.Id || session.Profile.ProfileId != profile.ActiveProfileId ||
                session.Profile.Fingerprint != profile.ActiveFingerprint || session.Profile.Generation.Value != profile.Generation) {
            throw Denied();
        }
        if (session.Reference.BackgroundSource is not null) {
            if (session.Purpose != AgentRuntimeContextPurpose.GovernedProcessAutomation ||
                    context.Purpose != AgentRuntimeToolProviderPurpose.GovernedProcessAutomation) {
                throw Denied();
            }
            return session;
        }
        var source = observation.TurnContext;
        var original = observation.Governance;
        if (source is null || original is not { ReadAllowed: true } ||
                original.AgentId != session.AgentId || original.AuthorityId != session.Reference.AuthorityId ||
                original.DatabaseProfileId != session.Profile.ProfileId || original.DatabaseProfileGeneration != session.Profile.Generation ||
                context.Purpose != AgentRuntimeToolProviderPurpose.InteractiveChat ||
                context.Governance is not { } supplied || supplied.AuthorityId != original.AuthorityId || supplied.WorkspaceScope != original.WorkspaceScope) {
            throw Denied();
        }
        AgentExecutionAuthorityRecord current;
        try {
            current = await authorityResolver.ResolveAsync(AgentExecutionAuthorityResolutionRequest.FromCaptured(source, original), cancellationToken);
        } catch (Exception exception) when (exception is AgentExecutionAuthorityMismatchException or AgentChatContextAccessDeniedException) {
            throw Denied();
        }
        if (!current.ReadAllowed || current.AgentId != session.AgentId || current.DatabaseProfileId != session.Profile.ProfileId ||
                current.DatabaseProfileGeneration != session.Profile.Generation || current.WorkspaceScope != original.WorkspaceScope) {
            throw Denied();
        }
        return session;
    }

    private static string NormalizePath(string value) => value.Replace('\\', '/');
    private bool PathEquals(string left, string right) {
        var fullLeft = Path.GetFullPath(left);
        var directory = Path.GetDirectoryName(fullLeft) ?? throw Denied();
        return pathPolicyFactory.Create(directory).PathComparer.Equals(fullLeft, Path.GetFullPath(right));
    }
    private static AgentToolAdmissionException Unavailable() => new("image-generation.result-authority-unavailable",
        "The saved image result lacks supported original source authority. It cannot be disclosed or regenerated by recovery.");
    private static AgentToolAdmissionException Denied() => new("image-generation.result-disclosure-denied",
        "Current read authority does not permit the original image result and source boundaries.");

    private sealed record ImageGenerationDisclosureCapture(AgentToolSessionAdmission Session, ImageGenerationDisclosureState State,
        ImmutableArray<ProjectWriteAdmission> SourceProjects, ImmutableArray<ImageGenerationDisclosurePath> SourcePaths);

    private sealed class DisclosureFunction(AIFunction inner, ImageGenerationResultDisclosureService owner,
        AgentRuntimeToolProviderContext context) : DelegatingAIFunction(inner) {
        protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken) {
            var capture = await owner.CaptureAsync(context, arguments, cancellationToken);
            var result = await base.InvokeCoreAsync(arguments, cancellationToken);
            var generated = result switch {
                ImageGenerationCreateResult typed => typed,
                JsonElement element => element.Deserialize<ImageGenerationCreateResult>(Json) ?? throw Unavailable(),
                _ => throw Unavailable()
            };
            await owner.CompleteAsync(capture, generated, cancellationToken);
            return result;
        }
    }
}
