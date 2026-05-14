using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Plugins.Abstractions;

public enum PluginManifestValidationIssueCode
{
    DuplicatePluginId,
    DuplicatePackageId,
    DuplicateWorkflowExecutorId,
    DuplicateRendererKey,
    DuplicateConnectionKey,
    MissingCapability,
    UnsupportedCapability
}

public sealed record PluginManifestValidationIssue(
    PluginManifestValidationIssueCode Code,
    string Message,
    PluginId? PluginId = null);

public sealed record PluginManifestValidationResult(IReadOnlyList<PluginManifestValidationIssue> Issues)
{
    public bool Succeeded => Issues.Count == 0;

    public void ThrowIfInvalid()
    {
        if (Succeeded)
        {
            return;
        }

        throw new InvalidOperationException(string.Join(" ", Issues.Select(issue => issue.Message)));
    }
}

public static class PluginManifestValidator
{
    private static readonly PluginCapabilityKind KnownCapabilityMask = Enum.GetValues<PluginCapabilityKind>()
        .Aggregate(PluginCapabilityKind.None, (current, value) => current | value);

    public static PluginManifestValidationResult Validate(PluginDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var issues = new List<PluginManifestValidationIssue>();

        AddDuplicateIssues(
            descriptor.WorkflowExecutors.Select(executor => executor.ExecutorId),
            id => id.Value,
            duplicate => new PluginManifestValidationIssue(
                PluginManifestValidationIssueCode.DuplicateWorkflowExecutorId,
                $"Plugin '{descriptor.Id}' declares duplicate workflow executor id '{duplicate}'.",
                descriptor.Id),
            issues);
        AddDuplicateIssues(
            descriptor.Settings.Renderers.Select(renderer => renderer.RendererKey),
            key => key.Value,
            duplicate => new PluginManifestValidationIssue(
                PluginManifestValidationIssueCode.DuplicateRendererKey,
                $"Plugin '{descriptor.Id}' declares duplicate settings renderer key '{duplicate}'.",
                descriptor.Id),
            issues);
        AddDuplicateIssues(
            descriptor.Connections.Select(connection => connection.Key),
            key => key.Value,
            duplicate => new PluginManifestValidationIssue(
                PluginManifestValidationIssueCode.DuplicateConnectionKey,
                $"Plugin '{descriptor.Id}' declares duplicate connection key '{duplicate}'.",
                descriptor.Id),
            issues);

        if ((descriptor.Capabilities & ~KnownCapabilityMask) != PluginCapabilityKind.None)
        {
            issues.Add(new PluginManifestValidationIssue(
                PluginManifestValidationIssueCode.UnsupportedCapability,
                $"Plugin '{descriptor.Id}' declares unsupported capability flags '{descriptor.Capabilities & ~KnownCapabilityMask}'.",
                descriptor.Id));
        }

        RequireCapability(
            descriptor,
            descriptor.WorkflowExecutors.Count > 0,
            PluginCapabilityKind.WorkflowExecutor,
            "workflow executors",
            issues);
        RequireCapability(
            descriptor,
            descriptor.Settings.Renderers.Count > 0,
            PluginCapabilityKind.SettingsRenderer,
            "settings renderers",
            issues);
        RequireCapability(
            descriptor,
            descriptor.OAuth2 is not null,
            PluginCapabilityKind.OAuth2,
            "OAuth2 metadata",
            issues);
        RequireCapability(
            descriptor,
            descriptor.Connections.Any(connection => RequiresSecretCapability(connection.AuthKind)),
            PluginCapabilityKind.SecretReference,
            "secret-backed connections",
            issues);

        return new PluginManifestValidationResult(issues);
    }

    public static PluginManifestValidationResult ValidateCatalog(IReadOnlyList<PluginDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        var issues = descriptors.SelectMany(descriptor => Validate(descriptor).Issues).ToList();

        AddDuplicateIssues(
            descriptors.Select(descriptor => descriptor.Id),
            id => id.Value,
            duplicate => new PluginManifestValidationIssue(
                PluginManifestValidationIssueCode.DuplicatePluginId,
                $"Plugin catalog declares duplicate plugin id '{duplicate}'."),
            issues);
        AddDuplicateIssues(
            descriptors
                .Where(descriptor => descriptor.Package is not null)
                .Select(descriptor => descriptor.Package!.PackageId),
            id => id.Value,
            duplicate => new PluginManifestValidationIssue(
                PluginManifestValidationIssueCode.DuplicatePackageId,
                $"Plugin catalog declares duplicate package id '{duplicate}'."),
            issues);

        return new PluginManifestValidationResult(issues);
    }

    private static void RequireCapability(
        PluginDescriptor descriptor,
        bool hasFeature,
        PluginCapabilityKind requiredCapability,
        string featureName,
        List<PluginManifestValidationIssue> issues)
    {
        if (!hasFeature || descriptor.Capabilities.HasFlag(requiredCapability))
        {
            return;
        }

        issues.Add(new PluginManifestValidationIssue(
            PluginManifestValidationIssueCode.MissingCapability,
            $"Plugin '{descriptor.Id}' declares {featureName} but does not include capability '{requiredCapability}'.",
            descriptor.Id));
    }

    private static bool RequiresSecretCapability(PluginConnectionAuthKind authKind)
        => authKind is
            PluginConnectionAuthKind.ApiKey or
            PluginConnectionAuthKind.Basic or
            PluginConnectionAuthKind.BearerToken or
            PluginConnectionAuthKind.Custom;

    private static void AddDuplicateIssues<T>(
        IEnumerable<T> values,
        Func<T, string> keySelector,
        Func<string, PluginManifestValidationIssue> issueFactory,
        List<PluginManifestValidationIssue> issues)
    {
        var duplicateKeys = values
            .Select(keySelector)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);

        foreach (var duplicateKey in duplicateKeys)
        {
            issues.Add(issueFactory(duplicateKey));
        }
    }
}
