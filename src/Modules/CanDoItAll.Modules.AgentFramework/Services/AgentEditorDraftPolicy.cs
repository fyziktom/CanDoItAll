using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AgentEditorSubmission {
    private readonly byte[] originalState;
    private readonly Guid? originalId;
    private readonly DateTimeOffset? originalVersion;

    internal AgentEditorSubmission(AgentEditorModel original, AgentEditorModel request) {
        originalId = original.Id;
        originalVersion = original.ExpectedUpdatedAtUtc;
        originalState = JsonSerializer.SerializeToUtf8Bytes(original);
        Request = request;
    }

    public AgentEditorModel Request { get; }

    public bool HasLaterEdits(AgentEditorModel draft, IReadOnlyList<string> visibleTags) {
        var current = AgentEditorDraftPolicy.Copy(draft);
        current.Id = originalId;
        current.ExpectedUpdatedAtUtc = originalVersion;
        current.Tags = AgentEditorDraftPolicy.BuildTags(draft.Tags, visibleTags);
        return !originalState.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(current));
    }
}

public static class AgentEditorDraftPolicy {
    public static AgentEditorSubmission Capture(AgentEditorModel draft, IReadOnlyList<string> visibleTags,
        IReadOnlyList<ProviderProfile> providers) {
        var original = Copy(draft);
        original.Tags = BuildTags(draft.Tags, visibleTags);
        var request = Copy(original);
        var runtime = providers.FirstOrDefault(provider => provider.Id == request.ProviderProfileId);
        request.Model = ProviderModelValuePolicy.Normalize(request.Model);
        var image = request.ImageGenerationAccess.PreferredProviderProfileId is { } imageProviderId
            ? providers.FirstOrDefault(provider => provider.Id == imageProviderId)
            : ImageGenerationProviderSelectionPolicy.ResolveDefault(providers, runtime);
        var access = AgentImageGenerationAccessMetadata.Normalize(request.ImageGenerationAccess);
        if (access.CanGenerateImages && !access.PreferredProviderProfileId.HasValue && image is not null) {
            access.PreferredProviderProfileId = image.Id;
        }
        access.DefaultModel = ProviderModelValuePolicy.Normalize(access.DefaultModel);
        request.ImageGenerationAccess = AgentImageGenerationAccessMetadata.Normalize(access);
        request.ProjectStructureAccess = AgentProjectStructureAccessMetadata.Normalize(request.ProjectStructureAccess);
        return new(original, request);
    }

    public static List<string> BuildTags(IEnumerable<string> storedTags, IEnumerable<string> visibleTags)
        => visibleTags.Select(tag => tag.Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag) && !AgentSpecialTags.IsFavorite(tag))
            .Concat(storedTags.Any(AgentSpecialTags.IsFavorite) ? [AgentSpecialTags.Favorite] : [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static AgentEditorModel Copy(AgentEditorModel source)
        => CanDoItAll.AgentFramework.Editor.UI.AgentEditorDraftSnapshot.Copy(source);

    public static AgentWorkspaceToolAccessSettings CopyWorkspaceAccess(AgentWorkspaceToolAccessSettings source)
        => CanDoItAll.AgentFramework.Editor.UI.AgentEditorDraftSnapshot.CopyWorkspaceAccess(source);
}
