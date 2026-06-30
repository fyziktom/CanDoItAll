using System.Text.RegularExpressions;
using CanDoItAll.Plugins.Abstractions;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Plugins;

public enum PluginWorkflowExecutorActivationFailureKind
{
    MissingPackageMetadata,
    ActivationFailed
}

public enum PluginWorkflowExecutorActivationRetryability
{
    Unknown,
    RetryableAfterRepair
}

public sealed class PluginWorkflowExecutorActivationException : InvalidOperationException
{
    private static readonly Regex AuthorizationBearerPattern = new(
        @"(?<prefix>Authorization\s*:\s*Bearer\s+)[^\s,;}]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex SensitiveAssignmentPattern = new(
        @"(?<prefix>\b(?:token|secret|password|api[-_]?key)\s*[:=]\s*)[""']?[^""'\s,;}]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public PluginWorkflowExecutorActivationException(
        PluginId pluginId,
        PluginPackageId? packageId,
        string executorTypeName,
        string operation,
        string message,
        PluginWorkflowExecutorActivationFailureKind failureKind,
        PluginWorkflowExecutorActivationRetryability retryability,
        string repairHint,
        string technicalDetail = "",
        Exception? innerException = null)
        : base(message, innerException)
    {
        PluginId = pluginId;
        PackageId = packageId;
        ExecutorTypeName = executorTypeName;
        Operation = operation;
        FailureKind = failureKind;
        Retryability = retryability;
        RepairHint = NormalizeRequired(repairHint, nameof(repairHint));
        RedactedTechnicalDetail = RedactTechnicalDetail(
            string.IsNullOrWhiteSpace(technicalDetail)
                ? message
                : technicalDetail);
    }

    public PluginId PluginId { get; }

    public PluginPackageId? PackageId { get; }

    public string ExecutorTypeName { get; }

    public string Operation { get; }

    public PluginWorkflowExecutorActivationFailureKind FailureKind { get; }

    public PluginWorkflowExecutorActivationRetryability Retryability { get; }

    public string RepairHint { get; }

    public string RedactedTechnicalDetail { get; }

    public static PluginWorkflowExecutorActivationException MissingPackageMetadata(
        PluginDescriptor plugin,
        Type executorType)
        => MissingPackageMetadata(
            plugin,
            executorType.FullName ?? executorType.Name,
            "runtime-package-descriptor");

    public static PluginWorkflowExecutorActivationException MissingPackageMetadata(
        PluginDescriptor plugin,
        string executorTypeName,
        string operation)
        => new(
            plugin.Id,
            plugin.Package?.PackageId,
            executorTypeName,
            operation,
            $"Runtime plugin executor '{executorTypeName}' for plugin '{plugin.Id}' is missing package metadata.",
            PluginWorkflowExecutorActivationFailureKind.MissingPackageMetadata,
            PluginWorkflowExecutorActivationRetryability.RetryableAfterRepair,
            "Add package metadata to the plugin manifest before loading runtime package executors.");

    public static PluginWorkflowExecutorActivationException ActivationFailed(
        PluginDescriptor plugin,
        Type executorType,
        Exception innerException)
        => new(
            plugin.Id,
            plugin.Package?.PackageId,
            executorType.FullName ?? executorType.Name,
            "runtime-package-activation",
            $"Runtime plugin executor '{executorType.FullName ?? executorType.Name}' for plugin '{plugin.Id}' could not be activated.",
            PluginWorkflowExecutorActivationFailureKind.ActivationFailed,
            PluginWorkflowExecutorActivationRetryability.RetryableAfterRepair,
            "Register missing constructor dependencies or repair the plugin package before retrying activation.",
            innerException.ToString(),
            innerException);

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} cannot be empty.", parameterName);
        }

        return value.Trim();
    }

    private static string RedactTechnicalDetail(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var bearerMasked = AuthorizationBearerPattern.Replace(value, "${prefix}[REDACTED]");
        return SensitiveAssignmentPattern.Replace(bearerMasked, "${prefix}[REDACTED]");
    }
}
