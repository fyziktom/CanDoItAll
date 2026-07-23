using System.Data;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Npgsql;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class PersistentWorkflowCatalogService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IWorkflowDefinitionValidator validator,
    IPromptGalleryService promptGallery,
    IPromptGalleryImportService promptGalleryImporter,
    IProviderProfileRegistry? providerRegistry = null,
    IProviderProfileService? providerProfileService = null,
    IWorkflowRuntimeBackendCatalog? runtimeBackendCatalog = null,
    PromptGalleryCompatibilityEvaluator? promptCompatibilityEvaluator = null) :
    IWorkflowCatalogService,
    IWorkflowCatalogSearchService,
    IWorkflowCatalogLookupService,
    IWorkflowComponentLibraryService,
    IWorkflowSettingsService
{
    private const string WorkflowPromptSourceCatalog = "agent-framework-workflow-components";

    private const string DefaultSettingsId = "default";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IWorkflowRuntimeBackendCatalog runtimeBackendCatalog = runtimeBackendCatalog ?? new WorkflowRuntimeBackendCatalog();
    private readonly PromptGalleryCompatibilityEvaluator promptCompatibilityEvaluator =
        promptCompatibilityEvaluator ?? new PromptGalleryCompatibilityEvaluator();

    public async Task<IReadOnlyList<WorkflowCatalogItem>> ListDefinitionsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var records = await LatestDefinitionQuery(dbContext)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item => new WorkflowCatalogProjection(
                item.WorkflowId,
                item.VersionId,
                item.Name,
                item.Description,
                item.Status,
                item.PreferredBackend,
                item.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return records
            .Select(MapCatalogItem)
            .ToArray();
    }

    public async Task<WorkflowCatalogSearchPage> SearchDefinitionsAsync(
        WorkflowCatalogSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var filtered = LatestDefinitionQuery(dbContext);
        if (query.Status.HasValue)
        {
            filtered = filtered.Where(item => item.Status == query.Status.Value);
        }

        if (query.Text is not null)
        {
            var normalizedText = query.Text.ToUpperInvariant();
            filtered = filtered.Where(item =>
                item.Name.ToUpper().Contains(normalizedText) ||
                item.Description.ToUpper().Contains(normalizedText));
        }

        var totalCount = await filtered.CountAsync(cancellationToken);
        var records = await filtered
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenBy(item => item.Name.ToUpper())
            .ThenBy(item => item.Name)
            .ThenBy(item => item.WorkflowId)
            .Skip(query.Offset)
            .Take(query.PageSize)
            .Select(item => new WorkflowCatalogProjection(
                item.WorkflowId,
                item.VersionId,
                item.Name,
                item.Description,
                item.Status,
                item.PreferredBackend,
                item.UpdatedAtUtc))
            .ToArrayAsync(cancellationToken);

        return new WorkflowCatalogSearchPage(
            records.Select(MapCatalogItem).ToArray(),
            query.PageIndex,
            query.PageSize,
            totalCount);
    }

    public async Task<IReadOnlyList<WorkflowCatalogItem>> LookupDefinitionsAsync(
        WorkflowCatalogLookupQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.WorkflowIds.Count == 0)
        {
            return [];
        }

        var workflowIds = query.WorkflowIds
            .Select(workflowId => workflowId.Value)
            .ToArray();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var records = await LatestDefinitionQuery(dbContext)
            .Where(item => workflowIds.Contains(item.WorkflowId))
            .Select(item => new WorkflowCatalogProjection(
                item.WorkflowId,
                item.VersionId,
                item.Name,
                item.Description,
                item.Status,
                item.PreferredBackend,
                item.UpdatedAtUtc))
            .ToArrayAsync(cancellationToken);

        return records
            .Select(MapCatalogItem)
            .ToArray();
    }

    public async Task<WorkflowDefinitionDetail?> GetDefinitionAsync(
        WorkflowId workflowId,
        WorkflowVersionId? versionId = null,
        CancellationToken cancellationToken = default)
    {
        var definition = await LoadDefinitionAsync(workflowId, versionId, cancellationToken);
        return definition is null
            ? null
            : new WorkflowDefinitionDetail(
                definition,
                await ValidateDefinitionAsync(definition, cancellationToken));
    }

    public async Task<WorkflowDefinitionDetail?> GetLatestDefinitionByStatusAsync(
        WorkflowId workflowId,
        WorkflowLifecycleStatus status,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var definitions = dbContext.Set<WorkflowDefinitionRecord>().AsNoTracking();
        var record = await (
                from head in dbContext.Set<WorkflowDefinitionHeadRecord>().AsNoTracking()
                join current in definitions
                    on new { head.WorkflowId, head.VersionId }
                    equals new { current.WorkflowId, current.VersionId }
                join candidate in definitions
                    on head.WorkflowId equals candidate.WorkflowId
                where head.WorkflowId == workflowId.Value &&
                      candidate.Status == status &&
                      (status != WorkflowLifecycleStatus.Active ||
                       current.Status == WorkflowLifecycleStatus.Draft ||
                       current.Status == WorkflowLifecycleStatus.Active)
                select candidate)
            .OrderByDescending(item => item.Revision)
            .FirstOrDefaultAsync(cancellationToken);
        if (record is null)
        {
            return null;
        }

        var definition = Deserialize<WorkflowDefinition>(record.DefinitionJson);
        return new WorkflowDefinitionDetail(
            definition,
            await ValidateDefinitionAsync(definition, cancellationToken));
    }

    public async Task<WorkflowDefinition> SaveDefinitionAsync(
        WorkflowDefinitionSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        ArgumentNullException.ThrowIfNull(request.Graph);
        ArgumentNullException.ThrowIfNull(request.RuntimePolicy);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var workflowId = request.Id ?? WorkflowId.New();
        var definitions = dbContext.Set<WorkflowDefinitionRecord>();
        var heads = dbContext.Set<WorkflowDefinitionHeadRecord>();
        var head = await heads.SingleOrDefaultAsync(
            item => item.WorkflowId == workflowId.Value,
            cancellationToken);
        var current = head is null
            ? null
            : await definitions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.WorkflowId == workflowId.Value && item.VersionId == head.VersionId,
                    cancellationToken);
        if (head is not null && current is null)
        {
            throw new InvalidOperationException(
                $"Workflow definition '{workflowId}' has a head that references missing version '{head.VersionId:D}'.");
        }

        if (head is null && await definitions
            .AsNoTracking()
            .AnyAsync(item => item.WorkflowId == workflowId.Value, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Workflow definition '{workflowId}' has persisted versions but no current head.");
        }

        if (request.ExpectedVersionId is { } expectedVersionId &&
            head?.VersionId != expectedVersionId.Value)
        {
            throw CreateDefinitionConcurrencyException(workflowId);
        }

        var now = DateTimeOffset.UtcNow;
        var graphSnapshot = await SnapshotGraphAsync(dbContext, request.Graph, cancellationToken);
        var definition = new WorkflowDefinition(
            workflowId,
            WorkflowVersionId.New(),
            request.Name.Trim(),
            request.Description.Trim(),
            request.Status,
            graphSnapshot,
            request.RuntimePolicy,
            current?.CreatedAtUtc ?? now,
            now)
        {
            InputParameters = SnapshotInputParameters(request.InputParameters)
        };

        ThrowIfValidationFailed(
            await ValidateDefinitionAsync(definition, cancellationToken),
            "Workflow definition save failed validation");

        definitions.Add(WorkflowDefinitionRecord.FromDefinition(
            definition,
            revision: (current?.Revision ?? 0) + 1));
        if (head is null)
        {
            heads.Add(new WorkflowDefinitionHeadRecord
            {
                WorkflowId = workflowId.Value,
                VersionId = definition.VersionId.Value
            });
        }
        else
        {
            head.VersionId = definition.VersionId.Value;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw CreateDefinitionConcurrencyException(workflowId, exception);
        }
        catch (DbUpdateException exception) when (IsWorkflowDefinitionWriteCollision(exception))
        {
            throw CreateDefinitionConcurrencyException(workflowId, exception);
        }

        return definition;
    }

    public async Task<WorkflowDefinition> ChangeDefinitionStatusAsync(
        WorkflowDefinitionStatusChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var detail = await GetDefinitionAsync(request.WorkflowId, versionId: null, cancellationToken)
            ?? throw new KeyNotFoundException($"Workflow definition '{request.WorkflowId}' was not found.");
        if (request.Status == WorkflowLifecycleStatus.Active)
        {
            ThrowIfValidationFailed(await ValidateDefinitionAsync(detail.Definition, cancellationToken), "Workflow definition cannot be published");
        }

        return await SaveDefinitionAsync(
            new WorkflowDefinitionSaveRequest(
                detail.Definition.Id,
                request.ExpectedVersionId ?? detail.Definition.VersionId,
                detail.Definition.Name,
                detail.Definition.Description,
                request.Status,
                detail.Definition.Graph,
                detail.Definition.RuntimePolicy)
            {
                InputParameters = detail.Definition.InputParameters
            },
            cancellationToken);
    }

    public async Task<WorkflowDefinitionExportEnvelope?> ExportDefinitionAsync(
        WorkflowId workflowId,
        WorkflowVersionId? versionId = null,
        CancellationToken cancellationToken = default)
    {
        var detail = await GetDefinitionAsync(workflowId, versionId, cancellationToken);
        return detail is null
            ? null
            : new WorkflowDefinitionExportEnvelope(
                WorkflowDefinitionExchangeFormats.Current,
                detail.Definition,
                detail.Validation,
                DateTimeOffset.UtcNow);
    }

    public async Task<WorkflowDefinition> ImportDefinitionAsync(
        WorkflowDefinitionImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Envelope);
        ArgumentNullException.ThrowIfNull(request.Envelope.Definition);
        if (!string.Equals(
            request.Envelope.SourceFormat,
            WorkflowDefinitionExchangeFormats.Current,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Workflow definition import format '{request.Envelope.SourceFormat}' is not supported.");
        }

        var source = request.Envelope.Definition;
        var workflowId = request.PreserveWorkflowId ? source.Id : (WorkflowId?)null;
        var importedName = string.IsNullOrWhiteSpace(request.Name) ? source.Name : request.Name.Trim();
        var importedStatus = request.Status ?? WorkflowLifecycleStatus.Draft;

        return await SaveDefinitionAsync(
            new WorkflowDefinitionSaveRequest(
                workflowId,
                null,
                importedName,
                source.Description,
                importedStatus,
                source.Graph,
                source.RuntimePolicy)
            {
                InputParameters = source.InputParameters
            },
            cancellationToken);
    }

    public async Task DeleteDefinitionAsync(
        WorkflowId workflowId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var head = await dbContext.Set<WorkflowDefinitionHeadRecord>()
            .SingleOrDefaultAsync(item => item.WorkflowId == workflowId.Value, cancellationToken);
        var records = await dbContext.Set<WorkflowDefinitionRecord>()
            .Where(item => item.WorkflowId == workflowId.Value)
            .ToListAsync(cancellationToken);
        if (head is null && records.Count == 0)
        {
            return;
        }

        if (head is not null)
        {
            dbContext.Remove(head);
        }

        dbContext.RemoveRange(records);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<WorkflowValidationResult> ValidateDefinitionAsync(
        WorkflowDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var componentSnapshot = await ListReferencedComponentsAsync(definition, cancellationToken);
        var result = validator.Validate(definition, componentSnapshot);
        var issues = result.Issues.ToList();
        var effectiveNodeComponents = ResolveEffectiveNodeComponents(definition, componentSnapshot);

        if (definition.RuntimePolicy.RequireDurableProductionRuns &&
            definition.RuntimePolicy.PreferredBackend == WorkflowRuntimeBackendKind.InProcess)
        {
            issues.Add(new WorkflowValidationIssue(
                WorkflowValidationIssueCode.InvalidWorkflowSettings,
                "Durable production workflows cannot prefer the in-process runtime backend."));
        }

        issues.AddRange(WorkflowRuntimePolicyValidator.ValidateRegisteredBackendAvailability(
            definition.RuntimePolicy,
            runtimeBackendCatalog));

        var currentSettings = await GetSettingsAsync(cancellationToken);
        if (!currentSettings.HumanInLoopPolicy.AllowHumanInputNodes &&
            definition.Graph.Nodes.Any(node => node.Kind == WorkflowNodeKind.HumanInput))
        {
            issues.Add(new WorkflowValidationIssue(
                WorkflowValidationIssueCode.InvalidWorkflowSettings,
                "Human-in-loop nodes are disabled by workflow settings."));
        }

        var promptArtifactIds = effectiveNodeComponents
            .Select(component => component.PromptArtifactId)
            .OfType<Guid>()
            .Distinct()
            .ToArray();
        var compatibilitySnapshotsResult = await promptGallery.GetCompatibilitySnapshotsAsync(
            promptArtifactIds,
            cancellationToken);
        var loadedCompatibilitySnapshots = compatibilitySnapshotsResult.Value;
        var compatibilitySnapshotsAvailable = compatibilitySnapshotsResult.IsSuccess &&
            loadedCompatibilitySnapshots is not null;
        IReadOnlyDictionary<Guid, PromptGalleryCompatibilitySnapshot> compatibilitySnapshots =
            loadedCompatibilitySnapshots ?? new Dictionary<Guid, PromptGalleryCompatibilitySnapshot>();
        if (!compatibilitySnapshotsAvailable)
        {
            var detail = compatibilitySnapshotsResult.Errors.Count == 0
                ? "The operation did not return compatibility metadata."
                : string.Join(" ", compatibilitySnapshotsResult.Errors.Select(error => error.Message));
            issues.Add(new WorkflowValidationIssue(
                WorkflowValidationIssueCode.InvalidComponentReference,
                $"Prompt Gallery execution validation failed closed. {detail}"));
        }

        var providerSnapshot = await LoadProviderSnapshotAsync(effectiveNodeComponents, cancellationToken);

        foreach (var component in effectiveNodeComponents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var providerResolution = ResolveProvider(component, providerSnapshot);
            issues.AddRange(ValidateProviderCompatibility(component, providerResolution));
            if (compatibilitySnapshotsAvailable)
            {
                issues.AddRange(ValidatePromptGalleryExecutionCompatibility(
                    component,
                    providerResolution.Provider,
                    compatibilitySnapshots));
            }
        }

        return new WorkflowValidationResult(issues);
    }

    private IReadOnlyList<WorkflowValidationIssue> ValidatePromptGalleryExecutionCompatibility(
        LlmCallComponent component,
        ProviderProfile? provider,
        IReadOnlyDictionary<Guid, PromptGalleryCompatibilitySnapshot> compatibilitySnapshots)
    {
        if (component.PromptArtifactId is not { } promptArtifactId)
        {
            return
            [
                new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.InvalidComponentReference,
                    $"LLM Call Component '{component.Id}' has no canonical Prompt Gallery binding.")
            ];
        }

        if (!compatibilitySnapshots.TryGetValue(promptArtifactId, out var snapshot))
        {
            return
            [
                new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.InvalidComponentReference,
                    $"LLM Call Component '{component.Id}' references unavailable Prompt Gallery item '{promptArtifactId:D}'.")
            ];
        }

        var compatibility = promptCompatibilityEvaluator.Evaluate(
            snapshot,
            new PromptGalleryConsumerContext(
                PromptGalleryConsumer.Workflow,
                PromptGalleryCompatibilityPurpose.Execution,
                Provider: provider?.Kind.ToString(),
                Model: component.Model,
                RequiresFinalVersion: true));
        return compatibility.Issues
            .Where(issue => issue.Severity == PromptCompatibilitySeverity.Error)
            .Select(issue => new WorkflowValidationIssue(
                WorkflowValidationIssueCode.InvalidComponentReference,
                $"LLM Call Component '{component.Id}' cannot execute with Prompt Gallery item '{promptArtifactId:D}': {issue.Message}"))
            .ToArray();
    }

    public async Task<IReadOnlyList<WorkflowProviderOption>> ListProviderOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        if (providerRegistry is null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return [];
        }

        var providers = await providerRegistry.ListProvidersAsync(cancellationToken);
        return providers
            .Select(NormalizeProvider)
            .Where(provider => provider.Purpose == ProviderProfilePurpose.Chat)
            .OrderByDescending(provider => provider.IsEnabled)
            .ThenBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
            .Select(CreateProviderOption)
            .ToArray();
    }

    public async Task<IReadOnlyList<LlmCallComponent>> ListComponentsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var records = await dbContext.Set<WorkflowComponentRecord>()
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        return (await HydrateComponentsAsync(records, cancellationToken))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<LlmCallComponent?> GetComponentAsync(
        WorkflowComponentId componentId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<WorkflowComponentRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == componentId.Value, cancellationToken);

        return record is null
            ? null
            : (await HydrateComponentsAsync([record], cancellationToken)).Single();
    }

    public async Task<LlmCallComponent> SaveComponentAsync(
        LlmCallComponentSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Model);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Instructions);
        ArgumentNullException.ThrowIfNull(request.ModelSettings);
        ArgumentNullException.ThrowIfNull(request.InputShape);
        ArgumentNullException.ThrowIfNull(request.ResultShape);

        var now = DateTimeOffset.UtcNow;
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var current = request.Id.HasValue
            ? await dbContext.Set<WorkflowComponentRecord>()
                .SingleOrDefaultAsync(item => item.Id == request.Id.Value.Value, cancellationToken)
            : null;
        var currentComponent = current is null
            ? null
            : (await HydrateComponentsAsync([current], cancellationToken)).Single();
        var componentCandidate = new LlmCallComponent(
            request.Id ?? WorkflowComponentId.New(),
            request.Name.Trim(),
            request.ProviderProfileId,
            request.Model.Trim(),
            request.Modality,
            request.ModelSettings,
            request.Instructions.Trim(),
            request.InputShape,
            request.ResultShape,
            request.Permissions,
            current?.CreatedAtUtc ?? now,
            now);

        var providerResolution = await ResolveProviderAsync(componentCandidate, cancellationToken);
        var validation = validator.Validate(CreateComponentValidationDefinition(componentCandidate), [componentCandidate]);
        var providerIssues = ValidateProviderCompatibility(componentCandidate, providerResolution);
        var issues = validation.Issues.Concat(providerIssues).ToArray();
        if (issues.Length > 0)
        {
            throw new InvalidOperationException(string.Join(" ", issues.Select(issue => issue.Message)));
        }

        var existingPromptArtifactId = request.PromptArtifactId ?? currentComponent?.PromptArtifactId;
        PromptGalleryItemDetails? promptItem = null;
        if (existingPromptArtifactId.HasValue)
        {
            promptItem = RequirePromptValue(
                await promptGallery.GetItemAsync(existingPromptArtifactId.Value, cancellationToken),
                "Prompt Gallery item");
            EnsurePromptCompatibility(
                promptItem,
                componentCandidate,
                PromptGalleryCompatibilityPurpose.Selection,
                providerResolution.Provider?.Kind.ToString());
        }

        var promptSnapshot = await ResolvePromptSnapshotAsync(
            request,
            currentComponent,
            componentCandidate.Id,
            promptItem,
            providerResolution.Provider,
            cancellationToken);
        var executionItem = promptItem?.Id == promptSnapshot.PromptArtifactId
            ? promptItem with
            {
                Status = PromptArtifactStatus.Final,
                CurrentVersionNumber = Math.Max(promptItem.CurrentVersionNumber, promptSnapshot.VersionNumber)
            }
            : RequirePromptValue(
                await promptGallery.GetItemAsync(promptSnapshot.PromptArtifactId, cancellationToken),
                "Prompt Gallery item");
        EnsurePromptCompatibility(
            executionItem,
            componentCandidate,
            PromptGalleryCompatibilityPurpose.Execution,
            providerResolution.Provider?.Kind.ToString());
        var component = componentCandidate with
        {
            Instructions = promptSnapshot.Content,
            PromptArtifactId = promptSnapshot.PromptArtifactId,
            PromptVersionId = promptSnapshot.PromptVersionId
        };

        if (current is null)
        {
            dbContext.Set<WorkflowComponentRecord>().Add(WorkflowComponentRecord.FromComponent(component));
        }
        else
        {
            current.Apply(component);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return component;
    }

    private void EnsurePromptCompatibility(
        PromptGalleryItemDetails item,
        LlmCallComponent component,
        PromptGalleryCompatibilityPurpose purpose,
        string? providerKind)
    {
        var suppressed = item.WarningSuppressions
            .Where(preference => preference.Consumer == PromptGalleryConsumer.Workflow)
            .Select(preference => preference.IssueCode)
            .ToHashSet();
        var compatibility = promptCompatibilityEvaluator.Evaluate(
            item,
            new PromptGalleryConsumerContext(
                PromptGalleryConsumer.Workflow,
                purpose,
                Provider: providerKind,
                Model: component.Model,
                RequiresFinalVersion: purpose == PromptGalleryCompatibilityPurpose.Execution),
            suppressed);
        var blockingIssues = purpose == PromptGalleryCompatibilityPurpose.Execution
            ? compatibility.Issues.Where(issue => issue.Severity == PromptCompatibilitySeverity.Error).ToArray()
            : compatibility.Issues.Where(issue =>
                issue.Severity == PromptCompatibilitySeverity.Error ||
                issue.Code == PromptCompatibilityIssueCode.ProviderModelNotSupported).ToArray();
        if (blockingIssues.Length > 0)
        {
            throw new InvalidOperationException(
                string.Join(" ", blockingIssues.Select(issue => issue.Message)));
        }
    }

    private async Task<PromptVersionSnapshot> ResolvePromptSnapshotAsync(
        LlmCallComponentSaveRequest request,
        LlmCallComponent? currentComponent,
        WorkflowComponentId componentId,
        PromptGalleryItemDetails? loadedItem,
        ProviderProfile? provider,
        CancellationToken cancellationToken)
    {
        if (request.PromptVersionId is { } requestedVersionId)
        {
            var requestedSnapshot = RequirePromptValue(
                await promptGallery.GetVersionSnapshotAsync(requestedVersionId, cancellationToken),
                "Prompt Gallery version");
            if (request.PromptArtifactId is { } requestedArtifactId &&
                requestedSnapshot.PromptArtifactId != requestedArtifactId)
            {
                throw new InvalidOperationException(
                    $"Prompt Gallery version '{requestedVersionId:D}' does not belong to item '{requestedArtifactId:D}'.");
            }

            return requestedSnapshot;
        }

        var promptArtifactId = request.PromptArtifactId ?? currentComponent?.PromptArtifactId;
        if (promptArtifactId is null)
        {
            var provenance = currentComponent is null
                ? PromptArtifactProvenance.WorkflowCreated
                : PromptArtifactProvenance.WorkflowMigration;
            return await CreateWorkflowPromptAsync(request, componentId, provider, provenance, cancellationToken);
        }

        if (currentComponent?.PromptArtifactId == promptArtifactId &&
            currentComponent.PromptVersionId is { } currentVersionId &&
            string.Equals(currentComponent.Instructions.Trim(), request.Instructions.Trim(), StringComparison.Ordinal))
        {
            return RequirePromptValue(
                await promptGallery.GetVersionSnapshotAsync(currentVersionId, cancellationToken),
                "Prompt Gallery version");
        }

        var item = loadedItem?.Id == promptArtifactId.Value
            ? loadedItem
            : RequirePromptValue(
                await promptGallery.GetItemAsync(promptArtifactId.Value, cancellationToken),
                "Prompt Gallery item");
        var saveResult = await promptGallery.SaveDraftAsync(
            new PromptGalleryDraft(
                item.Id,
                item.ProjectId,
                item.CollectionId,
                item.Title,
                item.Summary,
                item.Kind,
                item.Phase,
                request.Instructions,
                item.Tags,
                item.SupportedModels,
                item.SupportedConsumers,
                new PromptModelRecommendations(
                    request.ModelSettings.Temperature,
                    request.ModelSettings.MaxOutputTokens,
                    item.Recommendations.TopP),
                item.UpdatedAtUtc),
            cancellationToken);
        var saveReceipt = RequirePromptValue(saveResult, "Prompt Gallery item");

        return RequirePromptValue(
            await promptGallery.CreateVersionAsync(
                saveReceipt.PromptArtifactId,
                new PromptVersionCreateRequest(
                    $"Updated by workflow component '{request.Name.Trim()}'",
                    saveReceipt.UpdatedAtUtc),
                cancellationToken),
            "Prompt Gallery version");
    }

    private async Task<PromptVersionSnapshot> CreateWorkflowPromptAsync(
        LlmCallComponentSaveRequest request,
        WorkflowComponentId componentId,
        ProviderProfile? provider,
        PromptArtifactProvenance provenance,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PromptProviderModel> supportedModels = provider is null
            ? []
            : [new PromptProviderModel(provider.Kind.ToString(), request.Model.Trim(), IsPreferred: true)];
        return RequirePromptValue(
            await promptGalleryImporter.ImportVersionAsync(
                new PromptGalleryImportRequest(
                    provenance,
                    $"workflow-component:{componentId.Value:D}",
                    WorkflowPromptSourceCatalog,
                    new PromptGalleryDraft(
                    Id: null,
                    ProjectId: null,
                    CollectionId: null,
                    request.Name,
                    provenance == PromptArtifactProvenance.WorkflowMigration
                        ? $"Reusable instructions migrated from workflow component '{request.Name.Trim()}'."
                        : $"Reusable instructions created for workflow component '{request.Name.Trim()}'.",
                    PromptGalleryItemKind.FullPrompt,
                    Phase: "workflow",
                    request.Instructions,
                    Tags: ["workflow", "llm-call"],
                    SupportedModels: supportedModels,
                    SupportedConsumers: [PromptGalleryConsumer.Workflow],
                    Recommendations: new PromptModelRecommendations(
                        request.ModelSettings.Temperature,
                        request.ModelSettings.MaxOutputTokens)),
                    new PromptImportVersionRequest(
                        provenance == PromptArtifactProvenance.WorkflowMigration
                            ? $"Migrated from workflow component '{request.Name.Trim()}'"
                            : $"Created for workflow component '{request.Name.Trim()}'")),
                cancellationToken),
            "Prompt Gallery workflow import");
    }

    private static T RequirePromptValue<T>(Result<T> result, string resourceName)
    {
        if (result.IsSuccess && result.Value is not null)
        {
            return result.Value;
        }

        var detail = result.Errors.Count == 0
            ? "The operation did not return a value."
            : string.Join(" ", result.Errors.Select(error => $"{error.Code}: {error.Message}"));
        throw new InvalidOperationException($"{resourceName} operation failed. {detail}");
    }

    public async Task DeleteComponentAsync(
        WorkflowComponentId componentId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<WorkflowComponentRecord>()
            .SingleOrDefaultAsync(item => item.Id == componentId.Value, cancellationToken);
        if (record is null)
        {
            return;
        }

        dbContext.Remove(record);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<WorkflowSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<WorkflowSettingsRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == DefaultSettingsId, cancellationToken);

        return record is null
            ? WorkflowSettings.Default
            : Deserialize<WorkflowSettings>(record.SettingsJson);
    }

    public async Task<WorkflowSettings> SaveSettingsAsync(
        WorkflowSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.ArtifactPolicy.MaxInlinePayloadCharacters <= 0)
        {
            throw new InvalidOperationException("Workflow artifact inline payload limit must be positive.");
        }

        if (settings.HumanInLoopPolicy.DefaultRequestTimeoutMinutes <= 0)
        {
            throw new InvalidOperationException("Workflow human-in-loop timeout must be positive.");
        }

        var runtimeIssues = WorkflowRuntimePolicyValidator.ValidateRegisteredBackendAvailability(
            settings.DefaultRuntimePolicy,
            runtimeBackendCatalog);
        if (runtimeIssues.Count > 0)
        {
            throw new InvalidOperationException(
                $"Workflow default runtime policy is invalid: {string.Join(" ", runtimeIssues.Select(issue => issue.Message))}");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var record = await dbContext.Set<WorkflowSettingsRecord>()
            .SingleOrDefaultAsync(item => item.Id == DefaultSettingsId, cancellationToken);
        if (record is null)
        {
            dbContext.Set<WorkflowSettingsRecord>().Add(new WorkflowSettingsRecord
            {
                Id = DefaultSettingsId,
                SettingsJson = Serialize(settings),
                UpdatedAtUtc = now
            });
        }
        else
        {
            record.SettingsJson = Serialize(settings);
            record.UpdatedAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private async Task<WorkflowDefinition?> LoadDefinitionAsync(
        WorkflowId workflowId,
        WorkflowVersionId? versionId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = dbContext.Set<WorkflowDefinitionRecord>()
            .AsNoTracking()
            .Where(item => item.WorkflowId == workflowId.Value);
        var record = versionId.HasValue
            ? await query.SingleOrDefaultAsync(item => item.VersionId == versionId.Value.Value, cancellationToken)
            : await (
                    from head in dbContext.Set<WorkflowDefinitionHeadRecord>().AsNoTracking()
                    join current in query
                        on new { head.WorkflowId, head.VersionId }
                        equals new { current.WorkflowId, current.VersionId }
                    where head.WorkflowId == workflowId.Value
                    select current)
                .SingleOrDefaultAsync(cancellationToken);

        return record is null
            ? null
            : Deserialize<WorkflowDefinition>(record.DefinitionJson);
    }

    private async Task<ProviderResolution> ResolveProviderAsync(
        LlmCallComponent component,
        CancellationToken cancellationToken)
    {
        if (!component.ProviderProfileId.HasValue || providerRegistry is null)
        {
            return new ProviderResolution(ShouldValidate: false, Provider: null);
        }

        var provider = await providerRegistry.GetProviderAsync(component.ProviderProfileId.Value, cancellationToken);
        return new ProviderResolution(
            ShouldValidate: true,
            provider is null ? null : NormalizeProvider(provider));
    }

    private async Task<IReadOnlyDictionary<Guid, ProviderProfile>> LoadProviderSnapshotAsync(
        IReadOnlyCollection<LlmCallComponent> components,
        CancellationToken cancellationToken)
    {
        if (providerRegistry is null)
        {
            return new Dictionary<Guid, ProviderProfile>();
        }

        var providerIds = components
            .Select(component => component.ProviderProfileId)
            .OfType<Guid>()
            .ToHashSet();
        if (providerIds.Count == 0)
        {
            return new Dictionary<Guid, ProviderProfile>();
        }

        var providers = await providerRegistry.ListProvidersAsync(cancellationToken);
        return providers
            .Where(provider => providerIds.Contains(provider.Id))
            .Select(NormalizeProvider)
            .ToDictionary(provider => provider.Id);
    }

    private ProviderResolution ResolveProvider(
        LlmCallComponent component,
        IReadOnlyDictionary<Guid, ProviderProfile> providerSnapshot)
    {
        if (!component.ProviderProfileId.HasValue || providerRegistry is null)
        {
            return new ProviderResolution(ShouldValidate: false, Provider: null);
        }

        return new ProviderResolution(
            ShouldValidate: true,
            providerSnapshot.GetValueOrDefault(component.ProviderProfileId.Value));
    }

    private IReadOnlyList<WorkflowValidationIssue> ValidateProviderCompatibility(
        LlmCallComponent component,
        ProviderResolution resolution)
    {
        if (!resolution.ShouldValidate)
        {
            return [];
        }

        if (resolution.Provider is not { } provider)
        {
            return
            [
                new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.InvalidProviderModel,
                    $"LLM Call Component '{component.Id}' references provider '{component.ProviderProfileId.GetValueOrDefault():D}', which does not exist.")
            ];
        }

        if (!provider.IsEnabled)
        {
            return
            [
                new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.InvalidProviderModel,
                    $"LLM Call Component '{component.Id}' references disabled provider '{provider.Name}'.")
            ];
        }

        if (provider.Purpose != ProviderProfilePurpose.Chat)
        {
            return
            [
                new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.InvalidProviderModel,
                    $"LLM Call Component '{component.Id}' references provider '{provider.Name}', which is not a chat provider.")
            ];
        }

        var featureMatrix = ResolveProviderFeatureMatrix(provider);
        var providerSupportsVision = featureMatrix?.SupportsVision ?? true;
        if (component.Modality is (WorkflowModality.Vision or WorkflowModality.Multimodal) && !providerSupportsVision)
        {
            return
            [
                new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.UnsupportedModality,
                    $"LLM Call Component '{component.Id}' requires vision support but provider '{provider.Name}' does not support vision.")
            ];
        }

        var providerSupportsStructuredOutput = featureMatrix?.SupportsStructuredOutput ?? true;
        if (component.ModelSettings.RequireJsonOutput && !providerSupportsStructuredOutput)
        {
            return
            [
                new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.InvalidProviderModel,
                    $"LLM Call Component '{component.Id}' requires structured JSON output but provider '{provider.Name}' does not support structured output.")
            ];
        }

        var effectiveModel = string.IsNullOrWhiteSpace(component.Model)
            ? provider.DefaultModel
            : component.Model.Trim();
        if (!ProviderPricingDefaults.TryFindPrice(provider.ModelPrices, effectiveModel, out _))
        {
            return
            [
                new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.InvalidProviderModel,
                    $"LLM Call Component '{component.Id}' model '{effectiveModel}' requires a model price row on provider '{provider.Name}'.")
            ];
        }

        return [];
    }

    private sealed record ProviderResolution(bool ShouldValidate, ProviderProfile? Provider);

    private ProviderProfile NormalizeProvider(ProviderProfile provider)
    {
        return providerProfileService?.NormalizeImportedProfile(provider) ?? provider;
    }

    private ProviderFeatureMatrix? ResolveProviderFeatureMatrix(ProviderProfile provider)
    {
        return providerProfileService?.ResolveFeatureMatrix(provider);
    }

    private WorkflowProviderOption CreateProviderOption(ProviderProfile provider)
    {
        var featureMatrix = ResolveProviderFeatureMatrix(provider);
        return new WorkflowProviderOption(
            provider.Id,
            provider.Name,
            provider.Kind,
            provider.Transport,
            provider.Purpose,
            provider.DefaultModel,
            BuildModelOptions(provider),
            provider.IsEnabled,
            provider.SupportsStreaming,
            provider.SupportsTools,
            featureMatrix?.SupportsStructuredOutput ?? true,
            featureMatrix?.SupportsVision ?? true,
            provider.SupportsBackgroundResponses);
    }

    private async Task<IReadOnlyList<LlmCallComponent>> ListReferencedComponentsAsync(
        WorkflowDefinition definition,
        CancellationToken cancellationToken)
    {
        var referencedComponentIds = definition.Graph.Nodes
            .Where(node => node.Kind == WorkflowNodeKind.LlmCall && node.Settings.ComponentId.HasValue)
            .Select(node => node.Settings.ComponentId!.Value.Value)
            .ToHashSet();
        if (referencedComponentIds.Count == 0)
        {
            return [];
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var records = await dbContext.Set<WorkflowComponentRecord>()
            .AsNoTracking()
            .Where(item => referencedComponentIds.Contains(item.Id))
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        return (await HydrateComponentsAsync(records, cancellationToken))
            .Where(component => referencedComponentIds.Contains(component.Id.Value))
            .ToArray();
    }

    private static IReadOnlyList<string> BuildModelOptions(ProviderProfile provider)
    {
        return provider.SuggestedModels
            .Prepend(provider.DefaultModel)
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Select(model => model.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<WorkflowGraph> SnapshotGraphAsync(
        AppDbContext dbContext,
        WorkflowGraph graph,
        CancellationToken cancellationToken)
    {
        var referencedComponentIds = graph.Nodes
            .Where(node => node.Kind == WorkflowNodeKind.LlmCall)
            .Select(node => node.Settings.ComponentId?.Value)
            .OfType<Guid>()
            .Distinct()
            .ToArray();
        var records = referencedComponentIds.Length == 0
            ? []
            : await dbContext.Set<WorkflowComponentRecord>()
                .AsNoTracking()
                .Where(record => referencedComponentIds.Contains(record.Id))
                .ToListAsync(cancellationToken);
        var components = (await HydrateComponentsAsync(records, cancellationToken))
            .ToDictionary(component => component.Id.Value);

        return new WorkflowGraph(
            graph.StartNodeId,
            graph.Nodes
                .Select(node => SnapshotNode(node, components))
                .ToArray(),
            graph.Edges.ToArray());
    }

    private async Task<IReadOnlyList<LlmCallComponent>> HydrateComponentsAsync(
        IReadOnlyList<WorkflowComponentRecord> records,
        CancellationToken cancellationToken)
    {
        if (records.Count == 0)
        {
            return [];
        }

        var currentRecords = records
            .Where(record => record.PromptGalleryBindingSchemaVersion == WorkflowPersistenceSchemaVersions.PromptGalleryBinding)
            .ToArray();
        var unsupported = records.FirstOrDefault(record =>
            record.PromptGalleryBindingSchemaVersion > WorkflowPersistenceSchemaVersions.PromptGalleryBinding);
        if (unsupported is not null)
        {
            throw new InvalidOperationException(
                $"Workflow component '{unsupported.Id}' uses unsupported Prompt Gallery binding schema version '{unsupported.PromptGalleryBindingSchemaVersion}'.");
        }

        var versionIds = currentRecords
            .Select(record => record.PromptVersionId ?? throw new InvalidOperationException(
                $"Workflow component '{record.Id}' has no Prompt Gallery version binding."))
            .Distinct()
            .ToArray();
        var snapshots = versionIds.Length == 0
            ? []
            : RequirePromptValue(
                await promptGallery.GetVersionSnapshotsAsync(versionIds, cancellationToken),
                "Prompt Gallery versions");
        var snapshotsById = snapshots.ToDictionary(snapshot => snapshot.PromptVersionId);

        return records
            .Select(record => record.PromptGalleryBindingSchemaVersion == WorkflowPersistenceSchemaVersions.PromptGalleryBinding
                ? record.Hydrate(snapshotsById.GetValueOrDefault(record.PromptVersionId!.Value)
                    ?? throw new InvalidOperationException(
                        $"Workflow component '{record.Id}' references missing Prompt Gallery version '{record.PromptVersionId}'."))
                : Deserialize<LlmCallComponent>(record.ComponentJson))
            .ToArray();
    }

    private static WorkflowNode SnapshotNode(
        WorkflowNode node,
        IReadOnlyDictionary<Guid, LlmCallComponent> components)
    {
        if (node.Kind != WorkflowNodeKind.LlmCall ||
            node.Settings.ComponentId is not { } componentId ||
            !components.TryGetValue(componentId.Value, out var component))
        {
            return node with { Ports = node.Ports.ToArray() };
        }

        return node with
        {
            Ports = node.Ports.ToArray(),
            Settings = node.Settings with
            {
                ProviderProfileId = node.Settings.ProviderProfileId ?? component.ProviderProfileId,
                Model = string.IsNullOrWhiteSpace(node.Settings.Model)
                    ? component.Model
                    : node.Settings.Model.Trim(),
                Instructions = string.IsNullOrWhiteSpace(node.Settings.Instructions)
                    ? component.Instructions
                    : node.Settings.Instructions
            }
        };
    }

    private static IReadOnlyList<LlmCallComponent> ResolveEffectiveNodeComponents(
        WorkflowDefinition definition,
        IReadOnlyList<LlmCallComponent> components)
    {
        var componentsById = components.ToDictionary(component => component.Id);
        return definition.Graph.Nodes
            .Where(node => node.Kind == WorkflowNodeKind.LlmCall && node.Settings.ComponentId.HasValue)
            .Select(node => (Node: node, Component: componentsById.GetValueOrDefault(node.Settings.ComponentId!.Value)))
            .Where(item => item.Component is not null)
            .Select(item => item.Component! with
            {
                ProviderProfileId = item.Node.Settings.ProviderProfileId ?? item.Component.ProviderProfileId,
                Model = string.IsNullOrWhiteSpace(item.Node.Settings.Model)
                    ? item.Component.Model
                    : item.Node.Settings.Model.Trim()
            })
            .ToArray();
    }

    private static IReadOnlyList<WorkflowInputParameterDescriptor> SnapshotInputParameters(
        IReadOnlyList<WorkflowInputParameterDescriptor> inputParameters)
        => inputParameters
            .Select(parameter => parameter with
            {
                OptionSource = parameter.OptionSource with
                {
                    StaticOptions = parameter.OptionSource.StaticOptions.ToArray()
                }
            })
            .ToArray();

    private static void ThrowIfValidationFailed(
        WorkflowValidationResult validation,
        string messagePrefix)
    {
        if (validation.Succeeded)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{messagePrefix}: {string.Join(" ", validation.Issues.Select(issue => issue.Message))}");
    }

    private static InvalidOperationException CreateDefinitionConcurrencyException(
        WorkflowId workflowId,
        Exception? innerException = null)
    {
        var message = $"Workflow definition '{workflowId}' was updated by another request.";
        return innerException is null
            ? new InvalidOperationException(message)
            : new InvalidOperationException(message, innerException);
    }

    private static bool IsWorkflowDefinitionWriteCollision(DbUpdateException exception)
    {
        if (!DbUpdateExceptionClassifier.IsUniqueConstraintViolation(exception))
        {
            return false;
        }

        return DbUpdateExceptionClassifier.GetConstraintName(exception) is
            WorkflowDefinitionPersistenceConstraints.HeadPrimaryKey or
            WorkflowDefinitionPersistenceConstraints.RevisionUniqueIndex;
    }

    private static WorkflowDefinition CreateComponentValidationDefinition(LlmCallComponent component)
    {
        var start = new WorkflowNodeId("start");
        var llm = new WorkflowNodeId("llm");
        var end = new WorkflowNodeId("end");
        return new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Component validation",
            "Internal component validation workflow.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(
                start,
                [
                    new WorkflowNode(
                        start,
                        WorkflowNodeKind.Start,
                        "Start",
                        [],
                        new WorkflowNodeSettings(
                            ComponentId: null,
                            AgentId: null,
                            SubworkflowId: null,
                            ExternalRequestKind: null,
                            Instructions: string.Empty,
                            InputShape: component.InputShape,
                            ResultShape: component.InputShape)),
                    new WorkflowNode(
                        llm,
                        WorkflowNodeKind.LlmCall,
                        "LLM",
                        [],
                        new WorkflowNodeSettings(
                            component.Id,
                            AgentId: null,
                            SubworkflowId: null,
                            ExternalRequestKind: null,
                            Instructions: component.Instructions,
                            InputShape: component.InputShape,
                            ResultShape: component.ResultShape)),
                    new WorkflowNode(
                        end,
                        WorkflowNodeKind.End,
                        "End",
                        [],
                        new WorkflowNodeSettings(
                            ComponentId: null,
                            AgentId: null,
                            SubworkflowId: null,
                            ExternalRequestKind: null,
                            Instructions: string.Empty,
                            InputShape: component.ResultShape,
                            ResultShape: component.ResultShape))
                ],
                [
                    new WorkflowEdge(
                        new WorkflowEdgeId("start-to-llm"),
                        start,
                        SourcePortId: null,
                        llm,
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty),
                    new WorkflowEdge(
                        new WorkflowEdgeId("llm-to-end"),
                        llm,
                        SourcePortId: null,
                        end,
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty)
                ]),
            WorkflowSettings.Default.DefaultRuntimePolicy,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private static IQueryable<WorkflowDefinitionRecord> LatestDefinitionQuery(AppDbContext dbContext)
        => from head in dbContext.Set<WorkflowDefinitionHeadRecord>().AsNoTracking()
           join record in dbContext.Set<WorkflowDefinitionRecord>().AsNoTracking()
               on new { head.WorkflowId, head.VersionId }
               equals new { record.WorkflowId, record.VersionId }
           select record;

    private static WorkflowCatalogItem MapCatalogItem(WorkflowCatalogProjection item) => new(
        new WorkflowId(item.WorkflowId),
        new WorkflowVersionId(item.VersionId),
        item.Name,
        item.Description,
        item.Status,
        item.PreferredBackend,
        item.UpdatedAtUtc);

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    private static T Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Stored workflow JSON could not be deserialized as '{typeof(T).Name}'.");
    }

    private sealed record WorkflowCatalogProjection(
        Guid WorkflowId,
        Guid VersionId,
        string Name,
        string Description,
        WorkflowLifecycleStatus Status,
        WorkflowRuntimeBackendKind PreferredBackend,
        DateTimeOffset UpdatedAtUtc);
}

public sealed class PersistentWorkflowRunStore(IDbContextFactory<AppDbContext> dbContextFactory) :
    IWorkflowRunStore,
    IWorkflowArtifactStore,
    IWorkflowExternalRequestStore,
    IWorkflowOverviewStore,
    IWorkflowDashboardActivityStore
{
    private const int MaximumOverviewRecentTake = 12;
    private const int MaximumOverviewTopWorkflowTake = 10;

    public async Task CreateRunWithStartedEventAsync(
        WorkflowRunSnapshot run,
        WorkflowEventRecord startedEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(startedEvent);
        if (run.RunId != startedEvent.RunId)
        {
            throw new InvalidOperationException("Workflow run and started event must use the same run id.");
        }

        if (startedEvent.Kind != WorkflowEventKind.Started)
        {
            throw new InvalidOperationException("Initial workflow persistence requires a Started event.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        dbContext.Set<WorkflowRunRecordEntity>().Add(WorkflowRunRecordEntity.FromSnapshot(run));
        dbContext.Set<WorkflowEventRecordEntity>().Add(WorkflowEventRecordEntity.FromEvent(startedEvent));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsWorkflowRunPrimaryKeyViolation(exception))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw new WorkflowRunAlreadyExistsException(run.RunId);
        }
    }

    public async Task<WorkflowRunTransitionResult> TryTransitionRunAsync(
        WorkflowRunId runId,
        IReadOnlyCollection<WorkflowRunState> expectedStates,
        WorkflowRunSnapshot updatedRun,
        WorkflowEventRecord? transitionEvent = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expectedStates);
        ArgumentNullException.ThrowIfNull(updatedRun);
        if (updatedRun.RunId != runId || transitionEvent is not null && transitionEvent.RunId != runId)
        {
            throw new InvalidOperationException("Workflow transition records must use the requested run id.");
        }

        var states = expectedStates.Distinct().ToArray();
        if (states.Length == 0)
        {
            return new WorkflowRunTransitionResult(false, await GetRunAsync(runId, cancellationToken));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var originJson = WorkflowRunRecordEntity.SerializeOrigin(updatedRun.Origin);
        var affected = await dbContext.Set<WorkflowRunRecordEntity>()
            .Where(record => record.RunId == runId.Value && states.Contains(record.State))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(record => record.WorkflowId, updatedRun.WorkflowId.Value)
                .SetProperty(record => record.VersionId, updatedRun.VersionId.Value)
                .SetProperty(record => record.State, updatedRun.State)
                .SetProperty(record => record.Backend, updatedRun.Backend)
                .SetProperty(record => record.BackendRunId, updatedRun.BackendRunId)
                .SetProperty(record => record.Summary, updatedRun.Summary)
                .SetProperty(record => record.CreatedAtUtc, updatedRun.CreatedAtUtc)
                .SetProperty(record => record.UpdatedAtUtc, updatedRun.UpdatedAtUtc)
                .SetProperty(record => record.TerminalAtUtc, updatedRun.TerminalAtUtc)
                .SetProperty(record => record.OriginJson, originJson),
                cancellationToken);
        if (affected == 0)
        {
            var currentRecord = await dbContext.Set<WorkflowRunRecordEntity>()
                .AsNoTracking()
                .SingleOrDefaultAsync(record => record.RunId == runId.Value, cancellationToken);
            await transaction.RollbackAsync(cancellationToken);
            return new WorkflowRunTransitionResult(false, currentRecord?.ToSnapshot());
        }

        if (transitionEvent is not null)
        {
            dbContext.Set<WorkflowEventRecordEntity>().Add(WorkflowEventRecordEntity.FromEvent(transitionEvent));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new WorkflowRunTransitionResult(true, updatedRun);
    }

    public async Task<WorkflowExternalResponseAcceptanceResult> TryAcceptExternalResponseAsync(
        WorkflowExternalRequestId requestId,
        string responseJson,
        DateTimeOffset respondedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responseJson);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var affected = await dbContext.Set<WorkflowExternalRequestRecordEntity>()
            .Where(record => record.Id == requestId.Value && record.RespondedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(record => record.ResponseJson, responseJson)
                .SetProperty(record => record.RespondedAtUtc, respondedAtUtc),
                cancellationToken);
        var record = await dbContext.Set<WorkflowExternalRequestRecordEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == requestId.Value, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (record is null)
        {
            return new WorkflowExternalResponseAcceptanceResult(
                WorkflowExternalResponseAcceptanceOutcome.NotFound,
                Request: null);
        }

        return new WorkflowExternalResponseAcceptanceResult(
            affected == 1
                ? WorkflowExternalResponseAcceptanceOutcome.Accepted
                : WorkflowExternalResponseAcceptanceOutcome.AlreadyResponded,
            record.ToRequest());
    }

    public async Task SaveRunAsync(
        WorkflowRunSnapshot run,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<WorkflowRunRecordEntity>()
            .SingleOrDefaultAsync(item => item.RunId == run.RunId.Value, cancellationToken);
        if (record is null)
        {
            dbContext.Set<WorkflowRunRecordEntity>().Add(WorkflowRunRecordEntity.FromSnapshot(run));
        }
        else
        {
            record.WorkflowId = run.WorkflowId.Value;
            record.VersionId = run.VersionId.Value;
            record.State = run.State;
            record.Backend = run.Backend;
            record.BackendRunId = run.BackendRunId;
            record.Summary = run.Summary;
            record.CreatedAtUtc = run.CreatedAtUtc;
            record.UpdatedAtUtc = run.UpdatedAtUtc;
            record.TerminalAtUtc = run.TerminalAtUtc;
            record.OriginJson = WorkflowRunRecordEntity.SerializeOrigin(run.Origin);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<WorkflowRunSnapshot?> GetRunAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<WorkflowRunRecordEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.RunId == runId.Value, cancellationToken);

        return record?.ToSnapshot();
    }

    public async Task<IReadOnlyList<WorkflowRunSnapshot>> ListRunsAsync(
        WorkflowId? workflowId = null,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = dbContext.Set<WorkflowRunRecordEntity>()
            .AsNoTracking();
        if (workflowId.HasValue)
        {
            query = query.Where(item => item.WorkflowId == workflowId.Value.Value);
        }

        var records = await query
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

        return records
            .Select(item => item.ToSnapshot())
            .ToArray();
    }

    public async Task<WorkflowListPage<WorkflowRunSnapshot>> ListRunPageAsync(
        WorkflowRunPageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var pageIndex = NormalizePageIndex(request.PageIndex);
        var pageSize = NormalizePageSize(request.PageSize);
        var query = dbContext.Set<WorkflowRunRecordEntity>()
            .AsNoTracking();
        if (request.WorkflowId.HasValue)
        {
            query = query.Where(item => item.WorkflowId == request.WorkflowId.Value.Value);
        }

        if (request.VersionId.HasValue)
        {
            query = query.Where(item => item.VersionId == request.VersionId.Value.Value);
        }

        if (request.State.HasValue)
        {
            query = query.Where(item => item.State == request.State.Value);
        }

        if (request.Backend.HasValue)
        {
            query = query.Where(item => item.Backend == request.Backend.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(item => item.Summary.Contains(search) || item.BackendRunId.Contains(search));
        }

        var orderedQuery = query.OrderByDescending(item => item.UpdatedAtUtc).ThenByDescending(item => item.RunId);
        var records = await orderedQuery
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var totalCount = request.IncludeTotalCount
            ? await query.CountAsync(cancellationToken)
            : records.Count;

        return new WorkflowListPage<WorkflowRunSnapshot>(
            records.Select(item => item.ToSnapshot()).ToArray(),
            pageIndex,
            pageSize,
            totalCount);
    }

    public async Task<WorkflowOverviewStoreSnapshot> QueryOverviewAsync(
        WorkflowOverviewStoreQuery request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateOverviewTake(request.RecentTake, MaximumOverviewRecentTake, nameof(request.RecentTake));
        ValidateOverviewTake(
            request.TopWorkflowTake,
            MaximumOverviewTopWorkflowTake,
            nameof(request.TopWorkflowTake));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var runs = dbContext.Set<WorkflowRunRecordEntity>().AsNoTracking();
        var stateBackendRows = await runs
            .GroupBy(run => new { run.State, run.Backend })
            .Select(group => new
            {
                group.Key.State,
                group.Key.Backend,
                Count = group.Count()
            })
            .ToArrayAsync(cancellationToken);
        var topWorkflowRows = await runs
            .GroupBy(run => run.WorkflowId)
            .Select(group => new
            {
                WorkflowId = group.Key,
                RunCount = group.Count(),
                FailedRunCount = group.Count(run => run.State == WorkflowRunState.Failed),
                LastRunAtUtc = group.Max(run => run.UpdatedAtUtc)
            })
            .OrderByDescending(row => row.RunCount)
            .ThenByDescending(row => row.LastRunAtUtc)
            .ThenBy(row => row.WorkflowId)
            .Take(request.TopWorkflowTake)
            .ToArrayAsync(cancellationToken);
        var recentRecords = await runs
            .OrderByDescending(run => run.UpdatedAtUtc)
            .ThenByDescending(run => run.RunId)
            .Take(request.RecentTake)
            .ToArrayAsync(cancellationToken);

        return new WorkflowOverviewStoreSnapshot(
            stateBackendRows
                .GroupBy(row => row.State)
                .ToDictionary(group => group.Key, group => group.Sum(row => row.Count)),
            stateBackendRows
                .GroupBy(row => row.Backend)
                .ToDictionary(group => group.Key, group => group.Sum(row => row.Count)),
            topWorkflowRows
                .Select(row => new WorkflowOverviewStoreWorkflowRow(
                    new WorkflowId(row.WorkflowId),
                    row.RunCount,
                    row.FailedRunCount,
                    row.LastRunAtUtc))
                .ToArray(),
            recentRecords.Select(record => record.ToSnapshot()).ToArray());
    }

    public async Task<WorkflowDashboardActivityStoreResult> QueryActivityAsync(
        WorkflowDashboardActivityQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var runs = dbContext.Set<WorkflowRunRecordEntity>()
            .AsNoTracking();
        var activeRuns = runs.Where(run =>
            run.State == WorkflowRunActivityPolicy.RunningState ||
            run.State == WorkflowRunActivityPolicy.WaitingForInputState);
        var activeCandidates = OrderActivity(activeRuns)
            .Take(query.Take);
        var fallbackCandidates = OrderActivity(runs.Where(_ => !activeRuns.Any()))
            .Take(query.Take);
        var selectableRuns = activeCandidates.Concat(fallbackCandidates);
        var selectedRecords = await OrderActivity(selectableRuns)
            .Select(run => new
            {
                run.RunId,
                run.WorkflowId,
                run.State,
                run.Summary,
                run.UpdatedAtUtc
            })
            .Take(query.Take)
            .ToArrayAsync(cancellationToken);
        var selectedRuns = selectedRecords
            .Select(record => new WorkflowDashboardActivityRun(
                new WorkflowRunId(record.RunId),
                new WorkflowId(record.WorkflowId),
                record.State,
                record.Summary,
                record.UpdatedAtUtc))
            .ToArray();
        var mode = selectedRuns.Any(run => WorkflowRunActivityPolicy.IsActive(run.State))
            ? WorkflowDashboardActivityMode.Active
            : WorkflowDashboardActivityMode.RecentFallback;
        return new WorkflowDashboardActivityStoreResult(
            mode,
            selectedRuns);
    }

    private static IOrderedQueryable<WorkflowRunRecordEntity> OrderActivity(
        IQueryable<WorkflowRunRecordEntity> runs)
        => runs
            .OrderByDescending(run => run.UpdatedAtUtc)
            .ThenByDescending(run => run.RunId);

    public async Task SaveEventAsync(
        WorkflowEventRecord workflowEvent,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<WorkflowEventRecordEntity>()
            .SingleOrDefaultAsync(item => item.Id == workflowEvent.Id, cancellationToken);
        if (record is null)
        {
            dbContext.Set<WorkflowEventRecordEntity>().Add(WorkflowEventRecordEntity.FromEvent(workflowEvent));
        }
        else
        {
            record.RunId = workflowEvent.RunId.Value;
            record.Kind = workflowEvent.Kind;
            record.NodeId = workflowEvent.NodeId?.Value;
            record.Message = workflowEvent.Message;
            record.PayloadJson = workflowEvent.PayloadJson;
            record.CreatedAtUtc = workflowEvent.CreatedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateOverviewTake(int value, int maximum, string parameterName)
    {
        if (value is < 1 || value > maximum)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"Workflow overview take must be between 1 and {maximum}.");
        }
    }

    public async Task<IReadOnlyList<WorkflowEventRecord>> ListEventsAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = dbContext.Set<WorkflowEventRecordEntity>()
            .AsNoTracking()
            .Where(item => item.RunId == runId.Value);
        var records = await query
            .OrderBy(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return records
            .Select(item => item.ToEvent())
            .ToArray();
    }

    public async Task<WorkflowListPage<WorkflowEventRecord>> ListEventPageAsync(
        WorkflowEventPageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var pageIndex = NormalizePageIndex(request.PageIndex);
        var pageSize = NormalizePageSize(request.PageSize);
        var query = dbContext.Set<WorkflowEventRecordEntity>()
            .AsNoTracking()
            .Where(item => item.RunId == request.RunId.Value);
        var totalCount = await query.CountAsync(cancellationToken);
        var orderedQuery = query.OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.Id);
        var records = await orderedQuery
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new WorkflowListPage<WorkflowEventRecord>(
            records.Select(item => item.ToEvent()).ToArray(),
            pageIndex,
            pageSize,
            totalCount);
    }

    public async Task<WorkflowCheckpointRecord> SaveCheckpointAsync(
        WorkflowCheckpointRecord checkpoint,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<WorkflowCheckpointRecordEntity>()
            .SingleOrDefaultAsync(item => item.Id == checkpoint.Id.Value, cancellationToken);
        if (record is null)
        {
            dbContext.Set<WorkflowCheckpointRecordEntity>().Add(WorkflowCheckpointRecordEntity.FromCheckpoint(checkpoint));
        }
        else
        {
            record.RunId = checkpoint.RunId.Value;
            record.WorkflowId = checkpoint.WorkflowId.Value;
            record.VersionId = checkpoint.VersionId.Value;
            record.Backend = checkpoint.Backend;
            record.Kind = checkpoint.Kind;
            record.TrustBoundary = checkpoint.TrustBoundary;
            record.ResumeAvailability = checkpoint.ResumeAvailability;
            record.NodeId = checkpoint.NodeId?.Value;
            record.ExternalRequestId = checkpoint.ExternalRequestId?.Value;
            record.BackendCheckpointId = checkpoint.BackendCheckpointId;
            record.PayloadReference = checkpoint.PayloadReference;
            record.PayloadHash = checkpoint.PayloadHash;
            record.Summary = checkpoint.Summary;
            record.ResumeUnavailableReason = checkpoint.ResumeUnavailableReason;
            record.CreatedAtUtc = checkpoint.CreatedAtUtc;
            record.ResumedAtUtc = checkpoint.ResumedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return checkpoint;
    }

    public async Task<WorkflowCheckpointRecord?> GetCheckpointAsync(
        WorkflowCheckpointId checkpointId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<WorkflowCheckpointRecordEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == checkpointId.Value, cancellationToken);

        return record?.ToCheckpoint();
    }

    public async Task<IReadOnlyList<WorkflowCheckpointRecord>> ListCheckpointsAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = dbContext.Set<WorkflowCheckpointRecordEntity>()
            .AsNoTracking()
            .Where(item => item.RunId == runId.Value);
        var records = await query
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        return records
            .Select(item => item.ToCheckpoint())
            .ToArray();
    }

    public async Task<WorkflowCheckpointRecord> MarkCheckpointResumedAsync(
        WorkflowCheckpointId checkpointId,
        DateTimeOffset resumedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<WorkflowCheckpointRecordEntity>()
            .SingleOrDefaultAsync(item => item.Id == checkpointId.Value, cancellationToken)
            ?? throw new KeyNotFoundException($"Workflow checkpoint '{checkpointId}' was not found.");

        record.ResumedAtUtc = resumedAtUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
        return record.ToCheckpoint();
    }

    public async Task SaveExternalRequestAsync(
        WorkflowExternalRequestRecord request,
        CancellationToken cancellationToken = default)
    {
        await SaveExternalRequestCoreAsync(request, cancellationToken);
    }

    public async Task<WorkflowExternalRequestRecord?> GetExternalRequestAsync(
        WorkflowExternalRequestId requestId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<WorkflowExternalRequestRecordEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == requestId.Value, cancellationToken);

        return record?.ToRequest();
    }

    public async Task<IReadOnlyList<WorkflowExternalRequestRecord>> ListPendingExternalRequestsAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = dbContext.Set<WorkflowExternalRequestRecordEntity>()
            .AsNoTracking()
            .Where(item => item.RunId == runId.Value && item.RespondedAtUtc == null);
        var records = await query
            .OrderBy(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return records
            .Select(item => item.ToRequest())
            .ToArray();
    }

    Task<IReadOnlyList<WorkflowExternalRequestRecord>> IWorkflowExternalRequestStore.ListPendingRequestsAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken)
    {
        return ListPendingExternalRequestsAsync(runId, cancellationToken);
    }

    public Task<WorkflowExternalRequestRecord> SaveRequestAsync(
        WorkflowExternalRequestRecord request,
        CancellationToken cancellationToken = default)
    {
        return SaveExternalRequestCoreAsync(request, cancellationToken);
    }

    public async Task<WorkflowExternalRequestRecord> MarkRespondedAsync(
        WorkflowExternalRequestId requestId,
        string responseJson,
        DateTimeOffset respondedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<WorkflowExternalRequestRecordEntity>()
            .SingleOrDefaultAsync(item => item.Id == requestId.Value, cancellationToken)
            ?? throw new KeyNotFoundException($"Workflow external request '{requestId}' was not found.");

        record.ResponseJson = responseJson;
        record.RespondedAtUtc = respondedAtUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
        return record.ToRequest();
    }

    public async Task SaveArtifactAsync(
        WorkflowArtifactRecord artifact,
        CancellationToken cancellationToken = default)
    {
        await SaveArtifactCoreAsync(artifact, cancellationToken);
    }

    async Task<WorkflowArtifactRecord> IWorkflowArtifactStore.SaveArtifactAsync(
        WorkflowArtifactRecord artifact,
        CancellationToken cancellationToken)
    {
        return await SaveArtifactCoreAsync(artifact, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowArtifactRecord>> ListArtifactsAsync(
        WorkflowRunId runId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = dbContext.Set<WorkflowArtifactRecordEntity>()
            .AsNoTracking()
            .Where(item => item.RunId == runId.Value);
        var records = await query
            .OrderBy(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return records
            .Select(item => item.ToArtifact())
            .ToArray();
    }

    private async Task<WorkflowExternalRequestRecord> SaveExternalRequestCoreAsync(
        WorkflowExternalRequestRecord request,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<WorkflowExternalRequestRecordEntity>()
            .SingleOrDefaultAsync(item => item.Id == request.Id.Value, cancellationToken);
        if (record is null)
        {
            dbContext.Set<WorkflowExternalRequestRecordEntity>().Add(WorkflowExternalRequestRecordEntity.FromRequest(request));
        }
        else
        {
            record.RunId = request.RunId.Value;
            record.Kind = request.Kind;
            record.NodeId = request.NodeId.Value;
            record.EventName = request.EventName;
            record.RequestJson = request.RequestJson;
            record.ResponseJson = request.ResponseJson;
            record.CreatedAtUtc = request.CreatedAtUtc;
            record.RespondedAtUtc = request.RespondedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return request;
    }

    private async Task<WorkflowArtifactRecord> SaveArtifactCoreAsync(
        WorkflowArtifactRecord artifact,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await dbContext.Set<WorkflowArtifactRecordEntity>()
            .SingleOrDefaultAsync(item => item.Id == artifact.Id.Value, cancellationToken);
        if (record is null)
        {
            dbContext.Set<WorkflowArtifactRecordEntity>().Add(WorkflowArtifactRecordEntity.FromArtifact(artifact));
        }
        else
        {
            record.RunId = artifact.RunId.Value;
            record.Kind = artifact.Kind;
            record.NodeId = artifact.NodeId?.Value;
            record.Name = artifact.Name;
            record.ContentType = artifact.ContentType;
            record.StoragePath = artifact.StoragePath;
            record.Summary = artifact.Summary;
            record.CreatedAtUtc = artifact.CreatedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return artifact;
    }

    private static int NormalizePageIndex(int pageIndex)
        => Math.Max(0, pageIndex);

    private static bool IsWorkflowRunPrimaryKeyViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: "PK_AgentFramework_WorkflowRuns"
                })
            {
                return true;
            }
        }

        return false;
    }

    private static int NormalizePageSize(int pageSize)
        => Math.Clamp(pageSize, 1, 100);
}

internal static class WorkflowDefinitionPersistenceConstraints
{
    public const string HeadPrimaryKey = "PK_AgentFramework_WorkflowDefinitionHeads";
    public const string RevisionUniqueIndex = "IX_WorkflowDefinitions_WorkflowId_Revision";
}

public sealed class WorkflowDefinitionHeadRecord
{
    public Guid WorkflowId { get; set; }

    public Guid VersionId { get; set; }
}

public sealed class WorkflowDefinitionRecord
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Guid WorkflowId { get; set; }

    public Guid VersionId { get; set; }

    public long Revision { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public WorkflowLifecycleStatus Status { get; set; }

    public WorkflowRuntimeBackendKind PreferredBackend { get; set; }

    public string DefinitionJson { get; set; } = "{}";

    public int InstructionSnapshotSchemaVersion { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public static WorkflowDefinitionRecord FromDefinition(
        WorkflowDefinition definition,
        long revision) => new()
    {
        WorkflowId = definition.Id.Value,
        VersionId = definition.VersionId.Value,
        Revision = revision,
        Name = definition.Name,
        Description = definition.Description,
        Status = definition.Status,
        PreferredBackend = definition.RuntimePolicy.PreferredBackend,
        DefinitionJson = JsonSerializer.Serialize(definition, JsonOptions),
        InstructionSnapshotSchemaVersion = WorkflowPersistenceSchemaVersions.InstructionSnapshot,
        CreatedAtUtc = definition.CreatedAtUtc,
        UpdatedAtUtc = definition.UpdatedAtUtc
    };
}

public sealed class WorkflowComponentRecord
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public Guid? ProviderProfileId { get; set; }

    public string Model { get; set; } = string.Empty;

    public WorkflowModality Modality { get; set; }

    public Guid? PromptArtifactId { get; set; }

    public Guid? PromptVersionId { get; set; }

    public int PromptGalleryBindingSchemaVersion { get; set; }

    public string ComponentJson { get; set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public static WorkflowComponentRecord FromComponent(LlmCallComponent component)
    {
        var record = new WorkflowComponentRecord();
        record.Apply(component);
        return record;
    }

    public void Apply(LlmCallComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (component.PromptArtifactId is not { } promptArtifactId ||
            component.PromptVersionId is not { } promptVersionId)
        {
            throw new InvalidOperationException(
                $"Workflow component '{component.Id}' must bind an immutable Prompt Gallery version before persistence.");
        }

        Id = component.Id.Value;
        Name = component.Name;
        ProviderProfileId = component.ProviderProfileId;
        Model = component.Model;
        Modality = component.Modality;
        PromptArtifactId = promptArtifactId;
        PromptVersionId = promptVersionId;
        PromptGalleryBindingSchemaVersion = WorkflowPersistenceSchemaVersions.PromptGalleryBinding;
        ComponentJson = JsonSerializer.Serialize(WorkflowComponentBinding.FromComponent(component), JsonOptions);
        CreatedAtUtc = component.CreatedAtUtc;
        UpdatedAtUtc = component.UpdatedAtUtc;
    }

    public LlmCallComponent Hydrate(PromptVersionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (PromptArtifactId != snapshot.PromptArtifactId || PromptVersionId != snapshot.PromptVersionId)
        {
            throw new InvalidOperationException(
                $"Workflow component '{Id}' has an inconsistent Prompt Gallery artifact/version binding.");
        }

        var binding = JsonSerializer.Deserialize<WorkflowComponentBinding>(ComponentJson, JsonOptions)
            ?? throw new InvalidOperationException($"Workflow component '{Id}' binding JSON deserialized to null.");
        return binding.ToComponent(snapshot);
    }

    private sealed record WorkflowComponentBinding(
        WorkflowComponentId Id,
        string Name,
        Guid? ProviderProfileId,
        string Model,
        WorkflowModality Modality,
        WorkflowModelSettings ModelSettings,
        WorkflowValueShape InputShape,
        WorkflowValueShape ResultShape,
        AgentPermissionsPolicy Permissions,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc)
    {
        public static WorkflowComponentBinding FromComponent(LlmCallComponent component)
            => new(
                component.Id,
                component.Name,
                component.ProviderProfileId,
                component.Model,
                component.Modality,
                component.ModelSettings,
                component.InputShape,
                component.ResultShape,
                component.Permissions,
                component.CreatedAtUtc,
                component.UpdatedAtUtc);

        public LlmCallComponent ToComponent(PromptVersionSnapshot snapshot)
            => new(
                Id,
                Name,
                ProviderProfileId,
                Model,
                Modality,
                ModelSettings,
                snapshot.Content,
                InputShape,
                ResultShape,
                Permissions,
                CreatedAtUtc,
                UpdatedAtUtc)
            {
                PromptArtifactId = snapshot.PromptArtifactId,
                PromptVersionId = snapshot.PromptVersionId
            };
    }
}

internal static class WorkflowPersistenceSchemaVersions
{
    public const int PromptGalleryBinding = 1;

    public const int InstructionSnapshot = 2;
}

public sealed class WorkflowSettingsRecord
{
    public string Id { get; set; } = string.Empty;

    public string SettingsJson { get; set; } = "{}";

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class WorkflowRunRecordEntity
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Guid RunId { get; set; }

    public Guid WorkflowId { get; set; }

    public Guid VersionId { get; set; }

    public WorkflowRunState State { get; set; }

    public WorkflowRuntimeBackendKind Backend { get; set; }

    public string BackendRunId { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DateTimeOffset? TerminalAtUtc { get; set; }

    public string OriginJson { get; set; } = string.Empty;

    public static WorkflowRunRecordEntity FromSnapshot(WorkflowRunSnapshot run) => new()
    {
        RunId = run.RunId.Value,
        WorkflowId = run.WorkflowId.Value,
        VersionId = run.VersionId.Value,
        State = run.State,
        Backend = run.Backend,
        BackendRunId = run.BackendRunId,
        Summary = run.Summary,
        CreatedAtUtc = run.CreatedAtUtc,
        UpdatedAtUtc = run.UpdatedAtUtc,
        TerminalAtUtc = run.TerminalAtUtc,
        OriginJson = SerializeOrigin(run.Origin)
    };

    public WorkflowRunSnapshot ToSnapshot() => new(
        new WorkflowRunId(RunId),
        new WorkflowId(WorkflowId),
        new WorkflowVersionId(VersionId),
        State,
        Backend,
        BackendRunId,
        Summary,
        CreatedAtUtc,
        UpdatedAtUtc)
    {
        TerminalAtUtc = this.TerminalAtUtc,
        Origin = string.IsNullOrWhiteSpace(OriginJson)
            ? null
            : JsonSerializer.Deserialize<WorkflowLaunchOrigin>(OriginJson, JsonOptions)
    };

    public static string SerializeOrigin(WorkflowLaunchOrigin? origin)
        => origin is null ? string.Empty : JsonSerializer.Serialize(origin, JsonOptions);
}

public sealed class WorkflowEventRecordEntity
{
    public Guid Id { get; set; }

    public Guid RunId { get; set; }

    public WorkflowEventKind Kind { get; set; }

    public string? NodeId { get; set; }

    public string Message { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public static WorkflowEventRecordEntity FromEvent(WorkflowEventRecord workflowEvent) => new()
    {
        Id = workflowEvent.Id,
        RunId = workflowEvent.RunId.Value,
        Kind = workflowEvent.Kind,
        NodeId = workflowEvent.NodeId?.Value,
        Message = workflowEvent.Message,
        PayloadJson = workflowEvent.PayloadJson,
        CreatedAtUtc = workflowEvent.CreatedAtUtc
    };

    public WorkflowEventRecord ToEvent() => new(
        Id,
        new WorkflowRunId(RunId),
        Kind,
        string.IsNullOrWhiteSpace(NodeId) ? null : new WorkflowNodeId(NodeId),
        Message,
        PayloadJson,
        CreatedAtUtc);
}

public sealed class WorkflowExternalRequestRecordEntity
{
    public Guid Id { get; set; }

    public Guid RunId { get; set; }

    public WorkflowExternalRequestKind Kind { get; set; }

    public string NodeId { get; set; } = string.Empty;

    public string EventName { get; set; } = string.Empty;

    public string RequestJson { get; set; } = string.Empty;

    public string ResponseJson { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? RespondedAtUtc { get; set; }

    public static WorkflowExternalRequestRecordEntity FromRequest(WorkflowExternalRequestRecord request) => new()
    {
        Id = request.Id.Value,
        RunId = request.RunId.Value,
        Kind = request.Kind,
        NodeId = request.NodeId.Value,
        EventName = request.EventName,
        RequestJson = request.RequestJson,
        ResponseJson = request.ResponseJson,
        CreatedAtUtc = request.CreatedAtUtc,
        RespondedAtUtc = request.RespondedAtUtc
    };

    public WorkflowExternalRequestRecord ToRequest() => new(
        new WorkflowExternalRequestId(Id),
        new WorkflowRunId(RunId),
        Kind,
        new WorkflowNodeId(NodeId),
        EventName,
        RequestJson,
        ResponseJson,
        CreatedAtUtc,
        RespondedAtUtc);
}

public sealed class WorkflowCheckpointRecordEntity
{
    public Guid Id { get; set; }

    public Guid RunId { get; set; }

    public Guid WorkflowId { get; set; }

    public Guid VersionId { get; set; }

    public WorkflowRuntimeBackendKind Backend { get; set; }

    public WorkflowCheckpointKind Kind { get; set; }

    public WorkflowCheckpointTrustBoundary TrustBoundary { get; set; }

    public WorkflowResumeAvailability ResumeAvailability { get; set; }

    public string? NodeId { get; set; }

    public Guid? ExternalRequestId { get; set; }

    public string BackendCheckpointId { get; set; } = string.Empty;

    public string PayloadReference { get; set; } = string.Empty;

    public string PayloadHash { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string ResumeUnavailableReason { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? ResumedAtUtc { get; set; }

    public static WorkflowCheckpointRecordEntity FromCheckpoint(WorkflowCheckpointRecord checkpoint) => new()
    {
        Id = checkpoint.Id.Value,
        RunId = checkpoint.RunId.Value,
        WorkflowId = checkpoint.WorkflowId.Value,
        VersionId = checkpoint.VersionId.Value,
        Backend = checkpoint.Backend,
        Kind = checkpoint.Kind,
        TrustBoundary = checkpoint.TrustBoundary,
        ResumeAvailability = checkpoint.ResumeAvailability,
        NodeId = checkpoint.NodeId?.Value,
        ExternalRequestId = checkpoint.ExternalRequestId?.Value,
        BackendCheckpointId = checkpoint.BackendCheckpointId,
        PayloadReference = checkpoint.PayloadReference,
        PayloadHash = checkpoint.PayloadHash,
        Summary = checkpoint.Summary,
        ResumeUnavailableReason = checkpoint.ResumeUnavailableReason,
        CreatedAtUtc = checkpoint.CreatedAtUtc,
        ResumedAtUtc = checkpoint.ResumedAtUtc
    };

    public WorkflowCheckpointRecord ToCheckpoint() => new(
        new WorkflowCheckpointId(Id),
        new WorkflowRunId(RunId),
        new WorkflowId(WorkflowId),
        new WorkflowVersionId(VersionId),
        Backend,
        Kind,
        TrustBoundary,
        ResumeAvailability,
        string.IsNullOrWhiteSpace(NodeId) ? null : new WorkflowNodeId(NodeId),
        ExternalRequestId.HasValue ? new WorkflowExternalRequestId(ExternalRequestId.Value) : null,
        BackendCheckpointId,
        PayloadReference,
        PayloadHash,
        Summary,
        ResumeUnavailableReason,
        CreatedAtUtc,
        ResumedAtUtc);
}

public sealed class WorkflowArtifactRecordEntity
{
    public Guid Id { get; set; }

    public Guid RunId { get; set; }

    public WorkflowArtifactKind Kind { get; set; }

    public string? NodeId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string StoragePath { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public static WorkflowArtifactRecordEntity FromArtifact(WorkflowArtifactRecord artifact) => new()
    {
        Id = artifact.Id.Value,
        RunId = artifact.RunId.Value,
        Kind = artifact.Kind,
        NodeId = artifact.NodeId?.Value,
        Name = artifact.Name,
        ContentType = artifact.ContentType,
        StoragePath = artifact.StoragePath,
        Summary = artifact.Summary,
        CreatedAtUtc = artifact.CreatedAtUtc
    };

    public WorkflowArtifactRecord ToArtifact() => new(
        new WorkflowArtifactId(Id),
        new WorkflowRunId(RunId),
        Kind,
        string.IsNullOrWhiteSpace(NodeId) ? null : new WorkflowNodeId(NodeId),
        Name,
        ContentType,
        StoragePath,
        Summary,
        CreatedAtUtc);
}

internal sealed class WorkflowDefinitionRecordConfiguration : IEntityTypeConfiguration<WorkflowDefinitionRecord>
{
    public void Configure(EntityTypeBuilder<WorkflowDefinitionRecord> builder)
    {
        builder.ToTable("AgentFramework_WorkflowDefinitions");
        builder.HasKey(item => item.VersionId);
        builder.Property(item => item.Name).HasMaxLength(240).IsRequired();
        builder.Property(item => item.Description).HasMaxLength(2000);
        builder.Property(item => item.Status).HasConversion<int>();
        builder.Property(item => item.PreferredBackend).HasConversion<int>();
        builder.Property(item => item.DefinitionJson).HasColumnType("TEXT");
        builder.HasIndex(item => item.WorkflowId);
        builder.HasIndex(item => new { item.WorkflowId, item.Revision })
            .IsUnique()
            .HasDatabaseName(WorkflowDefinitionPersistenceConstraints.RevisionUniqueIndex);
        builder.HasIndex(item => new { item.WorkflowId, item.UpdatedAtUtc });
        builder.HasIndex(item => new { item.InstructionSnapshotSchemaVersion, item.VersionId })
            .HasDatabaseName("IX_WorkflowDefinitions_InstructionSnapshotSchema_Id");
    }
}

internal sealed class WorkflowDefinitionHeadRecordConfiguration : IEntityTypeConfiguration<WorkflowDefinitionHeadRecord>
{
    public void Configure(EntityTypeBuilder<WorkflowDefinitionHeadRecord> builder)
    {
        builder.ToTable("AgentFramework_WorkflowDefinitionHeads");
        builder.HasKey(item => item.WorkflowId)
            .HasName(WorkflowDefinitionPersistenceConstraints.HeadPrimaryKey);
        builder.Property(item => item.VersionId).IsConcurrencyToken();
        builder.HasOne<WorkflowDefinitionRecord>()
            .WithMany()
            .HasForeignKey(item => item.VersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class WorkflowComponentRecordConfiguration : IEntityTypeConfiguration<WorkflowComponentRecord>
{
    public void Configure(EntityTypeBuilder<WorkflowComponentRecord> builder)
    {
        builder.ToTable("AgentFramework_WorkflowComponents");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).HasMaxLength(240).IsRequired();
        builder.Property(item => item.Model).HasMaxLength(240).IsRequired();
        builder.Property(item => item.Modality).HasConversion<int>();
        builder.Property(item => item.ComponentJson).HasColumnType("TEXT");
        builder.HasIndex(item => item.Name);
        builder.HasIndex(item => item.ProviderProfileId);
        builder.HasIndex(item => new { item.PromptGalleryBindingSchemaVersion, item.Id })
            .HasDatabaseName("IX_WorkflowComponents_PromptGalleryBindingSchema_Id");
        builder.HasIndex(item => new { item.PromptArtifactId, item.PromptVersionId })
            .HasDatabaseName("IX_WorkflowComponents_PromptBinding");
        builder.HasOne<PromptArtifact>()
            .WithMany()
            .HasForeignKey(item => item.PromptArtifactId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PromptVersion>()
            .WithMany()
            .HasForeignKey(item => item.PromptVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class WorkflowSettingsRecordConfiguration : IEntityTypeConfiguration<WorkflowSettingsRecord>
{
    public void Configure(EntityTypeBuilder<WorkflowSettingsRecord> builder)
    {
        builder.ToTable("AgentFramework_WorkflowSettings");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasMaxLength(64);
        builder.Property(item => item.SettingsJson).HasColumnType("TEXT");
    }
}

internal sealed class WorkflowRunRecordEntityConfiguration : IEntityTypeConfiguration<WorkflowRunRecordEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowRunRecordEntity> builder)
    {
        builder.ToTable("AgentFramework_WorkflowRuns");
        builder.HasKey(item => item.RunId);
        builder.Property(item => item.State).HasConversion<int>();
        builder.Property(item => item.Backend).HasConversion<int>();
        builder.Property(item => item.BackendRunId).HasMaxLength(300);
        builder.Property(item => item.Summary).HasColumnType("TEXT");
        builder.Property(item => item.OriginJson).HasColumnType("TEXT");
        builder.HasIndex(item => item.WorkflowId);
        builder.HasIndex(item => item.UpdatedAtUtc);
        builder.HasIndex(item => new { item.State, item.UpdatedAtUtc, item.RunId })
            .IsDescending(false, true, true);
    }
}

internal sealed class WorkflowEventRecordEntityConfiguration : IEntityTypeConfiguration<WorkflowEventRecordEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowEventRecordEntity> builder)
    {
        builder.ToTable("AgentFramework_WorkflowEvents");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Kind).HasConversion<int>();
        builder.Property(item => item.NodeId).HasMaxLength(200);
        builder.Property(item => item.Message).HasColumnType("TEXT");
        builder.Property(item => item.PayloadJson).HasColumnType("TEXT");
        builder.HasIndex(item => new { item.RunId, item.CreatedAtUtc });
    }
}

internal sealed class WorkflowExternalRequestRecordEntityConfiguration : IEntityTypeConfiguration<WorkflowExternalRequestRecordEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowExternalRequestRecordEntity> builder)
    {
        builder.ToTable("AgentFramework_WorkflowExternalRequests");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Kind).HasConversion<int>();
        builder.Property(item => item.NodeId).HasMaxLength(200).IsRequired();
        builder.Property(item => item.EventName).HasMaxLength(240).IsRequired();
        builder.Property(item => item.RequestJson).HasColumnType("TEXT");
        builder.Property(item => item.ResponseJson).HasColumnType("TEXT");
        builder.HasIndex(item => new { item.RunId, item.RespondedAtUtc });
    }
}

internal sealed class WorkflowCheckpointRecordEntityConfiguration : IEntityTypeConfiguration<WorkflowCheckpointRecordEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowCheckpointRecordEntity> builder)
    {
        builder.ToTable("AgentFramework_WorkflowCheckpoints");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Backend).HasConversion<int>();
        builder.Property(item => item.Kind).HasConversion<int>();
        builder.Property(item => item.TrustBoundary).HasConversion<int>();
        builder.Property(item => item.ResumeAvailability).HasConversion<int>();
        builder.Property(item => item.NodeId).HasMaxLength(200);
        builder.Property(item => item.BackendCheckpointId).HasMaxLength(300);
        builder.Property(item => item.PayloadReference).HasMaxLength(1200);
        builder.Property(item => item.PayloadHash).HasMaxLength(128);
        builder.Property(item => item.Summary).HasColumnType("TEXT");
        builder.Property(item => item.ResumeUnavailableReason).HasColumnType("TEXT");
        builder.HasIndex(item => new { item.RunId, item.CreatedAtUtc });
        builder.HasIndex(item => new { item.RunId, item.Kind });
        builder.HasIndex(item => item.ExternalRequestId);
    }
}

internal sealed class WorkflowArtifactRecordEntityConfiguration : IEntityTypeConfiguration<WorkflowArtifactRecordEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowArtifactRecordEntity> builder)
    {
        builder.ToTable("AgentFramework_WorkflowArtifacts");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Kind).HasConversion<int>();
        builder.Property(item => item.NodeId).HasMaxLength(200);
        builder.Property(item => item.Name).HasMaxLength(300).IsRequired();
        builder.Property(item => item.ContentType).HasMaxLength(200);
        builder.Property(item => item.StoragePath).HasMaxLength(1200);
        builder.Property(item => item.Summary).HasColumnType("TEXT");
        builder.HasIndex(item => new { item.RunId, item.CreatedAtUtc });
    }
}
