using CanDoItAll.SharedKernel;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed partial class ProjectWorkbenchService {
    internal async Task RequireProcessAssetMediaReadAsync(ProjectProcessAssetSnapshot prepared, CancellationToken cancellationToken) {
        RetainedEvidenceImport.RequireNative(prepared.ImportedHistory);
        await using var database = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await database.Set<ProjectProcessAssetContributionRecord>().AsNoTracking().SingleAsync(
            item => item.IntentId == prepared.Plan.Producer.IntentId.Value, cancellationToken);
        RequireProcessAssetSnapshot(row, prepared);
        await mutationScopes.RequireProcessMediaPreparationAsync(new(prepared.Plan.Execution), prepared.Plan.ProjectAdmission.ProjectId, cancellationToken);
        await RequireProcessAssetTargetAsync(database, prepared.Plan, cancellationToken);
    }

    internal async Task<ProjectProcessAssetSnapshot?> FindProcessAssetAsync(AgentToolBusinessIntentId intentId,
        CancellationToken cancellationToken = default) {
        await using var database = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await ReadProcessAssetAsync(database, intentId, cancellationToken);
    }

    internal async Task<ProjectProcessAssetSnapshot> PrepareProcessAssetAsync(ProjectProcessAssetInvocation invocation,
        CancellationToken cancellationToken) {
        var input = new ProjectProcessAssetProposalCodec().Read(invocation.Admitted.Payload);
        var process = new ProjectProcessMutationAdmission(invocation.Execution);
        if (input.ProjectId != process.ProjectAdmission.ProjectId || invocation.Execution.SourceAuthority?.CanCreateAssets != true) {
            throw AssetConflict("The original Process proposal does not permit an asset in this project.");
        }
        var existing = await FindProcessAssetAsync(invocation.Admitted.IntentId, cancellationToken);
        if (existing is not null) {
            RequireProcessAssetInvocation(existing, invocation);
            return existing;
        }

        await using var database = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutation = await mutationScopes.BeginBindingWriteAsync(database,
            [ProjectStructureSerializableMutationScope.ForProject(input.ProjectId), ProcessAssetScope(invocation.Admitted.IntentId)],
            cancellationToken, [process.ProjectAdmission], process);
        existing = await ReadProcessAssetAsync(database, invocation.Admitted.IntentId, cancellationToken);
        if (existing is not null) {
            RequireProcessAssetInvocation(existing, invocation);
            return existing;
        }

        var parent = ProjectWorkbenchGraphConventions.NormalizeEditableParentNodeKey(input.ProjectId, invocation.ParentNodeKey);
        var template = await CreateProcessAssetTemplateAsync(database, input, parent, cancellationToken);
        var nodes = await LoadCreatePlanningNodesAsync(database, input.ProjectId, parent, template, cancellationToken);
        EnsureCanonicalTaskResourceChildAllowed(parent, template.ObjectType, nodes, false);
        InvariantService.ValidateParentAssignment(input.ProjectId, "pending:process-asset", parent, nodes);
        var plan = new ProjectProcessAssetPlan(ProjectProcessAssetPlan.CurrentSchemaVersion, invocation.Admitted,
            invocation.Execution, parent, await ReadWorkflowTargetBindingAsync(database, input.ProjectId, parent, cancellationToken),
            Guid.NewGuid(), new(Guid.NewGuid()), template, input.Revision is not null);
        plan.Validate();
        var json = JsonSerializer.Serialize(plan, ProjectProcessAssetPersistence.Json);
        var row = new ProjectProcessAssetContributionRecord {
            IntentId = invocation.Admitted.IntentId.Value,
            DatabaseProfileId = plan.ProjectAdmission.DatabaseProfileId,
            ProjectId = input.ProjectId,
            ProjectLifetimeId = plan.ProjectAdmission.LifetimeId,
            SourceExecutionRunId = plan.Execution.Evidence.ExecutionRunId,
            NativeObjectId = plan.NativeObjectId,
            StorageIntentId = plan.StorageIntentId.Value,
            PlanJson = json,
            PlanFingerprint = ProjectProcessAssetPersistence.Hash(json),
            PreparedAtUtc = clock.GetUtcNow()
        };
        database.Add(row);
        await database.SaveChangesAsync(cancellationToken);
        await mutation.CommitAsync(cancellationToken);
        return new(ProjectProcessAssetPersistence.ReadPlan(row), row.PlanFingerprint, null, null, null, null, false);
    }

    internal async Task<ProjectProcessAssetSnapshot> MaterializeProcessAssetAsync(ProjectProcessAssetSnapshot prepared,
        ProjectObjectMediaPayload media, CancellationToken cancellationToken) {
        RetainedEvidenceImport.RequireNative(prepared.ImportedHistory);
        ArgumentNullException.ThrowIfNull(media);
        if (media.Base64Data.Length > ProjectStructureAssetUploadLimits.MaximumBase64Characters) {
            throw new ArgumentException("The prepared asset exceeds the supported upload limit.", nameof(media));
        }
        var plan = prepared.Plan;
        var materializedJson = JsonSerializer.Serialize(plan.Template with { Media = media }, ProjectProcessAssetPersistence.Json);
        var materializedFingerprint = ProjectProcessAssetPersistence.Hash(materializedJson);
        var proposal = new ProjectProcessAssetProposalCodec().Read(plan.Producer.Payload);
        var supplied = proposal.Create?.Media ?? proposal.Revision?.Media;
        if (supplied is not null && supplied != media) {
            throw AssetConflict("The materialized media differs from the exact supplied proposal content.");
        }
        var process = new ProjectProcessMutationAdmission(plan.Execution);
        await using var database = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var mutation = await mutationScopes.BeginBindingWriteAsync(database,
            [ProjectStructureSerializableMutationScope.ForProject(plan.ProjectAdmission.ProjectId), ProcessAssetScope(plan.Producer.IntentId)],
            cancellationToken, [plan.ProjectAdmission], process);
        var row = await database.Set<ProjectProcessAssetContributionRecord>().SingleAsync(
            item => item.IntentId == plan.Producer.IntentId.Value, cancellationToken);
        RequireProcessAssetSnapshot(row, prepared);
        if (ProjectProcessAssetPersistence.ReadMaterialized(row) is not null) {
            if (row.MaterializedFingerprint != materializedFingerprint) {
                throw AssetConflict("The original asset intent already has different materialized content.");
            }
            return (await ReadProcessAssetAsync(database, plan.Producer.IntentId, cancellationToken))!;
        }
        await RequireProcessAssetTargetAsync(database, plan, cancellationToken);
        row.MaterializedRequestJson = materializedJson;
        row.MaterializedFingerprint = materializedFingerprint;
        await database.SaveChangesAsync(cancellationToken);
        await mutation.CommitAsync(cancellationToken);
        return new(plan, row.PlanFingerprint, ProjectProcessAssetPersistence.ReadMaterialized(row), row.MaterializedFingerprint,
            null, null, false);
    }

    internal async Task<ProjectProcessAssetResult> CommitProcessAssetAsync(ProjectProcessAssetSnapshot prepared,
        CancellationToken cancellationToken) {
        RetainedEvidenceImport.RequireNative(prepared.ImportedHistory);
        var plan = prepared.Plan;
        var current = await FindProcessAssetAsync(plan.Producer.IntentId, cancellationToken)
            ?? throw AssetConflict("The durable asset preparation is missing.");
        RetainedEvidenceImport.RequireNative(current.ImportedHistory);
        if (current.PlanFingerprint != prepared.PlanFingerprint) {
            throw AssetConflict("The original asset preparation has changed.");
        }
        plan = current.Plan;
        if (current.Receipt is not null) {
            return ProcessAssetResult(current, true);
        }
        var request = current.MaterializedRequest ?? throw AssetConflict("The asset media has not been durably materialized.");
        var process = new ProjectProcessMutationAdmission(plan.Execution);
        await mutationScopes.RequireProcessMediaPreparationAsync(process, plan.ProjectAdmission.ProjectId, cancellationToken);
        await using (var database = await dbContextFactory.CreateDbContextAsync(cancellationToken)) {
            await RequireProcessAssetTargetAsync(database, plan, cancellationToken);
        }
        SavedMediaDescriptor media;
        Exception? observation;
        try {
            (media, observation) = await assetStorageService.SaveStableAsync(plan.StorageIntentId, plan.ProjectAdmission.ProjectId,
                request.ObjectType, ProjectObjectSubtypePolicy.Normalize(request.ObjectType, request.ObjectSubtype), request.Media!, cancellationToken);
        } catch (StorageStablePlacementPendingException exception) {
            throw new ProjectProcessAssetReconciliationRequiredException(plan.Producer.IntentId, exception);
        }
        var result = await CreateObjectCoreAsync(plan.ProjectAdmission.ProjectId, request with {
            ExpectedProjectAdmission = plan.ProjectAdmission,
            ProcessMutationAdmission = process
        }, false, cancellationToken, processAsset: current, preparedProcessAssetMedia: media);
        var committed = await FindProcessAssetAsync(plan.Producer.IntentId, cancellationToken)
            ?? throw AssetConflict("The committed asset receipt could not be observed.");
        return ProcessAssetResult(committed, result.WasReplay) with {
            StorageObservationException = observation
        };
    }

    private async Task<ProjectObjectCreateRequest> CreateProcessAssetTemplateAsync(WorkbenchDbContext database,
        ProjectProcessAssetProposal input, string parent, CancellationToken cancellationToken) {
        if (input.Create is { } create) {
            var subtype = ProjectStructureRequestedNodeKindParser.NormalizeSubtypeForType(create.ObjectType, create.ObjectSubtype);
            return new(create.ObjectType, create.Title, create.Subtitle, create.Notes, parent, ObjectSubtype: subtype,
                MetadataJson: runtimeMetadataBoundary.ValidateAndCanonicalizeForAgent(create.ObjectType, subtype, create.Notes, null),
                PlacementIntent: ProjectObjectPlacementIntent.AutomaticAroundParent);
        }
        var revision = input.Revision!;
        var assembly = await projectStructureAssemblyService.LoadAsync(database, input.ProjectId, cancellationToken);
        var original = assembly.Nodes.SingleOrDefault(item => item.NodeKey == parent);
        if (original is null || original.ObjectType is not (ProjectObjectType.File or ProjectObjectType.ImageAsset or ProjectObjectType.VideoAsset)) {
            throw AssetConflict("The original revision target is no longer an asset.");
        }
        return new(original.ObjectType, revision.Title, revision.Subtitle, revision.Notes, parent,
            ObjectSubtype: string.IsNullOrWhiteSpace(revision.ObjectSubtype) ? original.ObjectSubtype : revision.ObjectSubtype,
            MetadataJson: original.MetadataJson, DurationSeconds: original.DurationSeconds,
            PlacementIntent: ProjectObjectPlacementIntent.AutomaticAroundParent);
    }

    private async Task RequireProcessAssetTargetAsync(WorkbenchDbContext database, ProjectProcessAssetPlan plan,
        CancellationToken cancellationToken) {
        if (await ReadWorkflowTargetBindingAsync(database, plan.ProjectAdmission.ProjectId, plan.ParentNodeKey, cancellationToken) !=
                plan.TargetBindingFingerprint) {
            throw AssetConflict("The prepared asset parent or binding changed. The original intent cannot be retargeted.");
        }
    }

    private static void RequireProcessAssetInvocation(ProjectProcessAssetSnapshot saved, ProjectProcessAssetInvocation invocation) {
        RetainedEvidenceImport.RequireNative(saved.ImportedHistory);
        var plan = saved.Plan;
        if (plan.Producer != invocation.Admitted || plan.Execution.OwnerFingerprint != invocation.Execution.OwnerFingerprint ||
                plan.Execution.Evidence != invocation.Execution.Evidence || plan.ParentNodeKey != invocation.ParentNodeKey) {
            throw AssetConflict("The original asset intent belongs to a different proposal, Process claim or target.");
        }
    }

    private static void RequireProcessAssetSnapshot(ProjectProcessAssetContributionRecord row, ProjectProcessAssetSnapshot prepared) {
        RetainedEvidenceImport.RequireNative(row.ImportedHistory);
        RetainedEvidenceImport.RequireNative(prepared.ImportedHistory);
        ProjectProcessAssetPersistence.ReadPlan(row);
        if (row.PlanFingerprint != prepared.PlanFingerprint ||
                ProjectProcessAssetPersistence.Hash(JsonSerializer.Serialize(prepared.Plan, ProjectProcessAssetPersistence.Json)) != prepared.PlanFingerprint) {
            throw AssetConflict("The durable asset preparation does not match this invocation.");
        }
    }

    private static async Task<ProjectProcessAssetSnapshot?> ReadProcessAssetAsync(WorkbenchDbContext database,
        AgentToolBusinessIntentId intentId, CancellationToken cancellationToken) {
        var row = await database.Set<ProjectProcessAssetContributionRecord>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.IntentId == intentId.Value, cancellationToken);
        if (row is null) {
            return null;
        }
        var plan = ProjectProcessAssetPersistence.ReadPlan(row);
        var request = ProjectProcessAssetPersistence.ReadMaterialized(row);
        if (row.ReceiptJson.Length == 0) {
            if (row.NodeJson.Length != 0) {
                throw AssetConflict("The saved native asset has no atomic contribution receipt.");
            }
            return new(plan, row.PlanFingerprint, request, request is null ? null : row.MaterializedFingerprint, null, null, false) { ImportedHistory = row.ImportedHistory };
        }
        var node = JsonSerializer.Deserialize<ProjectStructureNode>(row.NodeJson, ProjectProcessAssetPersistence.Json)
            ?? throw AssetConflict("The saved native asset result is missing.");
        var receipt = JsonSerializer.Deserialize<ProjectProcessAssetReceipt>(row.ReceiptJson, ProjectProcessAssetPersistence.Json)
            ?? throw AssetConflict("The saved native asset receipt is missing.");
        if (receipt.IntentId != intentId || receipt.NativeObjectId != plan.NativeObjectId || receipt.NodeId != node.Id ||
                receipt.NodeId != ProcessAssetNodeKey(plan.NativeObjectId) || receipt.StorageIntentId != plan.StorageIntentId ||
                receipt.Fingerprint != ProcessAssetReceiptFingerprint(row) ||
                StorageJson.ParseReference(node.StorageObjectReferenceJson)?.PlacementIntentId != plan.StorageIntentId.Value) {
            throw AssetConflict("The saved native asset receipt has inconsistent target or Storage identity.");
        }
        var exists = await database.Set<ProjectObjectRecord>().AsNoTracking().AnyAsync(item => item.Id == plan.NativeObjectId &&
            item.ProjectId == plan.ProjectAdmission.ProjectId && item.NodeKey == receipt.NodeId, cancellationToken);
        return new(plan, row.PlanFingerprint, request, row.MaterializedFingerprint, node, receipt, !exists) { ImportedHistory = row.ImportedHistory };
    }

    private static ProjectProcessAssetResult ProcessAssetResult(ProjectProcessAssetSnapshot snapshot, bool wasReplay) {
        RetainedEvidenceImport.RequireNative(snapshot.ImportedHistory);
        return new(snapshot.Node ?? throw AssetConflict("The native asset result has not committed."),
            new(snapshot.Receipt ?? throw AssetConflict("The native asset receipt has not committed."), wasReplay, snapshot.TargetDeleted));
    }

    private static async Task StageProcessAssetAsync(WorkbenchDbContext database, ProjectObjectRecord record,
        ProjectProcessAssetSnapshot prepared, CancellationToken cancellationToken) {
        var row = await database.Set<ProjectProcessAssetContributionRecord>().SingleAsync(
            item => item.IntentId == prepared.Plan.Producer.IntentId.Value, cancellationToken);
        RequireProcessAssetSnapshot(row, prepared);
        if (row.ReceiptJson.Length != 0 || row.MaterializedFingerprint != prepared.MaterializedFingerprint) {
            throw AssetConflict("The native asset receipt or media preparation changed before commit.");
        }
        var createdAt = record.CreatedAtUtc.ToUniversalTime();
        createdAt = new(createdAt.Ticks - createdAt.Ticks % TimeSpan.TicksPerMicrosecond, TimeSpan.Zero);
        var node = ProjectWorkbenchNodeMapper.MapStructureNode(record);
        row.NodeJson = JsonSerializer.Serialize(node, ProjectProcessAssetPersistence.Json);
        row.ReceiptJson = JsonSerializer.Serialize(new ProjectProcessAssetReceipt(prepared.Plan.Producer.IntentId, record.Id,
            record.NodeKey, prepared.Plan.StorageIntentId, ProcessAssetReceiptFingerprint(row), createdAt), ProjectProcessAssetPersistence.Json);
    }

    private static string ProcessAssetReceiptFingerprint(ProjectProcessAssetContributionRecord row)
        => ProjectProcessAssetPersistence.Hash($"process-asset-v1\n{row.PlanFingerprint}\n{row.MaterializedFingerprint}");

    private static string ProcessAssetScope(AgentToolBusinessIntentId intentId) => $"workbench:process-asset:{intentId.Value:N}";
    private static string ProcessAssetNodeKey(Guid id) => $"custom:{id:N}";
    private static ProjectStructureAgentException AssetConflict(string message) => new(409, "ProcessAssetIntentConflict", message);
}
