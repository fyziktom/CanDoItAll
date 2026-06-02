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
    UnsupportedCapability,
    MissingConnectionMetadata,
    InconsistentPermissionPolicy
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
            descriptor.Connections.Any(connection => connection.AuthKind == PluginConnectionAuthKind.OAuth2),
            PluginCapabilityKind.OAuth2,
            "OAuth2 connections",
            issues);
        RequireCapability(
            descriptor,
            descriptor.Connections.Any(connection => RequiresSecretCapability(connection.AuthKind)),
            PluginCapabilityKind.SecretReference,
            "secret-backed connections",
            issues);
        ValidateOAuthConnectionMetadata(descriptor, issues);
        ValidateWorkflowExecutorPermissionPolicies(descriptor, issues);

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

    private static void ValidateOAuthConnectionMetadata(
        PluginDescriptor descriptor,
        List<PluginManifestValidationIssue> issues)
    {
        if (descriptor.OAuth2 is null)
        {
            return;
        }

        var hasMatchingConnection = descriptor.Connections.Any(connection =>
            connection.AuthKind == PluginConnectionAuthKind.OAuth2 &&
            connection.Key == descriptor.OAuth2.ConnectionKey);
        if (hasMatchingConnection)
        {
            return;
        }

        issues.Add(new PluginManifestValidationIssue(
            PluginManifestValidationIssueCode.MissingConnectionMetadata,
            $"Plugin '{descriptor.Id}' declares OAuth2 metadata for connection '{descriptor.OAuth2.ConnectionKey}' but no matching OAuth2 connection.",
            descriptor.Id));
    }

    private static void ValidateWorkflowExecutorPermissionPolicies(
        PluginDescriptor descriptor,
        List<PluginManifestValidationIssue> issues)
    {
        foreach (var executor in descriptor.WorkflowExecutors)
        {
            var requiredCapabilities = executor.PermissionPolicy.RequiredCapabilities;

            RequireAnyCapability(
                descriptor,
                requiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.UsesNetwork) ||
                requiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.ReadsExternalData) ||
                requiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.WritesExternalData),
                [PluginCapabilityKind.HttpClient, PluginCapabilityKind.OAuth2],
                $"workflow executor '{executor.ExecutorId}' network or external data access",
                issues);
            RequireAnyCapability(
                descriptor,
                requiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.UsesSecrets),
                [PluginCapabilityKind.SecretReference, PluginCapabilityKind.OAuth2],
                $"workflow executor '{executor.ExecutorId}' secret access",
                issues);
            RequireCapability(
                descriptor,
                requiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.RunsHostCommand),
                PluginCapabilityKind.HostCommand,
                $"workflow executor '{executor.ExecutorId}' host-command access",
                issues);

            if (requiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.UsesSecrets) &&
                !HasSecretOrOAuthConnectionMetadata(descriptor))
            {
                issues.Add(new PluginManifestValidationIssue(
                    PluginManifestValidationIssueCode.MissingConnectionMetadata,
                    $"Plugin '{descriptor.Id}' workflow executor '{executor.ExecutorId}' requires secrets but no secret or OAuth2 connection metadata is declared.",
                    descriptor.Id));
            }

            if (requiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.WritesExternalData) &&
                executor.PermissionPolicy.ApprovalRequirement == WorkflowExecutorApprovalRequirement.NotRequired &&
                !requiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.IdempotentExternalMarker))
            {
                issues.Add(new PluginManifestValidationIssue(
                    PluginManifestValidationIssueCode.InconsistentPermissionPolicy,
                    $"Plugin '{descriptor.Id}' workflow executor '{executor.ExecutorId}' writes external data but does not require approval or declare an idempotent external marker policy.",
                    descriptor.Id));
            }

            if (requiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.WritesExternalData) &&
                !executor.SideEffects.WritesExternalState)
            {
                issues.Add(new PluginManifestValidationIssue(
                    PluginManifestValidationIssueCode.InconsistentPermissionPolicy,
                    $"Plugin '{descriptor.Id}' workflow executor '{executor.ExecutorId}' writes external data but does not declare an external-write side-effect contract.",
                    descriptor.Id));
            }

            if (executor.SideEffects.WritesExternalState &&
                !requiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.WritesExternalData))
            {
                issues.Add(new PluginManifestValidationIssue(
                    PluginManifestValidationIssueCode.InconsistentPermissionPolicy,
                    $"Plugin '{descriptor.Id}' workflow executor '{executor.ExecutorId}' declares external-write side effects but does not require the WritesExternalData capability.",
                    descriptor.Id));
            }

            if (requiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.IdempotentExternalMarker) &&
                (executor.SideEffects.ExternalMutationKind != WorkflowExecutorExternalMutationKind.ProcessedMarker ||
                 !executor.SideEffects.AllowsIdempotentRetry))
            {
                issues.Add(new PluginManifestValidationIssue(
                    PluginManifestValidationIssueCode.InconsistentPermissionPolicy,
                    $"Plugin '{descriptor.Id}' workflow executor '{executor.ExecutorId}' declares an idempotent external marker capability but does not expose a retry-safe processed-marker side-effect contract.",
                    descriptor.Id));
            }

            if (requiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.RunsHostCommand) &&
                executor.PermissionPolicy.ApprovalRequirement != WorkflowExecutorApprovalRequirement.AlwaysRequired)
            {
                issues.Add(new PluginManifestValidationIssue(
                    PluginManifestValidationIssueCode.InconsistentPermissionPolicy,
                    $"Plugin '{descriptor.Id}' workflow executor '{executor.ExecutorId}' runs host commands and must always require approval.",
                    descriptor.Id));
            }

            ValidateDeterministicTestMode(descriptor, executor, requiredCapabilities, issues);
        }
    }

    private static void RequireAnyCapability(
        PluginDescriptor descriptor,
        bool hasFeature,
        IReadOnlyList<PluginCapabilityKind> requiredCapabilities,
        string featureName,
        List<PluginManifestValidationIssue> issues)
    {
        if (!hasFeature || requiredCapabilities.Any(capability => (descriptor.Capabilities & capability) == capability))
        {
            return;
        }

        issues.Add(new PluginManifestValidationIssue(
            PluginManifestValidationIssueCode.MissingCapability,
            $"Plugin '{descriptor.Id}' declares {featureName} but does not include any required capability: {string.Join(", ", requiredCapabilities)}.",
            descriptor.Id));
    }

    private static bool HasSecretOrOAuthConnectionMetadata(PluginDescriptor descriptor)
        => descriptor.OAuth2 is not null ||
           descriptor.Connections.Any(connection =>
               connection.AuthKind == PluginConnectionAuthKind.OAuth2 ||
               RequiresSecretCapability(connection.AuthKind));

    private static void ValidateDeterministicTestMode(
        PluginDescriptor descriptor,
        PluginWorkflowExecutorDescriptor executor,
        WorkflowExecutorCapabilityFlags requiredCapabilities,
        List<PluginManifestValidationIssue> issues)
    {
        var declaresCapability = requiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode);
        if (declaresCapability == executor.DeterministicTestMode.IsSupported)
        {
            return;
        }

        issues.Add(new PluginManifestValidationIssue(
            PluginManifestValidationIssueCode.InconsistentPermissionPolicy,
            $"Plugin '{descriptor.Id}' workflow executor '{executor.ExecutorId}' has inconsistent deterministic test-mode capability and descriptor metadata.",
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
