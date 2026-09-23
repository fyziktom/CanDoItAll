using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.FileSystem;
using Microsoft.Agents.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class MafSkillResultDisclosure(IAgentWorkspaceToolResultSource? source,
    AgentRuntimeToolProviderContext context, string workspaceRoot, WorkspaceScopeDescriptor workspaceScope,
    AgentToolSemanticDigest readConfiguration,
    Func<IAgentWorkspaceToolResultReadLease, AgentToolSemanticDigest> currentReadConfiguration,
    IPhysicalFileSystemPathPolicyFactory paths) {
    internal async ValueTask RecordAsync(MafSkillSourceEntry selected, MafSkillResultKind kind, string name,
        string path, CancellationToken cancellationToken) {
        var proposal = AgentToolInvocationClaim.Current?.Proposal ?? throw Unavailable();
        var prepared = MafSkillSourceEvidence.Read(proposal.Payload.SourcePreparation ?? throw Unavailable());
        if (!prepared.Candidates.Contains(selected)) {
            throw Unavailable();
        }
        var completedSource = await RequireSource().CompleteAsync(prepared.Source, cancellationToken);
        var evidence = new MafSkillResultEvidence(proposal.IntentId, proposal.Payload.Digest, completedSource,
            readConfiguration, selected, kind, name, path, CurrentExecutionScope());
        if (!IsSupported(evidence, prepared)) {
            evidence = evidence with { Kind = MafSkillResultKind.Unsupported };
        }
        AgentToolInvocationEffectScope.RecordDisclosureEvidence(evidence.Write());
    }

    internal async ValueTask<IAsyncDisposable?> AuthorizeAsync(AgentToolResultDisclosure disclosure,
        CancellationToken cancellationToken) {
        MafSkillSourceEvidence prepared;
        MafSkillResultEvidence result;
        try {
            prepared = MafSkillSourceEvidence.Read(disclosure.Payload.SourcePreparation ?? throw Unavailable());
            result = MafSkillResultEvidence.Read(disclosure.Evidence ?? throw Unavailable());
        } catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidDataException) {
            throw Unavailable();
        }
        if (result.IntentId != disclosure.IntentId || result.PreparedDigest != disclosure.Payload.Digest || result.Source != prepared.Source ||
                !prepared.Candidates.Contains(result.Selected) || !MatchesArguments(disclosure.Payload, result)) {
            throw Unavailable();
        }
        if (prepared.WorkspaceRoot != workspaceRoot || result.ExecutionWorkspaceScope != CurrentExecutionScope() || !IsSupported(result, prepared)) {
            throw Unavailable();
        }
        var held = await RequireSource().AcquireReadAsync(context, workspaceScope, result.Source, cancellationToken);
        try {
            if (currentReadConfiguration(held) != result.ReadConfiguration) {
                throw Denied();
            }
            if (!IsSupported(result, prepared)) {
                throw Denied();
            }
            held.RequireCurrent();
            return held;
        } catch {
            await held.DisposeAsync();
            throw;
        }
    }

    internal static AgentToolSemanticDigest ConfigurationDigest(AgentRuntimeConfiguration configuration,
        IReadOnlyList<CapabilityCatalogItem> capabilities, IReadOnlyList<string> roots, AgentWorkspaceToolAccessSettings access)
        => MafSkillSourcePreparation.ConfigurationDigest(configuration, capabilities, roots,
            new AgentWorkspaceToolAccessSettings { CanReadFiles = access.CanReadFiles, CanRunLocalScripts = false });

    private bool IsSupported(MafSkillResultEvidence result, MafSkillSourceEvidence prepared) {
        if (result.Kind == MafSkillResultKind.Unsupported || result.Selected.Origin.Kind == MafSkillOriginKind.Registered) {
            return false;
        }
        if (result.Selected.Origin.Kind == MafSkillOriginKind.Inline) {
            return result.Kind is MafSkillResultKind.Content or MafSkillResultKind.InlineResource && result.FullPath == string.Empty;
        }
        if (result.Selected.Origin.Kind != MafSkillOriginKind.File ||
                result.Kind is not (MafSkillResultKind.Content or MafSkillResultKind.FileResource or MafSkillResultKind.FileScript)) {
            return false;
        }
        if (result.Kind == MafSkillResultKind.Content ? result.FullPath != string.Empty : !Path.IsPathFullyQualified(result.FullPath)) {
            return false;
        }
        var origin = result.Selected.Origin;
        try {
            var root = paths.Create(origin.Root);
            root.EnsureSafePath(origin.Location);
            var skill = paths.Create(origin.Location);
            var target = result.Kind == MafSkillResultKind.Content ? Path.Combine(origin.Location, "SKILL.md") : result.FullPath;
            skill.EnsureSafePath(target);
            var workspace = paths.Create(prepared.WorkspaceRoot);
            return !workspace.IsWithinRoot(target) || IsWithinSourceScopes(
                Path.GetRelativePath(prepared.WorkspaceRoot, target).Replace(Path.DirectorySeparatorChar, '/'), workspaceScope, result.ExecutionWorkspaceScope);
        } catch (PhysicalPathValidationException) {
            return false;
        }
    }

    private WorkspaceScopeDescriptor? CurrentExecutionScope() {
        var session = context.AdmittedToolSession ?? throw Unavailable();
        return WorkspaceExecutionAuditContext.RequireMatchingExecutionWorkspaceScope(session.ExecutionRunId, context.Agent.Id, workspaceScope);
    }

    internal static bool IsWithinSourceScopes(string relativePath, WorkspaceScopeDescriptor workspaceScope,
        WorkspaceScopeDescriptor? executionScope)
        => WorkspaceToolResultDisclosure.IsWithinCapturedScope(relativePath, workspaceScope) ||
           executionScope is not null && WorkspaceToolResultDisclosure.IsWithinCapturedScope(relativePath, executionScope);

    private static bool MatchesArguments(AgentToolPreparedPayload payload, MafSkillResultEvidence result) {
        using var parsed = JsonDocument.Parse(payload.ArgumentsJson);
        var arguments = parsed.RootElement;
        if (arguments.ValueKind != JsonValueKind.Object || !Matches("skillName", result.Selected.Name)) {
            return false;
        }
        return payload.ToolName switch {
            AgentSkillsProvider.LoadSkillToolName => result.Kind == MafSkillResultKind.Content && result.Name == string.Empty,
            AgentSkillsProvider.ReadSkillResourceToolName => result.Kind is MafSkillResultKind.FileResource or MafSkillResultKind.InlineResource &&
                Matches("resourceName", result.Name),
            AgentSkillsProvider.RunSkillScriptToolName => result.Kind == MafSkillResultKind.FileScript && Matches("scriptName", result.Name),
            _ => false
        };

        bool Matches(string key, string expected) => arguments.TryGetProperty(key, out var value) &&
            value.ValueKind == JsonValueKind.String && value.GetString() == expected;
    }

    private IAgentWorkspaceToolResultSource RequireSource() => source ?? throw Unavailable();
    internal static AgentToolAdmissionException Unavailable() => new("skill.result-authority-unavailable",
        "This saved skill result has no supported original owner and resource evidence. It cannot be disclosed or repeated automatically.");
    private static AgentToolAdmissionException Denied() => new("skill.result-disclosure-denied",
        "Current skill source, capability or workspace read authority does not permit the saved result.");
}

internal enum MafSkillResultKind { Unsupported, Content, FileResource, InlineResource, FileScript }

internal sealed record MafSkillResultEvidence(AgentToolBusinessIntentId IntentId, AgentToolSemanticDigest PreparedDigest,
    AgentToolProtocolEnvelope Source, AgentToolSemanticDigest ReadConfiguration, MafSkillSourceEntry Selected,
    MafSkillResultKind Kind, string Name, string FullPath, WorkspaceScopeDescriptor? ExecutionWorkspaceScope = null) {
    private const string Format = "maf-skill-result-source";
    private const int MaximumUtf8Bytes = 65_536;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal AgentToolProtocolEnvelope Write() {
        Validate();
        var json = JsonSerializer.Serialize(this, Json);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaximumUtf8Bytes) {
            throw MafSkillResultDisclosure.Unavailable();
        }
        return AgentToolProtocolEnvelope.Create(Format, 1, json);
    }

    internal static MafSkillResultEvidence Read(AgentToolProtocolEnvelope envelope) {
        if (envelope.Format != Format || envelope.Version != 1 ||
                System.Text.Encoding.UTF8.GetByteCount(envelope.PayloadJson) > MaximumUtf8Bytes) {
            throw MafSkillResultDisclosure.Unavailable();
        }
        var value = JsonSerializer.Deserialize<MafSkillResultEvidence>(envelope.PayloadJson, Json)
            ?? throw MafSkillResultDisclosure.Unavailable();
        value.Validate();
        return value;
    }

    private void Validate() {
        if (IntentId.Value == Guid.Empty || string.IsNullOrWhiteSpace(PreparedDigest.Value) ||
                string.IsNullOrWhiteSpace(ReadConfiguration.Value) || Source is null || Selected is null ||
                Selected.Origin is null || string.IsNullOrWhiteSpace(Selected.Name) || Selected.Name.Length > 512 ||
                !Enum.IsDefined(Kind) || Name is null || Name.Length > 4096 || FullPath is null || FullPath.Length > 8192) {
            throw MafSkillResultDisclosure.Unavailable();
        }
    }
}
