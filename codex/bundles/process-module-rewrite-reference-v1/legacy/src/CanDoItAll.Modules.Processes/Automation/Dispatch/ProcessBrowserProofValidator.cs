using System.Text.Json;
using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessBrowserProofRecord(
    string SchemaVersion,
    Guid ProcessRunId,
    Guid ProcessStepRunId,
    Guid ExecutionRunId,
    Guid? ProjectId,
    RuntimeHostIdentityRecord RuntimeHost,
    BrowserProofViewport Viewport,
    IReadOnlyList<BrowserProofToolOutput> ToolOutputs,
    IReadOnlyList<string> EvidenceArtifactPaths,
    IReadOnlyList<string> InteractionToolNames,
    RuntimeCleanupReceiptRecord CleanupReceipt,
    DateTimeOffset CapturedAtUtc);

internal sealed record RuntimeHostIdentityRecord(
    string HostUrl,
    string Route,
    string DatabaseProfileId,
    string DatabaseProfileFingerprint,
    string StartupReceiptPath,
    bool KeepAlive);

internal sealed record RuntimeCleanupReceiptRecord(
    string CleanupReceiptPath,
    bool CleanupAttempted,
    IReadOnlyList<int> CleanupProcessIds,
    DateTimeOffset? CleanupCompletedAtUtc);

internal sealed record BrowserProofViewport(int Width, int Height);

internal sealed record BrowserProofToolOutput(string ToolName, string RelativePath);

internal sealed record ProcessBrowserProofValidationContext(
    Guid ProcessRunId,
    Guid ProcessStepRunId,
    Guid ExecutionRunId,
    Guid? ProjectId,
    DateTimeOffset? ExecutionStartedAtUtc,
    string RuntimeHostUrl,
    string DatabaseProfileId,
    string DatabaseProfileFingerprint,
    IReadOnlySet<string> SuccessfulBrowserOutputPaths,
    IReadOnlySet<string> SuccessfulBrowserToolNames,
    bool RequiresRepresentativeInteraction,
    bool RequiresCleanupReceipt);

internal sealed record ProcessBrowserProofValidationResult(bool IsValid, string Diagnostic)
{
    public static ProcessBrowserProofValidationResult Valid { get; } = new(true, string.Empty);

    public static ProcessBrowserProofValidationResult Invalid(string diagnostic)
    {
        return new ProcessBrowserProofValidationResult(false, diagnostic);
    }
}

internal static class ProcessBrowserProofValidator
{
    public const string SchemaVersion = "process-browser-proof/v1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static bool IsPotentialProofRecordPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalizedPath = NormalizePath(path);
        return normalizedPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
               (normalizedPath.Contains("browser-proof", StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.Contains("browser/proof", StringComparison.OrdinalIgnoreCase));
    }

    public static bool TryParse(string json, out ProcessBrowserProofRecord record, out string diagnostic)
    {
        record = EmptyRecord();
        diagnostic = string.Empty;

        if (string.IsNullOrWhiteSpace(json))
        {
            diagnostic = "browser proof record is empty";
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<ProcessBrowserProofRecord>(json, JsonOptions);
            if (parsed is null)
            {
                diagnostic = "browser proof record did not deserialize";
                return false;
            }

            record = parsed;
            return true;
        }
        catch (JsonException exception)
        {
            diagnostic = $"browser proof record JSON is invalid: {exception.Message}";
            return false;
        }
    }

    public static ProcessBrowserProofValidationResult Validate(
        ProcessBrowserProofRecord record,
        ProcessBrowserProofValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(context);

        if (!string.Equals(record.SchemaVersion, SchemaVersion, StringComparison.Ordinal))
        {
            return ProcessBrowserProofValidationResult.Invalid($"browser proof record schema must be '{SchemaVersion}'");
        }

        if (record.ProcessRunId != context.ProcessRunId)
        {
            return ProcessBrowserProofValidationResult.Invalid("browser proof record is for a different process run");
        }

        if (record.ProcessStepRunId != context.ProcessStepRunId)
        {
            return ProcessBrowserProofValidationResult.Invalid("browser proof record is for a different process step");
        }

        if (record.ExecutionRunId != context.ExecutionRunId)
        {
            return ProcessBrowserProofValidationResult.Invalid("browser proof record is for a different execution run");
        }

        if (context.ProjectId.HasValue && record.ProjectId.HasValue && record.ProjectId.Value != context.ProjectId.Value)
        {
            return ProcessBrowserProofValidationResult.Invalid("browser proof record is for a different project");
        }

        if (context.ExecutionStartedAtUtc.HasValue && record.CapturedAtUtc < context.ExecutionStartedAtUtc.Value)
        {
            return ProcessBrowserProofValidationResult.Invalid("browser proof record was captured before the current execution run started");
        }

        if (record.RuntimeHost is null)
        {
            return ProcessBrowserProofValidationResult.Invalid("browser proof runtime host identity is missing");
        }

        var hostResult = ValidateRuntimeHost(record.RuntimeHost, context);
        if (!hostResult.IsValid)
        {
            return hostResult;
        }

        if (record.Viewport is null)
        {
            return ProcessBrowserProofValidationResult.Invalid("browser proof viewport is missing");
        }

        var viewportResult = ValidateViewport(record.Viewport);
        if (!viewportResult.IsValid)
        {
            return viewportResult;
        }

        if (record.ToolOutputs is null)
        {
            return ProcessBrowserProofValidationResult.Invalid("browser proof record has no browser tool outputs");
        }

        var outputResult = ValidateToolOutputs(record, context);
        if (!outputResult.IsValid)
        {
            return outputResult;
        }

        var artifactResult = ValidateEvidenceArtifactPaths(record.EvidenceArtifactPaths ?? [], context.ProcessRunId);
        if (!artifactResult.IsValid)
        {
            return artifactResult;
        }

        if (context.RequiresRepresentativeInteraction &&
            !(record.InteractionToolNames ?? []).Any(IsRepresentativeBrowserInteractionTool))
        {
            return ProcessBrowserProofValidationResult.Invalid("browser proof record does not include a representative browser interaction tool");
        }

        if (context.RequiresCleanupReceipt || record.RuntimeHost.KeepAlive)
        {
            if (record.CleanupReceipt is null)
            {
                return ProcessBrowserProofValidationResult.Invalid("runtime cleanup receipt is missing");
            }

            var cleanupResult = ValidateCleanupReceipt(record.CleanupReceipt, record.RuntimeHost.KeepAlive);
            if (!cleanupResult.IsValid)
            {
                return cleanupResult;
            }
        }

        return ProcessBrowserProofValidationResult.Valid;
    }

    private static ProcessBrowserProofValidationResult ValidateRuntimeHost(
        RuntimeHostIdentityRecord host,
        ProcessBrowserProofValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(host.HostUrl) ||
            !Uri.TryCreate(host.HostUrl.Trim(), UriKind.Absolute, out var hostUri) ||
            hostUri.Scheme is not "http" and not "https")
        {
            return ProcessBrowserProofValidationResult.Invalid("browser proof runtime host URL is missing or invalid");
        }

        if (!string.IsNullOrWhiteSpace(context.RuntimeHostUrl) &&
            !string.Equals(NormalizeUrl(host.HostUrl), NormalizeUrl(context.RuntimeHostUrl), StringComparison.OrdinalIgnoreCase))
        {
            return ProcessBrowserProofValidationResult.Invalid("browser proof runtime host URL does not match the current runtime host");
        }

        if (string.IsNullOrWhiteSpace(host.Route))
        {
            return ProcessBrowserProofValidationResult.Invalid("browser proof route is missing");
        }

        if (!host.Route.Trim().StartsWith("/", StringComparison.Ordinal) &&
            !Uri.TryCreate(host.Route.Trim(), UriKind.Absolute, out _))
        {
            return ProcessBrowserProofValidationResult.Invalid("browser proof route must be an absolute URL or an app-relative route");
        }

        if (!string.IsNullOrWhiteSpace(context.DatabaseProfileId) &&
            !string.Equals(host.DatabaseProfileId.Trim(), context.DatabaseProfileId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return ProcessBrowserProofValidationResult.Invalid("browser proof database profile id does not match the current profile");
        }

        if (!string.IsNullOrWhiteSpace(context.DatabaseProfileFingerprint) &&
            !string.Equals(host.DatabaseProfileFingerprint.Trim(), context.DatabaseProfileFingerprint.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return ProcessBrowserProofValidationResult.Invalid("browser proof database profile fingerprint does not match the current profile");
        }

        if (!string.IsNullOrWhiteSpace(host.StartupReceiptPath) &&
            !IsCurrentRunBrowserOrRuntimeArtifactPath(host.StartupReceiptPath, context.ProcessRunId))
        {
            return ProcessBrowserProofValidationResult.Invalid("browser proof startup receipt is not bound to the current process run");
        }

        return ProcessBrowserProofValidationResult.Valid;
    }

    private static ProcessBrowserProofValidationResult ValidateViewport(BrowserProofViewport viewport)
    {
        if (viewport.Width is < 320 or > 7680 ||
            viewport.Height is < 240 or > 4320)
        {
            return ProcessBrowserProofValidationResult.Invalid("browser proof viewport is missing or outside supported bounds");
        }

        return ProcessBrowserProofValidationResult.Valid;
    }

    private static ProcessBrowserProofValidationResult ValidateToolOutputs(
        ProcessBrowserProofRecord record,
        ProcessBrowserProofValidationContext context)
    {
        if (record.ToolOutputs.Count == 0)
        {
            return ProcessBrowserProofValidationResult.Invalid("browser proof record has no browser tool outputs");
        }

        var normalizedSuccessfulOutputPaths = context.SuccessfulBrowserOutputPaths
            .Select(NormalizePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var normalizedSuccessfulToolNames = context.SuccessfulBrowserToolNames
            .Select(ToolContractCatalog.NormalizeToolName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var output in record.ToolOutputs)
        {
            if (output is null)
            {
                return ProcessBrowserProofValidationResult.Invalid("browser proof output is missing");
            }

            var toolName = ToolContractCatalog.NormalizeToolName(output.ToolName);
            if (!ToolContractCatalog.BrowserToolNames.Contains(toolName, StringComparer.OrdinalIgnoreCase))
            {
                return ProcessBrowserProofValidationResult.Invalid($"browser proof output uses unsupported browser tool '{output.ToolName}'");
            }

            if (normalizedSuccessfulToolNames.Count > 0 &&
                !normalizedSuccessfulToolNames.Contains(toolName))
            {
                return ProcessBrowserProofValidationResult.Invalid($"browser proof output tool '{output.ToolName}' was not executed in the current run");
            }

            var normalizedPath = NormalizePath(output.RelativePath);
            if (string.IsNullOrWhiteSpace(normalizedPath) ||
                Path.IsPathRooted(normalizedPath))
            {
                return ProcessBrowserProofValidationResult.Invalid("browser proof output path is missing or absolute");
            }

            if (normalizedSuccessfulOutputPaths.Count > 0 &&
                !normalizedSuccessfulOutputPaths.Contains(normalizedPath) &&
                !IsCurrentRunBrowserOrRuntimeArtifactPath(normalizedPath, context.ProcessRunId))
            {
                return ProcessBrowserProofValidationResult.Invalid($"browser proof output path '{output.RelativePath}' was not produced by the current execution");
            }
        }

        if (!record.ToolOutputs.Any(output => string.Equals(ToolContractCatalog.NormalizeToolName(output.ToolName), ToolContractCatalog.BrowserTakeScreenshot, StringComparison.OrdinalIgnoreCase)))
        {
            return ProcessBrowserProofValidationResult.Invalid("browser proof record does not include a screenshot output");
        }

        if (!record.ToolOutputs.Any(output =>
                string.Equals(ToolContractCatalog.NormalizeToolName(output.ToolName), ToolContractCatalog.BrowserSnapshot, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ToolContractCatalog.NormalizeToolName(output.ToolName), ToolContractCatalog.BrowserEvaluate, StringComparison.OrdinalIgnoreCase)))
        {
            return ProcessBrowserProofValidationResult.Invalid("browser proof record does not include browser state output");
        }

        if (!record.ToolOutputs.Any(output => string.Equals(ToolContractCatalog.NormalizeToolName(output.ToolName), ToolContractCatalog.BrowserConsoleMessages, StringComparison.OrdinalIgnoreCase)))
        {
            return ProcessBrowserProofValidationResult.Invalid("browser proof record does not include browser console output");
        }

        return ProcessBrowserProofValidationResult.Valid;
    }

    private static ProcessBrowserProofValidationResult ValidateEvidenceArtifactPaths(
        IReadOnlyList<string> evidenceArtifactPaths,
        Guid processRunId)
    {
        if (evidenceArtifactPaths.Count == 0)
        {
            return ProcessBrowserProofValidationResult.Invalid("browser proof record has no durable evidence artifact paths");
        }

        foreach (var path in evidenceArtifactPaths)
        {
            if (!IsCurrentRunBrowserOrRuntimeArtifactPath(path, processRunId))
            {
                return ProcessBrowserProofValidationResult.Invalid($"browser proof evidence path '{path}' is not bound to the current process run");
            }
        }

        return ProcessBrowserProofValidationResult.Valid;
    }

    private static ProcessBrowserProofValidationResult ValidateCleanupReceipt(
        RuntimeCleanupReceiptRecord cleanupReceipt,
        bool keepAlive)
    {
        if (string.IsNullOrWhiteSpace(cleanupReceipt.CleanupReceiptPath))
        {
            return ProcessBrowserProofValidationResult.Invalid("runtime cleanup receipt path is missing");
        }

        if (!cleanupReceipt.CleanupAttempted)
        {
            return ProcessBrowserProofValidationResult.Invalid("runtime cleanup receipt does not show cleanup was attempted");
        }

        if (keepAlive && cleanupReceipt.CleanupProcessIds.Count == 0)
        {
            return ProcessBrowserProofValidationResult.Invalid("runtime cleanup receipt does not include process ids for a kept-alive host");
        }

        return ProcessBrowserProofValidationResult.Valid;
    }

    private static bool IsRepresentativeBrowserInteractionTool(string toolName)
    {
        var normalized = ToolContractCatalog.NormalizeToolName(toolName);
        return ToolContractCatalog.RepresentativeBrowserInteractionToolNames.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsCurrentRunBrowserOrRuntimeArtifactPath(string path, Guid processRunId)
    {
        var normalizedPath = NormalizePath(path);
        if (string.IsNullOrWhiteSpace(normalizedPath) ||
            Path.IsPathRooted(normalizedPath))
        {
            return false;
        }

        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var runIdText = processRunId.ToString("D");
        for (var index = 0; index < segments.Length - 2; index++)
        {
            if (!string.Equals(segments[index], "process-runs", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(segments[index + 1], runIdText, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return string.Equals(segments[index + 2], "browser", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(segments[index + 2], "runtime", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static string NormalizeUrl(string url)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return url.Trim();
        }

        return uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped).TrimEnd('/');
    }

    private static string NormalizePath(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : path.Replace('\\', '/').Trim().TrimStart('/').TrimEnd();
    }

    private static ProcessBrowserProofRecord EmptyRecord()
    {
        return new ProcessBrowserProofRecord(
            string.Empty,
            Guid.Empty,
            Guid.Empty,
            Guid.Empty,
            null,
            new RuntimeHostIdentityRecord(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, false),
            new BrowserProofViewport(0, 0),
            [],
            [],
            [],
            new RuntimeCleanupReceiptRecord(string.Empty, false, [], null),
            DateTimeOffset.MinValue);
    }
}
