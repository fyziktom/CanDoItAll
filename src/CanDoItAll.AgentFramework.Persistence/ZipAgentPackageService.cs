using System.IO.Compression;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

public sealed class ZipAgentPackageService(string workspaceRoot, WorkspaceScopeDescriptor? workspaceScope = null) : IAgentPackageService
{
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

        var exportedRuns = NormalizeExecutionRuns(document.ExecutionRuns.Where(item => item.AgentId == agent.Id).ToList());
        var latestRunBySessionId = BuildLatestRunBySessionId(exportedRuns);

        var manifest = new AgentPackageManifest(
            Agent: agent,
            Sessions: NormalizeChatSessions(document.ChatSessions.Where(item => item.AgentId == agent.Id).ToList(), latestRunBySessionId),
            ExecutionLog: document.ExecutionLog.Where(item => item.AgentId == agent.Id).ToList(),
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
            Approvals = document.ExecutionApprovals.Where(item => runIds.Contains(item.ExecutionRunId)).ToList(),
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
                    : ChatSessionRuntimeCompatibilityRecord.Create(
                        session.Compatibility?.RuntimeSessionKey,
                        session.Compatibility?.SerializedSessionStateJson,
                        session.Compatibility?.PendingApprovals,
                        session.Compatibility?.AutoApprovePendingToolCalls ?? false),
                LatestExecutionRunId = latestRunBySessionId.TryGetValue(session.Id, out var latestRun)
                    ? latestRun.Id
                    : session.LatestExecutionRunId
            })
            .OrderByDescending(session => session.UpdatedAtUtc)
            .ToList();
    }

    private static IReadOnlyList<ExecutionRunRecord> NormalizeExecutionRuns(IReadOnlyList<ExecutionRunRecord> runs)
    {
        return runs
            .Select(run => run with
            {
                MetadataJson = string.IsNullOrWhiteSpace(run.MetadataJson) ? "{}" : run.MetadataJson,
                PendingApprovals = run.PendingApprovals ?? []
            })
            .OrderByDescending(run => run.UpdatedAtUtc)
            .ThenByDescending(run => run.CreatedAtUtc)
            .ToList();
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
