using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

internal sealed record ProjectLifecycleToolArguments(Guid ParentProjectId, ProjectStructureSubprojectChangeRequest Request,
    int? EstimatedMinutes = null);

internal static class ProjectLifecycleToolProposalCodec {
    internal const int SemanticVersion = 2;
    internal const int MaximumArgumentUtf8Bytes = 4096;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    internal static bool Supports(string tool) => tool == ProjectStructureToolPolicy.ProjectStructureSubprojectLink;

    internal static AgentToolPreparedPayload Prepare(string tool, JsonElement arguments, AgentToolProtocolEnvelope source) {
        if (!Supports(tool)) {
            throw Denied("The hierarchy tool is unsupported.");
        }
        var saved = ProjectDefinitionToolProposalCodec.ReadSource(source);
        RequireBytes(arguments.GetRawText());
        var input = arguments.Deserialize<ProjectLifecycleToolArguments>(ProjectDefinitionToolProposalCodec.Json)
            ?? throw Denied("The original hierarchy arguments are missing.");
        _ = Command(input, saved.Session.Profile.ProfileId);
        var canonical = JsonSerializer.Serialize(input, ProjectDefinitionToolProposalCodec.Json);
        RequireBytes(canonical);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, "projects:hierarchy-tool-proposal:v2");
        Append(hash, tool);
        Append(hash, canonical);
        Append(hash, source.Format);
        Append(hash, source.Version.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Append(hash, source.PayloadJson);
        return new(tool, SemanticVersion, new(Convert.ToHexStringLower(hash.GetHashAndReset())), canonical,
            AgentToolProposalEffect.Mutation, AgentToolProposalRecovery.OwnerReceipt, source);
    }

    internal static (ProjectDefinitionToolSource Source, ProjectLifecycleToolArguments Input) Read(AgentToolPreparedPayload payload) {
        if (!Supports(payload.ToolName) || payload.SemanticVersion != SemanticVersion ||
                payload.Effect != AgentToolProposalEffect.Mutation || payload.Recovery != AgentToolProposalRecovery.OwnerReceipt ||
                payload.SourcePreparation is not { } source) {
            throw Denied("The saved hierarchy proposal lacks its supported original source.");
        }
        RequireBytes(payload.ArgumentsJson);
        using var arguments = JsonDocument.Parse(payload.ArgumentsJson);
        if (Prepare(payload.ToolName, arguments.RootElement, source) != payload) {
            throw Denied("The saved hierarchy proposal differs from its original canonical interpretation.");
        }
        return (ProjectDefinitionToolProposalCodec.ReadSource(source),
            arguments.RootElement.Deserialize<ProjectLifecycleToolArguments>(ProjectDefinitionToolProposalCodec.Json)!);
    }

    internal static ProjectLifecycleSubmission Submission(ProjectDefinitionToolSource source, AgentToolBusinessIntentId intent,
        ProjectLifecycleToolArguments input)
        => new(new(source.Session.Profile.ProfileId, source.Namespace, source.Epoch, source.CallerBinding, intent.Value),
            ProjectLifecycleInterpretation.VersionOne, Command(input, source.Session.Profile.ProfileId));

    internal static ProjectLifecycleCommand Command(ProjectLifecycleToolArguments input, Guid profile) {
        if (input.Request is not { ExpectedParentAdmission: { } parent, ExpectedChildAdmission: { } child } request ||
                parent.DatabaseProfileId != profile || child.DatabaseProfileId != profile ||
                parent.ProjectId != input.ParentProjectId || child.ProjectId != request.ChildProjectId ||
                parent.ProjectId == child.ProjectId || request.LeaseToken is { Length: > 512 }) {
            throw Denied("The hierarchy proposal requires both original endpoint lifetimes and its exact target identifiers.");
        }
        if (request.CurrentParentProjectId is null && request.ExpectedCurrentEdge is null) {
            return new ProjectLifecycleCommand.EnsureLink(parent, child);
        }
        if (request.CurrentParentProjectId is not { } previous || request.ExpectedCurrentEdge is not { } edge ||
                edge.Child != child || edge.Parent.DatabaseProfileId != profile || edge.Parent.ProjectId != previous ||
                previous == parent.ProjectId) {
            throw Denied("Reconnection requires the original edge identity and all three original endpoint lifetimes.");
        }
        return new ProjectLifecycleCommand.ReconnectLink(edge, parent);
    }

    private static void Append(IncrementalHash hash, string value) {
        var bytes = Utf8.GetBytes(value);
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    private static void RequireBytes(string value) {
        if (value.Length > MaximumArgumentUtf8Bytes || Utf8.GetByteCount(value) > MaximumArgumentUtf8Bytes) {
            throw Denied("The hierarchy proposal exceeds its supported byte bound.");
        }
    }

    private static AgentToolAdmissionException Denied(string message) => ProjectDefinitionToolProposalCodec.Denied(message);
}
