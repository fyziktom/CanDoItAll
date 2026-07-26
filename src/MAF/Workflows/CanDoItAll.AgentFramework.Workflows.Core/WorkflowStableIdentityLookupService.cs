using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowStableIdentityLookupService(
    IWorkflowCatalogService catalogService) : IWorkflowStableIdentityLookupService
{
    public async Task<WorkflowStableIdentityResolution> ResolveByTemplateKeyAsync(
        string templateKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = WorkflowStableIdentityNormalizer.NormalizeKey(
            templateKey,
            nameof(templateKey));

        return await ResolveAsync(
            WorkflowStableIdentityKind.Template,
            string.Empty,
            normalizedKey,
            item => string.Equals(
                item.TemplateKey,
                normalizedKey,
                StringComparison.Ordinal),
            cancellationToken);
    }

    public async Task<WorkflowStableIdentityResolution> ResolveByExternalKeyAsync(
        string externalNamespace,
        string externalKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedNamespace = WorkflowStableIdentityNormalizer.NormalizeNamespace(
            externalNamespace,
            nameof(externalNamespace));
        var normalizedKey = WorkflowStableIdentityNormalizer.NormalizeKey(
            externalKey,
            nameof(externalKey));

        return await ResolveAsync(
            WorkflowStableIdentityKind.External,
            normalizedNamespace,
            normalizedKey,
            item =>
                string.Equals(
                    item.ExternalNamespace,
                    normalizedNamespace,
                    StringComparison.Ordinal) &&
                string.Equals(
                    item.ExternalKey,
                    normalizedKey,
                    StringComparison.Ordinal),
            cancellationToken);
    }

    private async Task<WorkflowStableIdentityResolution> ResolveAsync(
        WorkflowStableIdentityKind identityKind,
        string identityNamespace,
        string key,
        Func<WorkflowCatalogItem, bool> matches,
        CancellationToken cancellationToken)
    {
        var materializations = (await catalogService.ListDefinitionsAsync(cancellationToken))
            .Where(matches)
            .OrderBy(item => item.Id.Value)
            .ToArray();

        if (materializations.Length == 0)
        {
            return CreateResult(
                WorkflowStableIdentityResolutionStatus.NotFound,
                workflowId: null,
                runnableVersionId: null,
                materializations,
                "No workflow materialization matches the stable identity.");
        }

        if (materializations.Length > 1)
        {
            return CreateResult(
                WorkflowStableIdentityResolutionStatus.Ambiguous,
                workflowId: null,
                runnableVersionId: null,
                materializations,
                "Multiple workflow materializations match the stable identity; select none until the catalog is repaired.");
        }

        var latest = materializations[0];
        var runnableVersionId = await ResolveLatestRunnableVersionIdAsync(
            latest,
            cancellationToken);
        return runnableVersionId is null
            ? CreateResult(
                WorkflowStableIdentityResolutionStatus.Stale,
                latest.Id,
                runnableVersionId: null,
                materializations,
                "The matching workflow has no runnable active version.")
            : CreateResult(
                WorkflowStableIdentityResolutionStatus.Resolved,
                latest.Id,
                runnableVersionId,
                materializations,
                "The stable identity resolves to one runnable workflow version.");

        WorkflowStableIdentityResolution CreateResult(
            WorkflowStableIdentityResolutionStatus status,
            WorkflowId? workflowId,
            WorkflowVersionId? runnableVersionId,
            IReadOnlyList<WorkflowCatalogItem> materializations,
            string message)
            => new(
                identityKind,
                identityNamespace,
                key,
                status,
                workflowId,
                runnableVersionId,
                materializations,
                message);
    }

    private async Task<WorkflowVersionId?> ResolveLatestRunnableVersionIdAsync(
        WorkflowCatalogItem latest,
        CancellationToken cancellationToken)
    {
        if (latest.Status == WorkflowLifecycleStatus.Active)
        {
            return latest.VersionId;
        }

        if (latest.Status != WorkflowLifecycleStatus.Draft)
        {
            return null;
        }

        var active = await catalogService.GetLatestDefinitionByStatusAsync(
            latest.Id,
            WorkflowLifecycleStatus.Active,
            cancellationToken);
        return active?.Definition.VersionId;
    }

    internal static WorkflowCatalogItem MapCatalogItem(WorkflowDefinition definition) => new(
        definition.Id,
        definition.VersionId,
        definition.Name,
        definition.Description,
        definition.Status,
        definition.RuntimePolicy.PreferredBackend,
        definition.UpdatedAtUtc)
    {
        TemplateKey = definition.TemplateKey,
        TemplatePackKey = definition.TemplatePackKey,
        TemplatePackVersion = definition.TemplatePackVersion,
        SourceHash = definition.SourceHash,
        ExternalNamespace = definition.ExternalNamespace,
        ExternalKey = definition.ExternalKey
    };
}

internal static class WorkflowStableIdentityNormalizer
{
    internal const int MaximumNamespaceLength = 100;
    internal const int MaximumKeyLength = 200;

    public static string NormalizeNamespace(string value, string parameterName)
        => Normalize(value, parameterName, MaximumNamespaceLength);

    public static string NormalizeKey(string value, string parameterName)
        => Normalize(value, parameterName, MaximumKeyLength);

    private static string Normalize(
        string value,
        string parameterName,
        int maximumLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                normalized.Length,
                $"Stable workflow identity values cannot exceed {maximumLength} characters.");
        }

        if (normalized.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) ||
                  character is '-' or '_' or '.' or ':')))
        {
            throw new ArgumentException(
                "Stable workflow identity values may contain only ASCII letters, digits, '-', '_', '.', and ':'.",
                parameterName);
        }

        return normalized;
    }
}

public sealed record WorkflowStableIdentityValues(
    string TemplateKey,
    string TemplatePackKey,
    string TemplatePackVersion,
    string SourceHash,
    string ExternalNamespace,
    string ExternalKey);

public static class WorkflowStableIdentityPolicy
{
    public static WorkflowStableIdentityValues Resolve(
        WorkflowDefinitionSaveRequest request,
        WorkflowDefinition? current)
    {
        var template = request.TemplateProvenance is null
            ? ResolveCurrentTemplate(current)
            : NormalizeTemplate(request.TemplateProvenance);
        var external = ResolveExternal(request, current);
        return new WorkflowStableIdentityValues(
            template.TemplateKey,
            template.TemplatePackKey,
            template.TemplatePackVersion,
            template.SourceHash,
            external.ExternalNamespace,
            external.ExternalKey);
    }

    public static void EnsureExternalIdentityIsUnique(
        IReadOnlyDictionary<WorkflowId, List<WorkflowDefinition>> definitions,
        WorkflowId workflowId,
        string externalNamespace,
        string externalKey)
    {
        if (externalNamespace.Length == 0)
        {
            return;
        }

        var conflict = definitions
            .Where(item => item.Key != workflowId && item.Value.Count > 0)
            .Select(item => item.Value[^1])
            .FirstOrDefault(definition =>
                string.Equals(
                    definition.ExternalNamespace,
                    externalNamespace,
                    StringComparison.Ordinal) &&
                string.Equals(
                    definition.ExternalKey,
                    externalKey,
                    StringComparison.Ordinal));
        if (conflict is not null)
        {
            throw new InvalidOperationException(
                $"Workflow external identity '{externalNamespace}/{externalKey}' is already bound to workflow '{conflict.Id}'.");
        }
    }

    private static WorkflowTemplateProvenance ResolveCurrentTemplate(
        WorkflowDefinition? current)
        => new(
            current?.TemplateKey ?? string.Empty,
            current?.TemplatePackKey ?? string.Empty,
            current?.TemplatePackVersion ?? string.Empty,
            current?.SourceHash ?? string.Empty);

    private static WorkflowTemplateProvenance NormalizeTemplate(
        WorkflowTemplateProvenance template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var templateKey = WorkflowStableIdentityNormalizer.NormalizeKey(
            template.TemplateKey,
            nameof(template.TemplateKey));
        var packKey = WorkflowStableIdentityNormalizer.NormalizeKey(
            template.TemplatePackKey,
            nameof(template.TemplatePackKey));
        var packVersion = NormalizeRequiredMetadata(
            template.TemplatePackVersion,
            nameof(template.TemplatePackVersion),
            maximumLength: 200);
        var sourceHash = NormalizeSourceHash(template.SourceHash);
        return new WorkflowTemplateProvenance(
            templateKey,
            packKey,
            packVersion,
            sourceHash);
    }

    private static (string ExternalNamespace, string ExternalKey) ResolveExternal(
        WorkflowDefinitionSaveRequest request,
        WorkflowDefinition? current)
    {
        var hasNamespace = !string.IsNullOrWhiteSpace(request.ExternalNamespace);
        var hasKey = !string.IsNullOrWhiteSpace(request.ExternalKey);
        if (hasNamespace != hasKey)
        {
            throw new ArgumentException(
                "External namespace and external key must be supplied together.");
        }

        if (!hasNamespace)
        {
            return (
                current?.ExternalNamespace ?? string.Empty,
                current?.ExternalKey ?? string.Empty);
        }

        return (
            WorkflowStableIdentityNormalizer.NormalizeNamespace(
                request.ExternalNamespace,
                nameof(request.ExternalNamespace)),
            WorkflowStableIdentityNormalizer.NormalizeKey(
                request.ExternalKey,
                nameof(request.ExternalKey)));
    }

    private static string NormalizeRequiredMetadata(
        string value,
        string parameterName,
        int maximumLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                normalized.Length,
                $"Workflow provenance metadata cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }

    private static string NormalizeSourceHash(string sourceHash)
    {
        var normalized = NormalizeRequiredMetadata(
                sourceHash,
                nameof(sourceHash),
                maximumLength: 64)
            .ToLowerInvariant();
        if (normalized.Length != 64 ||
            normalized.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new ArgumentException(
                "Workflow template source hash must be a lowercase or uppercase SHA-256 hexadecimal value.",
                nameof(sourceHash));
        }

        return normalized;
    }
}
