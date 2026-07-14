using System.IO.Compression;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

public sealed class ZipAgentPackageService(string workspaceRoot, WorkspaceScopeDescriptor? workspaceScope = null) : IAgentPackageService
{
    private const string ManagerReviewInputExportSummary = "HR manager-review request redacted for export.";
    private const string ManagerReviewResultExportSummary = "HR manager-review response redacted for export.";
    private const string ManagerReviewLogExportMessage = "HR manager-review execution log redacted for export.";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string exportRoot = Path.Combine((workspaceScope ?? WorkspaceScopeDescriptor.Sandbox).ResolveDataRoot(workspaceRoot), "exports");

    public async Task<AgentExportResult> ExportAsync(
        SandboxWorkspaceDocument document,
        AgentDefinition agent,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(exportRoot);

        var sensitiveHrApprovalRunIds = document.ExecutionApprovals
            .Where(approval => AgentToolInvocationPolicyMetadata.HasSensitiveHrArguments(approval.ToolName))
            .Select(approval => approval.ExecutionRunId)
            .ToHashSet();
        var exportedRuns = NormalizeExecutionRuns(
            document.ExecutionRuns.Where(item => item.AgentId == agent.Id).ToList(),
            sensitiveHrApprovalRunIds);
        var latestRunBySessionId = BuildLatestRunBySessionId(exportedRuns);
        var managerReviewRunIds = exportedRuns
            .Where(IsManagerReviewRun)
            .Select(run => run.Id)
            .ToHashSet();

        var manifest = new AgentPackageManifest(
            Agent: agent,
            Sessions: NormalizeChatSessions(document.ChatSessions.Where(item => item.AgentId == agent.Id).ToList(), latestRunBySessionId),
            ExecutionLog: ProtectExecutionLogForExport(
                document.ExecutionLog.Where(item => item.AgentId == agent.Id),
                managerReviewRunIds),
            Metrics: document.Metrics.Where(item => item.AgentId == agent.Id).ToList(),
            Memory: document.Memory.Where(item => item.AgentId == agent.Id).ToList(),
            Providers: document.Providers.Where(item => item.Id == agent.ProviderProfileId).ToList(),
            Capabilities: document.Capabilities.Where(item => agent.Capabilities.Select(capability => capability.CapabilityId).Contains(item.Id)).ToList())
        {
            Runs = exportedRuns
        };
        var runIds = manifest.Runs.Select(item => item.Id).ToHashSet();
        manifest = manifest with
        {
            Approvals = document.ExecutionApprovals
                .Where(item => runIds.Contains(item.ExecutionRunId))
                .Select(item => ProtectApprovalForExport(
                    item,
                    managerReviewRunIds.Contains(item.ExecutionRunId)))
                .ToList(),
            Artifacts = document.ExecutionArtifacts.Where(item => runIds.Contains(item.ExecutionRunId)).ToList(),
            Checkpoints = document.ExecutionWorkflowCheckpoints.Where(item => runIds.Contains(item.ExecutionRunId)).ToList(),
            ToolReceipts = document.ToolExecutionReceipts.Where(item => runIds.Contains(item.ExecutionRunId)).ToList()
        };

        var safeName = string.Concat(agent.Name.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '-' : character)).Trim();
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "agent";
        }

        var packagePath = Path.Combine(exportRoot, $"{safeName}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.zip");
        var tempRoot = Path.Combine(Path.GetTempPath(), $"agent-package-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var manifestPath = Path.Combine(tempRoot, "manifest.json");
            var markdownPath = Path.Combine(tempRoot, "agent.md");
            var instructionsPath = Path.Combine(tempRoot, "instructions.txt");
            var memoryPath = Path.Combine(tempRoot, "memory.json");
            var sessionsPath = Path.Combine(tempRoot, "sessions.json");
            var metricsPath = Path.Combine(tempRoot, "metrics.json");

            await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, SerializerOptions), cancellationToken);
            await File.WriteAllTextAsync(markdownPath, BuildMarkdownSummary(agent, manifest), cancellationToken);
            await File.WriteAllTextAsync(instructionsPath, agent.Instructions, Encoding.UTF8, cancellationToken);
            await File.WriteAllTextAsync(memoryPath, JsonSerializer.Serialize(manifest.Memory, SerializerOptions), cancellationToken);
            await File.WriteAllTextAsync(sessionsPath, JsonSerializer.Serialize(manifest.Sessions, SerializerOptions), cancellationToken);
            await File.WriteAllTextAsync(metricsPath, JsonSerializer.Serialize(manifest.Metrics, SerializerOptions), cancellationToken);

            if (File.Exists(packagePath))
            {
                File.Delete(packagePath);
            }

            ZipFile.CreateFromDirectory(tempRoot, packagePath);
            return new AgentExportResult(packagePath, $"Exported {agent.Name} with {manifest.Sessions.Count} chat session(s) and {manifest.Memory.Count} memory item(s).");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    public async Task<AgentImportResult> ImportAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException("The selected package does not exist.", packagePath);
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), $"agent-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            ZipFile.ExtractToDirectory(packagePath, tempRoot);
            var manifestPath = Path.Combine(tempRoot, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                throw new InvalidOperationException("The package is missing manifest.json.");
            }

            var json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            var manifest = JsonSerializer.Deserialize<AgentPackageManifest>(json, SerializerOptions)
                ?? throw new InvalidOperationException("The package manifest could not be read.");
            var importedRuns = NormalizeExecutionRuns(manifest.Runs);
            var latestRunBySessionId = BuildLatestRunBySessionId(importedRuns);

            return new AgentImportResult(
                manifest.Agent,
                NormalizeChatSessions(manifest.Sessions, latestRunBySessionId),
                manifest.ExecutionLog,
                manifest.Metrics,
                manifest.Memory,
                manifest.Providers,
                manifest.Capabilities)
            {
                Runs = importedRuns,
                Approvals = manifest.Approvals,
                Artifacts = manifest.Artifacts,
                Checkpoints = manifest.Checkpoints,
                ToolReceipts = manifest.ToolReceipts
            };
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static string BuildMarkdownSummary(AgentDefinition agent, AgentPackageManifest manifest)
    {
        var lines = new List<string>
        {
            $"# {agent.Name}",
            string.Empty,
            $"Role: {agent.RoleTitle}",
            string.Empty,
            agent.Summary,
            string.Empty,
            "## Capabilities",
            string.Empty
        };

        lines.AddRange(agent.Capabilities.Select(item => $"- `{item.Kind}` {item.CapabilityKey} [{item.ProofStatus}]"));
        lines.Add(string.Empty);
        lines.Add("## Package Contents");
        lines.Add(string.Empty);
        lines.Add($"- Sessions: {manifest.Sessions.Count}");
        lines.Add($"- Runs: {manifest.Runs.Count}");
        lines.Add($"- Approvals: {manifest.Approvals.Count}");
        lines.Add($"- Checkpoints: {manifest.Checkpoints.Count}");
        lines.Add($"- Receipts: {manifest.ToolReceipts.Count}");
        lines.Add($"- Artifacts: {manifest.Artifacts.Count}");
        lines.Add($"- Memory items: {manifest.Memory.Count}");
        lines.Add($"- Metrics: {manifest.Metrics.Count}");
        return string.Join(Environment.NewLine, lines);
    }

    private static IReadOnlyList<ChatSessionRecord> NormalizeChatSessions(
        IReadOnlyList<ChatSessionRecord> sessions,
        IReadOnlyDictionary<Guid, ExecutionRunRecord> latestRunBySessionId)
    {
        return sessions
            .Select(session => session with
            {
                Messages = session.Messages ?? [],
                Compatibility = latestRunBySessionId.ContainsKey(session.Id)
                    ? null
                    : ProtectChatSessionCompatibilityForExport(session.Compatibility),
                LatestExecutionRunId = latestRunBySessionId.TryGetValue(session.Id, out var latestRun)
                    ? latestRun.Id
                    : session.LatestExecutionRunId
            })
            .OrderByDescending(session => session.UpdatedAtUtc)
            .ToList();
    }

    private static IReadOnlyList<ExecutionRunRecord> NormalizeExecutionRuns(
        IReadOnlyList<ExecutionRunRecord> runs,
        IReadOnlySet<Guid>? sensitiveHrApprovalRunIds = null)
    {
        var protectedRunIds = sensitiveHrApprovalRunIds ?? new HashSet<Guid>();
        return runs
            .Select(run =>
            {
                var pendingApprovals = run.PendingApprovals ?? [];
                var hasSensitiveHrApproval = protectedRunIds.Contains(run.Id) ||
                                             pendingApprovals.Any(approval =>
                                                 AgentToolInvocationPolicyMetadata.HasSensitiveHrArguments(approval.ToolName));
                var isManagerReview = IsManagerReviewRun(run);
                return run with
                {
                    MetadataJson = string.IsNullOrWhiteSpace(run.MetadataJson) ? "{}" : run.MetadataJson,
                    InputSummary = isManagerReview ? ManagerReviewInputExportSummary : run.InputSummary,
                    ResultSummary = isManagerReview ? ManagerReviewResultExportSummary : run.ResultSummary,
                    RuntimeSessionKey = isManagerReview || hasSensitiveHrApproval
                        ? string.Empty
                        : run.RuntimeSessionKey,
                    SerializedSessionStateJson = isManagerReview || hasSensitiveHrApproval
                        ? null
                        : run.SerializedSessionStateJson,
                    PendingApprovals = ProtectPendingApprovalsForExport(
                        pendingApprovals,
                        protectAll: isManagerReview)
                };
            })
            .OrderByDescending(run => run.UpdatedAtUtc)
            .ThenByDescending(run => run.CreatedAtUtc)
            .ToList();
    }

    private static ChatSessionRuntimeCompatibilityRecord? ProtectChatSessionCompatibilityForExport(
        ChatSessionRuntimeCompatibilityRecord? compatibility)
    {
        if (compatibility is null)
        {
            return null;
        }

        var hasSensitiveHrApproval = compatibility.PendingApprovals.Any(approval =>
            AgentToolInvocationPolicyMetadata.HasSensitiveHrArguments(approval.ToolName));
        return ChatSessionRuntimeCompatibilityRecord.Create(
            hasSensitiveHrApproval ? string.Empty : compatibility.RuntimeSessionKey,
            hasSensitiveHrApproval ? null : compatibility.SerializedSessionStateJson,
            ProtectPendingApprovalsForExport(compatibility.PendingApprovals),
            compatibility.AutoApprovePendingToolCalls);
    }

    private static IReadOnlyList<ExecutionLogEntry> ProtectExecutionLogForExport(
        IEnumerable<ExecutionLogEntry> executionLog,
        IReadOnlySet<Guid> managerReviewRunIds)
    {
        return executionLog
            .Select(entry => managerReviewRunIds.Contains(entry.ExecutionRunId)
                ? entry with { Message = ManagerReviewLogExportMessage }
                : entry)
            .ToList();
    }

    private static ExecutionApprovalRecord ProtectApprovalForExport(
        ExecutionApprovalRecord approval,
        bool protectAll)
    {
        if (protectAll)
        {
            return approval with
            {
                Details = HrAgentExecutionRetention.ManagerReviewApprovalDetails,
                ArgumentsJson = HrAgentExecutionRetention.ManagerReviewApprovalArgumentsJson
            };
        }

        return approval with
        {
            ArgumentsJson = AgentToolInvocationPolicyMetadata.ProtectApprovalArgumentsForAudit(
                approval.ToolName,
                approval.ArgumentsJson)
        };
    }

    private static IReadOnlyList<PendingToolApprovalRecord> ProtectPendingApprovalsForExport(
        IReadOnlyList<PendingToolApprovalRecord>? approvals,
        bool protectAll = false)
    {
        return approvals?
            .Select(approval => approval with
            {
                Details = protectAll
                    ? HrAgentExecutionRetention.ManagerReviewApprovalDetails
                    : approval.Details,
                ArgumentsJson = protectAll
                    ? HrAgentExecutionRetention.ManagerReviewApprovalArgumentsJson
                    : AgentToolInvocationPolicyMetadata.ProtectApprovalArgumentsForAudit(
                        approval.ToolName,
                        approval.ArgumentsJson)
            })
            .ToList() ?? [];
    }

    private static bool IsManagerReviewRun(ExecutionRunRecord run)
    {
        return string.Equals(
            run.SourceKind,
            HrAgentExecutionSourceKinds.ManagerReview,
            StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<Guid, ExecutionRunRecord> BuildLatestRunBySessionId(IReadOnlyList<ExecutionRunRecord> runs)
    {
        return runs
            .Where(run => run.ChatSessionId.HasValue)
            .GroupBy(run => run.ChatSessionId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(run => run.UpdatedAtUtc)
                    .ThenByDescending(run => run.CreatedAtUtc)
                    .First());
    }

    private sealed record AgentPackageManifest(
        AgentDefinition Agent,
        IReadOnlyList<ChatSessionRecord> Sessions,
        IReadOnlyList<ExecutionLogEntry> ExecutionLog,
        IReadOnlyList<AgentRunMetric> Metrics,
        IReadOnlyList<AgentMemoryRecord> Memory,
        IReadOnlyList<ProviderProfile> Providers,
        IReadOnlyList<CapabilityCatalogItem> Capabilities)
    {
        public IReadOnlyList<ExecutionRunRecord> Runs { get; init; } = [];
        public IReadOnlyList<ExecutionApprovalRecord> Approvals { get; init; } = [];
        public IReadOnlyList<ExecutionArtifactRecord> Artifacts { get; init; } = [];
        public IReadOnlyList<ExecutionWorkflowCheckpointRecord> Checkpoints { get; init; } = [];
        public IReadOnlyList<ToolExecutionReceiptRecord> ToolReceipts { get; init; } = [];
    }
}
