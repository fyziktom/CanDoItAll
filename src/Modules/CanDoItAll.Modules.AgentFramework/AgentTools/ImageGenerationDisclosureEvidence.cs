using System.Collections.Immutable;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.AgentFramework;

internal enum ImageGenerationDisclosureState {
    Complete,
    MissingOriginalLifetime,
    MissingOriginalPath,
    LifetimeChangedDuringOperation,
    SourceLimitExceeded
}

internal sealed record ImageGenerationDisclosurePath(string RelativePath, string FullPath);

internal sealed record ImageGenerationDisclosureEvidence(AgentToolSessionReference Session, Guid DatabaseProfileId, Guid AgentId,
    ImageGenerationDisclosureState State, Guid ProviderProfileId, ImageGenerationDisclosurePath Output,
    ImmutableArray<ProjectWriteAdmission> SourceProjects, ImmutableArray<ImageGenerationDisclosurePath> SourcePaths);

internal static class ImageGenerationDisclosureEvidenceCodec {
    private const string Format = "image-generation-result-authority";
    private const int Version = 1;
    internal const int MaximumSources = 128;

    internal static AgentToolProtocolEnvelope Write(ImageGenerationDisclosureEvidence evidence) {
        Validate(evidence);
        return AgentToolProtocolEnvelope.Create(Format, Version, JsonSerializer.Serialize(evidence));
    }

    internal static ImageGenerationDisclosureEvidence Read(AgentToolProtocolEnvelope envelope) {
        if (envelope.Format != Format || envelope.Version != Version) {
            throw new InvalidDataException("The saved image result has unsupported source authority evidence.");
        }
        var evidence = JsonSerializer.Deserialize<ImageGenerationDisclosureEvidence>(envelope.PayloadJson)
            ?? throw new InvalidDataException("The saved image result has no source authority evidence.");
        Validate(evidence);
        return evidence;
    }

    private static void Validate(ImageGenerationDisclosureEvidence evidence) {
        if (evidence.Session is null || evidence.DatabaseProfileId == Guid.Empty || evidence.AgentId == Guid.Empty ||
                evidence.ProviderProfileId == Guid.Empty || !Enum.IsDefined(evidence.State) ||
                evidence.SourceProjects.IsDefault || evidence.SourcePaths.IsDefault ||
                evidence.SourceProjects.Length + evidence.SourcePaths.Length > MaximumSources ||
                evidence.SourceProjects.Any(project => project is null || project.DatabaseProfileId != evidence.DatabaseProfileId) ||
                evidence.SourceProjects.Select(project => project.ProjectId).Distinct().Count() != evidence.SourceProjects.Length ||
                evidence.SourcePaths.Append(evidence.Output).Any(path => path is null || string.IsNullOrWhiteSpace(path.RelativePath) ||
                    string.IsNullOrWhiteSpace(path.FullPath) || Path.IsPathRooted(path.RelativePath) || !Path.IsPathFullyQualified(path.FullPath))) {
            throw new InvalidDataException("The saved image result source authority is invalid.");
        }
    }
}
