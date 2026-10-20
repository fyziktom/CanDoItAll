using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal sealed record ProjectDefinitionToolAgentSource(AgentProjectStructureAccessSettings Access,
    AgentExecutionGovernanceSnapshot Governance, ProjectWriteAdmission? SourceProject);

internal sealed record ProjectDefinitionToolSource(AgentToolSessionAdmission Session,
    DatasetNamespaceId Namespace, ProjectOperationEpoch Epoch, ProjectCallerBinding CallerBinding,
    ProjectDefinitionRequestVersion RequestVersion, ProjectDefinitionToolAgentSource? Agent,
    ProcessExecutionDispatchAuthority? Process) {
    internal ProjectMutationCallerScope Caller => Process is null
        ? ProjectDefinitionGovernedBindings.AgentCaller(Session) : ProjectDefinitionGovernedBindings.ProcessCaller(Process);

    internal ProjectAgentMutationAdmission AgentAdmission(string tool) {
        var original = Agent ?? throw ProjectDefinitionToolProposalCodec.Denied("The original Agent source is missing.");
        var domain = tool switch {
            ProjectStructureToolPolicy.ProjectStructureProjectUpdate => ProjectAgentMutationDomain.NonTaskStructure,
            ProjectStructureToolPolicy.ProjectStructureProjectCreate => ProjectAgentMutationDomain.ProjectCreation,
            ProjectStructureToolPolicy.ProjectStructureSubprojectCreate => ProjectAgentMutationDomain.SubprojectCreation,
            ProjectStructureToolPolicy.ProjectStructureSubprojectLink => ProjectAgentMutationDomain.Hierarchy,
            _ => throw ProjectDefinitionToolProposalCodec.Denied("The original definition tool is unsupported.")
        };
        return new(Session.AgentId, Session.Profile.ProfileId, original.Access, original.Governance, domain, tool, original.SourceProject);
    }

    internal ProjectDefinitionOperation Operation(AgentToolBusinessIntentId intent) => new(Namespace, Epoch, intent.Value, CallerBinding);
}

internal sealed record ProjectDefinitionToolArguments(ProjectStructureProjectSaveRequest Request,
    Guid? ProjectId = null, Guid? ParentProjectId = null, int? EstimatedMinutes = null);

internal static class ProjectDefinitionToolProposalCodec {
    internal const string SourceFormat = "projects-definition-source";
    internal const int SemanticVersion = 2;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    internal static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web) {
        MaxDepth = 32,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(), new ProjectProcessStepInstanceIdJsonConverter() }
    };

    internal static bool Supports(string tool) => tool is ProjectStructureToolPolicy.ProjectStructureProjectCreate or
        ProjectStructureToolPolicy.ProjectStructureSubprojectCreate or ProjectStructureToolPolicy.ProjectStructureProjectUpdate;

    internal static AgentToolPreparedPayload Prepare(string tool, JsonElement arguments, AgentToolProtocolEnvelope source) {
        var saved = ReadSource(source);
        var raw = arguments.GetRawText();
        RequireBytes(raw, ProjectDefinitionLimits.VersionOneMaximum.MaxRawRequestBytes);
        var input = arguments.Deserialize<ProjectDefinitionToolArguments>(Json) ?? throw Denied("The definition arguments are missing.");
        Validate(tool, input, saved.Session.Profile.ProfileId);
        var canonical = JsonSerializer.Serialize(input, Json);
        RequireBytes(canonical, ProjectDefinitionLimits.VersionOneMaximum.MaxRawRequestBytes);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, "projects:tool-proposal:v2");
        Append(hash, tool);
        Append(hash, canonical);
        Append(hash, source.Format);
        Append(hash, source.Version.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Append(hash, source.PayloadJson);
        return new(tool, SemanticVersion, new(Convert.ToHexStringLower(hash.GetHashAndReset())), canonical,
            AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.OwnerReceipt, source);
    }

    internal static (ProjectDefinitionToolSource Source, ProjectDefinitionToolArguments Input) Read(AgentToolPreparedPayload payload) {
        if (!Supports(payload.ToolName) || payload.SemanticVersion != SemanticVersion ||
            payload.Effect != AgentToolProposalEffect.Mutation || payload.Recovery != AgentToolProposalRecovery.OwnerReceipt ||
            payload.SourcePreparation is not { } source) {
            throw Denied("The saved definition proposal lacks its supported original operation source.");
        }
        RequireBytes(payload.ArgumentsJson, ProjectDefinitionLimits.VersionOneMaximum.MaxRawRequestBytes);
        using var arguments = JsonDocument.Parse(payload.ArgumentsJson, new() { MaxDepth = Json.MaxDepth });
        if (Prepare(payload.ToolName, arguments.RootElement, source) != payload) {
            throw Denied("The saved definition proposal differs from its original canonical interpretation.");
        }
        return (ReadSource(source), arguments.RootElement.Deserialize<ProjectDefinitionToolArguments>(Json)!);
    }

    internal static AgentToolProtocolEnvelope WriteSource(ProjectDefinitionToolSource source) {
        ValidateSource(source);
        var json = JsonSerializer.Serialize(source, Json);
        RequireBytes(json, AgentToolPreparedPayload.MaximumSourcePreparationUtf8Bytes);
        return AgentToolProtocolEnvelope.Create(SourceFormat, 1, json);
    }

    internal static ProjectDefinitionToolSource ReadSource(AgentToolProtocolEnvelope envelope) {
        if (envelope.Format != SourceFormat || envelope.Version != 1) {
            throw Denied("The original definition source interpretation is unsupported.");
        }
        RequireBytes(envelope.PayloadJson, AgentToolPreparedPayload.MaximumSourcePreparationUtf8Bytes);
        var source = JsonSerializer.Deserialize<ProjectDefinitionToolSource>(envelope.PayloadJson, Json)
            ?? throw Denied("The original definition source is missing.");
        ValidateSource(source);
        if (JsonSerializer.Serialize(source, Json) != envelope.PayloadJson) {
            throw Denied("The original definition source is not canonical.");
        }
        return source;
    }

    internal static Guid ReservedProjectId(ProjectDefinitionToolSource source, AgentToolBusinessIntentId intent) {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, "projects:governed-reserved-project:v1");
        Append(hash, source.Namespace.Value.ToString("D"));
        Append(hash, ((int)source.Caller.Kind).ToString(System.Globalization.CultureInfo.InvariantCulture));
        hash.AppendData(source.Caller.Key.ToArray());
        Append(hash, intent.Value.ToString("D"));
        return new Guid(hash.GetHashAndReset().AsSpan(0, 16), bigEndian: true);
    }

    internal static ProjectDefinitionScalars Scalars(ProjectStructureProjectSaveRequest request)
        => new(request.Name, request.Description, request.Objective, request.Status, request.CurrentPhase, request.TargetDateUtc);

    private static void Validate(string tool, ProjectDefinitionToolArguments input, Guid profile) {
        if (!Supports(tool) || input.Request is null) {
            throw Denied("The definition tool or request is unsupported.");
        }
        ProjectDefinitionInputBounds.Validate(Scalars(input.Request), null, null, null, ProjectDefinitionLimits.VersionOneMaximum);
        var valid = tool switch {
            ProjectStructureToolPolicy.ProjectStructureProjectUpdate => input.ParentProjectId is null && input.Request.ExpectedParentAdmission is null &&
                input.ProjectId is { } id && id != Guid.Empty && input.Request.ExpectedDefinition is { } expected &&
                expected.Project.DatabaseProfileId == profile && expected.Project.ProjectId == id,
            ProjectStructureToolPolicy.ProjectStructureProjectCreate => input.ProjectId is null && input.ParentProjectId is null &&
                input.Request.ExpectedDefinition is null && input.Request.ExpectedParentAdmission is null,
            ProjectStructureToolPolicy.ProjectStructureSubprojectCreate => input.ProjectId is null && input.ParentProjectId is { } parent &&
                parent != Guid.Empty && input.Request.ExpectedDefinition is null && input.Request.ExpectedParentAdmission is { } original &&
                original.DatabaseProfileId == profile && original.ProjectId == parent,
            _ => false
        };
        if (!valid) {
            throw Denied("The definition request must preserve its original target expectation or subproject parent admission.");
        }
    }

    private static void ValidateSource(ProjectDefinitionToolSource value) {
        if (value.Session is null || value.Namespace is null || value.Epoch is null || value.CallerBinding is null ||
            value.RequestVersion != ProjectDefinitionRequestVersion.VersionOne || (value.Agent is null) == (value.Process is null)) {
            throw Denied("The original definition source shape is invalid.");
        }
        if (value.Agent is { } agent) {
            if (value.Session.Reference.BackgroundSource is not null || value.Session.Purpose != AgentRuntimeContextPurpose.InteractiveChat ||
                agent.Access is null || agent.Governance is null || agent.Access.AllowedProjectIds.Count > 1024 ||
                agent.Access.AllowedProjectLifetimes.Count > 1024 || agent.Governance.AgentId != value.Session.AgentId ||
                agent.Governance.AuthorityId != value.Session.Reference.AuthorityId ||
                agent.Governance.DatabaseProfileId != value.Session.Profile.ProfileId ||
                agent.Governance.DatabaseProfileGeneration != value.Session.Profile.Generation) {
                throw Denied("The original definition Agent source differs from its admitted session.");
            }
        }
        if (value.Process is { } process && (value.Session.Purpose != AgentRuntimeContextPurpose.GovernedProcessAutomation ||
            value.Session.Reference.BackgroundSource is not { } background || process.SourceAuthority is null || process.ProjectReference is null ||
            process.Evidence.ExecutionRunId != value.Session.Reference.ExecutionRunId || process.Evidence.ExecutorAgentId != value.Session.AgentId ||
            process.SourceAuthority.DatabaseProfileId != value.Session.Profile.ProfileId || background.SourceId != process.Evidence.StepKey ||
            background.OwnerFingerprint != AgentToolProtocolEnvelope.ComputeDigest(process.OwnerFingerprint))) {
            throw Denied("The original definition Process source differs from its admitted session.");
        }
        ProjectDefinitionGovernedBindings.RequireSourceBounds(value.Agent?.Governance, value.Process);
        if (!value.Caller.Matches(value.Namespace, value.CallerBinding)) {
            throw Denied("The original definition caller binding is invalid.");
        }
    }

    private static void Append(IncrementalHash hash, string value) {
        var bytes = Utf8.GetBytes(value);
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    private static void RequireBytes(string value, int maximum) {
        if (value.Length > maximum || Utf8.GetByteCount(value) > maximum) {
            throw Denied("The definition proposal or original source exceeds its supported byte bound.");
        }
    }

    internal static AgentToolAdmissionException Denied(string message) => new("projects.definition-tool-admission-denied", message);
}
