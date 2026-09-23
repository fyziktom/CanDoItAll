using CanDoItAll.SharedKernel;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessAssetProposalTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Asset_proposal_round_trip_retains_exact_media_parent_and_owner_recovery(bool revision) {
        var codec = new ProjectProcessAssetProposalCodec();
        var project = Guid.NewGuid();
        var media = new ProjectObjectMediaPayload("original.txt", "text/plain", Convert.ToBase64String("original"u8.ToArray()));
        var input = revision
            ? JsonSerializer.SerializeToElement(new ProjectProcessAssetRevisionProposal(project, "custom:original",
                new("Revision", "", "", media)), ProjectProcessAssetProposalCodec.Json)
            : JsonSerializer.SerializeToElement(new ProjectProcessAssetCreateProposal(project,
                new(ProjectObjectType.File, "Asset", Media: media, ParentNodeKey: "custom:original")), ProjectProcessAssetProposalCodec.Json);
        var name = revision ? ProjectProcessAssetProposalCodec.RevisionToolName : ProjectStructureToolPolicy.ProjectStructureAssetCreate;
        var first = codec.Prepare(name, input);
        var saved = JsonSerializer.Deserialize<AgentToolPreparedPayload>(JsonSerializer.Serialize(first, ProjectProcessAssetPersistence.Json),
            ProjectProcessAssetPersistence.Json)!;
        var read = codec.Read(saved);
        Assert.Equal(first, saved);
        Assert.Equal(project, read.ProjectId);
        Assert.Equal("custom:original", read.ParentNodeKey);
        Assert.Equal(media, read.Create?.Media ?? read.Revision?.Media);
        Assert.Equal(AgentToolProposalRecovery.OwnerReceipt, first.Recovery);
        Assert.Equal(AgentToolProposalEffect.Mutation, first.Effect);
    }

    [Theory]
    [InlineData("projectId")]
    [InlineData("parentNodeKey")]
    [InlineData("base64Data")]
    [InlineData("title")]
    public void Changed_semantic_asset_input_does_not_reuse_the_original_approval(string field) {
        var codec = new ProjectProcessAssetProposalCodec();
        var input = new ProjectProcessAssetCreateProposal(Guid.NewGuid(), new(ProjectObjectType.File, "Original",
            Media: new("original.txt", "text/plain", "YQ=="), ParentNodeKey: "custom:original"));
        var changed = field switch {
            "projectId" => input with { ProjectId = Guid.NewGuid() },
            "parentNodeKey" => input with { Request = input.Request with { ParentNodeKey = "custom:other" } },
            "base64Data" => input with { Request = input.Request with { Media = input.Request.Media! with { Base64Data = "Yg==" } } },
            "title" => input with { Request = input.Request with { Title = "Changed" } },
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
        Assert.NotEqual(Prepare(input).Digest, Prepare(changed).Digest);
        AgentToolPreparedPayload Prepare(ProjectProcessAssetCreateProposal value)
            => codec.Prepare(ProjectStructureToolPolicy.ProjectStructureAssetCreate,
                JsonSerializer.SerializeToElement(value, ProjectProcessAssetProposalCodec.Json));
    }

    [Fact]
    public void Receipt_identifiers_and_microsecond_timestamp_survive_JSON_without_default_ids() {
        var receipt = new ProjectProcessAssetReceipt(new(Guid.NewGuid()), Guid.NewGuid(), "custom:fixed",
            new(Guid.NewGuid()), new('a', 64), new DateTimeOffset(2032, 2, 3, 4, 5, 6, TimeSpan.Zero).AddTicks(123450));
        var saved = JsonSerializer.Deserialize<ProjectProcessAssetReceipt>(JsonSerializer.Serialize(receipt, ProjectProcessAssetPersistence.Json),
            ProjectProcessAssetPersistence.Json)!;
        Assert.Equal(receipt, saved);
        Assert.NotEqual(Guid.Empty, saved.IntentId.Value);
        Assert.NotEqual(Guid.Empty, saved.NativeObjectId);
        Assert.NotEqual(Guid.Empty, saved.StorageIntentId.Value);
        Assert.Equal(0, saved.CommittedAtUtc.Ticks % TimeSpan.TicksPerMicrosecond);
    }

    [Fact]
    public void Asset_codec_does_not_classify_metered_image_generation_or_legacy_generic_tools_as_owner_recoverable() {
        var codec = new ProjectProcessAssetProposalCodec();
        foreach (var tool in new[] { "image_generate", "generate_image", "project_structure_node_create", "storage_write_text_file" }) {
            Assert.False(codec.Supports(tool));
        }
        Assert.Throws<ArgumentException>(() => codec.Prepare("image_generate", JsonSerializer.SerializeToElement(new { prompt = "image" })));
    }

    [Fact]
    public void A_saved_generic_recovery_policy_cannot_be_silently_promoted_to_an_asset_owner_receipt() {
        var codec = new ProjectProcessAssetProposalCodec();
        var payload = codec.Prepare(ProjectStructureToolPolicy.ProjectStructureAssetCreate,
            JsonSerializer.SerializeToElement(new ProjectProcessAssetCreateProposal(Guid.NewGuid(), new(ProjectObjectType.File,
                "Asset", Media: new("asset.txt", "text/plain", "YQ=="), ParentNodeKey: "custom:parent")), ProjectProcessAssetProposalCodec.Json));
        var generic = new AgentToolPreparedPayload(payload.ToolName, payload.SemanticVersion, payload.Digest, payload.ArgumentsJson,
            payload.Effect, AgentToolProposalRecovery.ReconcileBeforeRetry);
        Assert.Throws<InvalidOperationException>(() => codec.Read(generic));
        Assert.Equal(AgentToolProposalRecovery.OwnerReceipt, payload.Recovery);
    }

    [Theory]
    [InlineData(StorageStablePlacementState.Uncertain)]
    [InlineData(StorageStablePlacementState.Deleted)]
    public void Pending_Storage_exposes_only_typed_recovery_ids_and_never_claims_no_effect(StorageStablePlacementState state) {
        var intent = new AgentToolBusinessIntentId(Guid.NewGuid());
        var storage = new StoragePlacementIntentId(Guid.NewGuid());
        var original = new StorageStablePlacementPendingException(new(storage, state, null, "provider-sensitive detail"));
        var error = new ProjectProcessAssetReconciliationRequiredException(intent, original);
        Assert.Same(original, error.InnerException);
        Assert.Equal(intent, error.IntentId);
        Assert.Equal(storage, error.StorageIntentId);
        Assert.Equal(state, error.StorageState);
        Assert.Equal(AgentToolEffectState.Unknown, error.EffectState);
        Assert.True(error.IsSafeToExpose);
        Assert.False(error.CanRetryWithCorrectedInput);
        Assert.Contains(storage.Value.ToString("D"), error.SafeMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("provider-sensitive detail", error.SafeMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_owner_model_keeps_receipt_without_project_or_asset_delete_cascades() {
        using var context = new WorkbenchDbContext(new DbContextOptionsBuilder<WorkbenchDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=model_only").Options);
        var entity = context.Model.FindEntityType(typeof(ProjectProcessAssetContributionRecord))!;
        Assert.NotNull(entity);
        Assert.Equal("Workbench_ProcessAssetContributions", entity.GetTableName());
        Assert.Empty(entity.GetForeignKeys());
        Assert.Equal(nameof(ProjectProcessAssetContributionRecord.IntentId), Assert.Single(entity.FindPrimaryKey()!.Properties).Name);
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name)
            .SequenceEqual([nameof(ProjectProcessAssetContributionRecord.NativeObjectId)]));
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name)
            .SequenceEqual([nameof(ProjectProcessAssetContributionRecord.StorageIntentId)]));
        Assert.Null(context.Model.FindEntityType(typeof(StoragePlacementIntentRecord)));
    }
}
