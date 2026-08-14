using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Persistence;

public sealed class ZipAgentPackageService : IAgentPackageService
{
    private const string ManagerReviewInputExportSummary = "HR manager-review request redacted for export.";
    private const string ManagerReviewResultExportSummary = "HR manager-review response redacted for export.";
    private const string ManagerReviewLogExportMessage = "HR manager-review execution log redacted for export.";
    private const string SupportedPackageSchemaVersion = "1.0";

    private static readonly IReadOnlySet<string> AllowedArchiveEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "manifest.json",
        "agent.md",
        "instructions.txt",
        "memory.json",
        "sessions.json",
        "metrics.json"
    };

    private static readonly IReadOnlySet<string> RawSecretPropertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "accessToken",
        "apiKey",
        "bearerToken",
        "clientSecret",
        "password",
        "privateKey",
        "refreshToken"
    };

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly DurableFileWriter durableFileWriter;
    private readonly string exportRoot;
    private readonly string workspaceRoot;

    public ZipAgentPackageService(
        string workspaceRoot,
        WorkspaceScopeDescriptor? workspaceScope = null)
    {
        var physicalPathPolicyFactory = new PhysicalFileSystemPathPolicyFactory();
        this.workspaceRoot = physicalPathPolicyFactory.Create(workspaceRoot).RootPath;
        exportRoot = Path.Combine(
            (workspaceScope ?? WorkspaceScopeDescriptor.Sandbox).ResolveDataRoot(this.workspaceRoot),
            "exports");
        durableFileWriter = new DurableFileWriter(physicalPathPolicyFactory);
    }

    public async Task<AgentExportResult> ExportAsync(
        SandboxWorkspaceDocument document,
        AgentDefinition agent,
        CancellationToken cancellationToken = default)
    {
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

        string safeName = PortablePhysicalFileNamePolicy.Encode(agent.Name).PhysicalName;
        string packagePath = Path.Combine(
            exportRoot,
            $"{safeName}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fffffff}-{Guid.NewGuid():N}.zip");
        await durableFileWriter.WriteStreamAsync(
            workspaceRoot,
            packagePath,
            async (stream, token) =>
            {
                using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
                await WriteArchiveEntryAsync(
                    archive,
                    "manifest.json",
                    JsonSerializer.Serialize(manifest, SerializerOptions),
                    token);
                await WriteArchiveEntryAsync(archive, "agent.md", BuildMarkdownSummary(agent, manifest), token);
                await WriteArchiveEntryAsync(archive, "instructions.txt", agent.Instructions, token);
                await WriteArchiveEntryAsync(
                    archive,
                    "memory.json",
                    JsonSerializer.Serialize(manifest.Memory, SerializerOptions),
                    token);
                await WriteArchiveEntryAsync(
                    archive,
                    "sessions.json",
                    JsonSerializer.Serialize(manifest.Sessions, SerializerOptions),
                    token);
                await WriteArchiveEntryAsync(
                    archive,
                    "metrics.json",
                    JsonSerializer.Serialize(manifest.Metrics, SerializerOptions),
                    token);
            },
            cancellationToken: cancellationToken);

        return new AgentExportResult(packagePath, $"Exported {agent.Name} with {manifest.Sessions.Count} chat session(s) and {manifest.Memory.Count} memory item(s).");
    }

    private static async Task WriteArchiveEntryAsync(
        ZipArchive archive,
        string entryName,
        string content,
        CancellationToken cancellationToken)
    {
        ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await using Stream entryStream = entry.Open();
        await using var writer = new StreamWriter(entryStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await writer.WriteAsync(content.AsMemory(), cancellationToken);
    }

    public async Task<AgentImportResult> ImportAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException("The selected package does not exist.", packagePath);
        }

        await using var package = File.OpenRead(packagePath);
        return await ImportAsync(package, new AgentPackageReadOptions(), cancellationToken);
    }

    public async Task<AgentImportResult> ImportAsync(
        Stream package,
        AgentPackageReadOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(options);
        ValidateReadOptions(options);

        await using var packageBuffer = await ReadBoundedPackageAsync(package, options.MaximumPackageBytes, cancellationToken);
        var packageSha256 = Convert.ToHexString(SHA256.HashData(packageBuffer.GetBuffer().AsSpan(0, checked((int)packageBuffer.Length))));
        if (!string.IsNullOrWhiteSpace(options.ExpectedPackageSha256) &&
            !string.Equals(packageSha256, NormalizeSha256(options.ExpectedPackageSha256), StringComparison.OrdinalIgnoreCase))
        {
            throw new AgentPackageValidationException(
                "agent-package.hash-mismatch",
                "The uploaded package does not match the expected SHA-256 hash.");
        }

        packageBuffer.Position = 0;
        try
        {
            using var archive = new ZipArchive(packageBuffer, ZipArchiveMode.Read, leaveOpen: true);
            ValidateArchiveEntries(archive, options);
            var manifestEntry = archive.GetEntry("manifest.json")
                ?? throw new AgentPackageValidationException(
                    "agent-package.manifest-missing",
                    "The package is missing manifest.json.");
            var json = await ReadManifestAsync(manifestEntry, options.MaximumManifestBytes, cancellationToken);
            RejectRawSecretMaterial(json);
            var manifest = JsonSerializer.Deserialize<AgentPackageManifest>(json, SerializerOptions)
                ?? throw new AgentPackageValidationException(
                    "agent-package.manifest-invalid",
                    "The package manifest could not be read.");
            if (!string.Equals(manifest.SchemaVersion, SupportedPackageSchemaVersion, StringComparison.Ordinal))
            {
                throw new AgentPackageValidationException(
                    "agent-package.schema-version-unsupported",
                    $"Package schema version '{manifest.SchemaVersion}' is not supported.");
            }

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
                ToolReceipts = manifest.ToolReceipts,
                PackageSha256 = packageSha256,
                PackageSchemaVersion = manifest.SchemaVersion
            };
        }
        catch (AgentPackageValidationException)
        {
            throw;
        }
        catch (InvalidDataException exception)
        {
            throw new AgentPackageValidationException(
                "agent-package.invalid-archive",
                $"The uploaded package is not a valid agent archive: {exception.Message}");
        }
        catch (JsonException exception)
        {
            throw new AgentPackageValidationException(
                "agent-package.manifest-invalid",
                $"The package manifest is invalid JSON: {exception.Message}");
        }
    }

    private static void ValidateReadOptions(AgentPackageReadOptions options)
    {
        if (options.MaximumPackageBytes <= 0 ||
            options.MaximumExpandedBytes <= 0 ||
            options.MaximumEntryCount <= 0 ||
            options.MaximumManifestBytes <= 0 ||
            options.MaximumManifestBytes > options.MaximumExpandedBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Package import limits must be positive and internally consistent.");
        }
    }

    private static async Task<MemoryStream> ReadBoundedPackageAsync(
        Stream package,
        long maximumPackageBytes,
        CancellationToken cancellationToken)
    {
        var capacity = checked((int)Math.Min(maximumPackageBytes, 1024 * 1024));
        var buffer = new MemoryStream(capacity);
        var chunk = new byte[64 * 1024];
        long total = 0;

        while (true)
        {
            var read = await package.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > maximumPackageBytes)
            {
                await buffer.DisposeAsync();
                throw new AgentPackageValidationException(
                    "agent-package.too-large",
                    $"The uploaded package exceeds the {maximumPackageBytes}-byte limit.");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        buffer.Position = 0;
        return buffer;
    }

    private static void ValidateArchiveEntries(ZipArchive archive, AgentPackageReadOptions options)
    {
        if (archive.Entries.Count == 0)
        {
            throw new AgentPackageValidationException("agent-package.empty", "The uploaded package is empty.");
        }

        if (archive.Entries.Count > options.MaximumEntryCount)
        {
            throw new AgentPackageValidationException(
                "agent-package.too-many-entries",
                $"The uploaded package exceeds the {options.MaximumEntryCount}-entry limit.");
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long expandedBytes = 0;
        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName;
            if (string.IsNullOrWhiteSpace(name) ||
                Path.IsPathRooted(name) ||
                name.Contains('\\') ||
                name.Contains('/') ||
                name is "." or ".." ||
                !AllowedArchiveEntries.Contains(name))
            {
                throw new AgentPackageValidationException(
                    "agent-package.entry-not-allowed",
                    $"Archive entry '{name}' is not allowed.");
            }

            if (!names.Add(name))
            {
                throw new AgentPackageValidationException(
                    "agent-package.duplicate-entry",
                    $"Archive entry '{name}' appears more than once.");
            }

            var unixMode = (entry.ExternalAttributes >> 16) & 0xFFFF;
            if ((unixMode & 0xF000) == 0xA000)
            {
                throw new AgentPackageValidationException(
                    "agent-package.symlink-not-allowed",
                    $"Archive entry '{name}' is a symbolic link.");
            }

            if ((unixMode & 0x49) != 0)
            {
                throw new AgentPackageValidationException(
                    "agent-package.executable-not-allowed",
                    $"Archive entry '{name}' is executable.");
            }

            if (entry.Length < 0 || entry.Length > options.MaximumExpandedBytes - expandedBytes)
            {
                throw new AgentPackageValidationException(
                    "agent-package.expanded-size-exceeded",
                    $"The uploaded package exceeds the {options.MaximumExpandedBytes}-byte expanded-size limit.");
            }

            expandedBytes += entry.Length;
        }

        if (!names.Contains("manifest.json"))
        {
            throw new AgentPackageValidationException(
                "agent-package.manifest-missing",
                "The package is missing manifest.json.");
        }
    }

    private static async Task<string> ReadManifestAsync(
        ZipArchiveEntry manifestEntry,
        long maximumManifestBytes,
        CancellationToken cancellationToken)
    {
        if (manifestEntry.Length > maximumManifestBytes)
        {
            throw new AgentPackageValidationException(
                "agent-package.manifest-too-large",
                $"manifest.json exceeds the {maximumManifestBytes}-byte limit.");
        }

        await using var manifestStream = manifestEntry.Open();
        using var reader = new StreamReader(
            manifestStream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 16 * 1024,
            leaveOpen: false);
        var json = await reader.ReadToEndAsync(cancellationToken);
        if (Encoding.UTF8.GetByteCount(json) > maximumManifestBytes)
        {
            throw new AgentPackageValidationException(
                "agent-package.manifest-too-large",
                $"manifest.json exceeds the {maximumManifestBytes}-byte limit.");
        }

        return json;
    }

    private static void RejectRawSecretMaterial(string json)
    {
        using var document = JsonDocument.Parse(json);
        InspectElementForRawSecrets(document.RootElement, "$");
    }

    private static void InspectElementForRawSecrets(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var propertyPath = $"{path}.{property.Name}";
                if (RawSecretPropertyNames.Contains(property.Name) &&
                    property.Value.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(property.Value.GetString()))
                {
                    throw new AgentPackageValidationException(
                        "agent-package.raw-secret-material",
                        $"Raw secret material is not allowed at '{propertyPath}'.");
                }

                InspectElementForRawSecrets(property.Value, propertyPath);
                if (property.Name.EndsWith("Json", StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.String)
                {
                    InspectEmbeddedJsonForRawSecrets(property.Value.GetString(), propertyPath);
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                InspectElementForRawSecrets(item, $"{path}[{index++}]");
            }
        }
    }

    private static void InspectEmbeddedJsonForRawSecrets(string? json, string path)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            InspectElementForRawSecrets(document.RootElement, path);
        }
        catch (JsonException)
        {
            // Other model validation owns malformed opaque configuration JSON.
        }
    }

    private static string NormalizeSha256(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new AgentPackageValidationException(
                "agent-package.expected-hash-invalid",
                "Expected package SHA-256 must contain exactly 64 hexadecimal characters.");
        }

        return normalized;
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
                    MetadataJson = ExecutionInvocationMetadata.ProtectForUntrustedOutput(run.MetadataJson),
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
            ArgumentsJson = AgentToolInvocationPolicyMetadata.ProtectPreviouslyProtectedApprovalArgumentsForExport(
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
                    : AgentToolInvocationPolicyMetadata.ProtectPreviouslyProtectedApprovalArgumentsForExport(
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
        public string SchemaVersion { get; init; } = SupportedPackageSchemaVersion;
        public IReadOnlyList<ExecutionRunRecord> Runs { get; init; } = [];
        public IReadOnlyList<ExecutionApprovalRecord> Approvals { get; init; } = [];
        public IReadOnlyList<ExecutionArtifactRecord> Artifacts { get; init; } = [];
        public IReadOnlyList<ExecutionWorkflowCheckpointRecord> Checkpoints { get; init; } = [];
        public IReadOnlyList<ToolExecutionReceiptRecord> ToolReceipts { get; init; } = [];
    }
}
