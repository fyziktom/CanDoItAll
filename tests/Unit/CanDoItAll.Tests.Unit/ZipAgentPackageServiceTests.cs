using CanDoItAll.Modules.AgentFramework;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ZipAgentPackageServiceTests
{
    private static readonly DateTimeOffset FixedTimestamp =
        new(2026, 7, 25, 12, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ImportAsync_ValidArchive_ReturnsAgentSchemaAndComputedHash()
    {
        var agent = CreateAgent();
        var packageBytes = CreatePackage(agent);
        var expectedHash = Convert.ToHexString(SHA256.HashData(packageBytes));
        var sut = new ZipAgentPackageService(Path.GetTempPath());

        await using var package = new MemoryStream(packageBytes);
        var result = await sut.ImportAsync(
            package,
            new AgentPackageReadOptions { ExpectedPackageSha256 = expectedHash.ToLowerInvariant() });

        Assert.Equal(agent.Id, result.Agent.Id);
        Assert.Equal(agent.Name, result.Agent.Name);
        Assert.Equal(agent.ConfigurationJson, result.Agent.ConfigurationJson);
        Assert.Equal("1.0", result.PackageSchemaVersion);
        Assert.Equal(expectedHash, result.PackageSha256);
        Assert.Empty(result.Sessions);
        Assert.Empty(result.Providers);
        Assert.Empty(result.Capabilities);
    }

    [Theory]
    [InlineData("../payload.json")]
    [InlineData("setup.exe")]
    public async Task ImportAsync_ArchiveContainsUnallowedEntry_RejectsArchive(string entryName)
    {
        var packageBytes = CreatePackage(CreateAgent(), additionalEntries: [entryName]);
        var sut = new ZipAgentPackageService(Path.GetTempPath());

        await using var package = new MemoryStream(packageBytes);
        var exception = await Assert.ThrowsAsync<AgentPackageValidationException>(
            () => sut.ImportAsync(package, new AgentPackageReadOptions()));

        Assert.Equal("agent-package.entry-not-allowed", exception.Code);
        Assert.Contains(entryName, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_ManifestContainsRawSecret_RejectsPackage()
    {
        var packageBytes = CreatePackage(CreateAgent(), rawSecret: "do-not-import-this-secret");
        var sut = new ZipAgentPackageService(Path.GetTempPath());

        await using var package = new MemoryStream(packageBytes);
        var exception = await Assert.ThrowsAsync<AgentPackageValidationException>(
            () => sut.ImportAsync(package, new AgentPackageReadOptions()));

        Assert.Equal("agent-package.raw-secret-material", exception.Code);
        Assert.Contains("$.apiKey", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_UnsupportedSchemaVersion_RejectsPackage()
    {
        var packageBytes = CreatePackage(CreateAgent(), schemaVersion: "2.0");
        var sut = new ZipAgentPackageService(Path.GetTempPath());

        await using var package = new MemoryStream(packageBytes);
        var exception = await Assert.ThrowsAsync<AgentPackageValidationException>(
            () => sut.ImportAsync(package, new AgentPackageReadOptions()));

        Assert.Equal("agent-package.schema-version-unsupported", exception.Code);
        Assert.Contains("'2.0'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_ExpectedHashDoesNotMatch_RejectsPackageBeforeImport()
    {
        var packageBytes = CreatePackage(CreateAgent());
        var sut = new ZipAgentPackageService(Path.GetTempPath());

        await using var package = new MemoryStream(packageBytes);
        var exception = await Assert.ThrowsAsync<AgentPackageValidationException>(
            () => sut.ImportAsync(
                package,
                new AgentPackageReadOptions { ExpectedPackageSha256 = new string('0', 64) }));

        Assert.Equal("agent-package.hash-mismatch", exception.Code);
        Assert.Contains("SHA-256", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportAsync_ExecutionRunWithExternalTargetBinding_ExportsAliasWithoutProtectedBinding()
    {
        const string rootId = "0123456789abcdef01234567";
        const string alias = $"external-target/v1/{rootId}/output";
        const string protectedRootToken = "protected-root-token-that-must-not-be-exported";
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"agent-package-export-{Guid.NewGuid():N}");
        var agent = CreateAgent();
        var metadataJson = ExecutionInvocationMetadata.ApplyExternalTargetRootBindings(
            $$"""
              {
                "{{ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey}}": ["{{alias}}"]
              }
              """,
            [new ExternalTargetRootBinding(rootId, "windows", protectedRootToken)]);
        var document = SandboxWorkspaceDocument.Empty with
        {
            Agents = [agent],
            ExecutionRuns = [CreateExecutionRun(agent.Id, metadataJson)]
        };

        try
        {
            var result = await new ZipAgentPackageService(workspaceRoot).ExportAsync(document, agent);
            using var archive = ZipFile.OpenRead(result.PackagePath);
            var manifestEntry = archive.GetEntry("manifest.json");
            Assert.NotNull(manifestEntry);
            using var reader = new StreamReader(manifestEntry.Open(), Encoding.UTF8);
            var manifestJson = await reader.ReadToEndAsync();

            Assert.Contains(alias, manifestJson, StringComparison.Ordinal);
            Assert.DoesNotContain(protectedRootToken, manifestJson, StringComparison.Ordinal);
            Assert.DoesNotContain(
                ExecutionInvocationMetadata.ExternalTargetRootBindingsMetadataKey,
                manifestJson,
                StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExportAsync_owner_policy_protects_legacy_pending_and_decided_prompt_approvals() {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"prompt-policy-export-{Guid.NewGuid():N}");
        var agent = CreateAgent();
        var policies = new AgentToolPolicyCatalog(PromptGalleryToolPolicy.Capabilities);
        const string privateContent = "prompt-export-private-sentinel";
        var pending = new PendingToolApprovalRecord("approval-1", "call-1", PromptGalleryToolPolicy.PromptGalleryDraftUpdate,
            "function", "", JsonSerializer.Serialize(new { request = new { promptArtifactId = "item-42", content = privateContent } }));
        var run = CreateExecutionRun(agent.Id, "{}") with { PendingApprovals = [pending] };
        var decision = ExecutionRunStateTransitions.ApplyApprovalDecision([], run,
            [new PendingToolApprovalDecision(pending.ApprovalId, true)], FixedTimestamp, "execution-run", run.Id.ToString("N"), policies);
        var audit = Assert.Single(decision.Decided).ArgumentsJson;
        var document = SandboxWorkspaceDocument.Empty with {
            Agents = [agent],
            ExecutionRuns = [run],
            ExecutionApprovals = decision.RunApprovals
        };

        try {
            var result = await new ZipAgentPackageService(workspaceRoot, toolPolicies: policies).ExportAsync(document, agent);
            using var archive = ZipFile.OpenRead(result.PackagePath);
            using var reader = new StreamReader(archive.GetEntry("manifest.json")!.Open(), Encoding.UTF8);
            var json = await reader.ReadToEndAsync();
            Assert.DoesNotContain(privateContent, json, StringComparison.Ordinal);
            using var manifest = JsonDocument.Parse(json);
            var exported = Assert.Single(manifest.RootElement.GetProperty("approvals").EnumerateArray()).GetProperty("argumentsJson").GetString();
            Assert.Equal(audit, exported);
            Assert.Contains("prompt-curator-approval-redacted-v1", json, StringComparison.Ordinal);
        } finally {
            if (Directory.Exists(workspaceRoot)) {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExportAsync_admitted_history_excludes_private_journal_and_runtime_state() {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"admitted-package-export-{Guid.NewGuid():N}");
        var agent = CreateAgent();
        var run = AgentPackageAdmittedRunFixture.Create(agent.Id);
        var document = SandboxWorkspaceDocument.Empty with { Agents = [agent], ExecutionRuns = [run] };
        try {
            var service = new ZipAgentPackageService(workspaceRoot);
            var result = await service.ExportAsync(document, agent);
            using var archive = ZipFile.OpenRead(result.PackagePath);
            using var reader = new StreamReader(archive.GetEntry("manifest.json")!.Open(), Encoding.UTF8);
            var json = await reader.ReadToEndAsync();
            Assert.DoesNotContain(AgentPackageAdmittedRunFixture.PrivateContent, json, StringComparison.Ordinal);
            using var manifest = JsonDocument.Parse(json);
            var exported = Assert.Single(manifest.RootElement.GetProperty("runs").EnumerateArray());
            Assert.Equal(run.Id, exported.GetProperty("id").GetGuid());
            Assert.Equal(run.ResultSummary, exported.GetProperty("resultSummary").GetString());
            Assert.False(exported.TryGetProperty("toolAdmission", out _));
            Assert.Equal(string.Empty, exported.GetProperty("runtimeSessionKey").GetString());
            Assert.Equal(JsonValueKind.Null, exported.GetProperty("serializedSessionStateJson").ValueKind);
            Assert.NotNull(run.ToolAdmission);
            Assert.Equal(AgentPackageAdmittedRunFixture.PrivateContent, run.ToolAdmission.OriginalInput!.Content);
        } finally {
            if (Directory.Exists(workspaceRoot)) {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ImportAsync_portable_history_drops_foreign_admission_and_provider_checkpoint() {
        var agent = CreateAgent();
        var run = AgentPackageAdmittedRunFixture.Create(agent.Id);
        await using var package = new MemoryStream(CreatePackage(agent, runs: [run]));
        var result = await new ZipAgentPackageService(Path.GetTempPath()).ImportAsync(package, new AgentPackageReadOptions());

        var imported = Assert.Single(result.Runs);
        Assert.Equal(run.Id, imported.Id);
        Assert.Equal(run.ResultSummary, imported.ResultSummary);
        Assert.Null(imported.ToolAdmission);
        Assert.Equal(string.Empty, imported.RuntimeSessionKey);
        Assert.Null(imported.SerializedSessionStateJson);
        Assert.DoesNotContain(AgentPackageAdmittedRunFixture.PrivateContent,
            JsonSerializer.Serialize(imported), StringComparison.Ordinal);
    }

    private static byte[] CreatePackage(
        AgentDefinition agent,
        string schemaVersion = "1.0",
        string? rawSecret = null,
        IReadOnlyList<string>? additionalEntries = null,
        IReadOnlyList<ExecutionRunRecord>? runs = null)
    {
        var manifest = new Dictionary<string, object?>
        {
            ["schemaVersion"] = schemaVersion,
            ["agent"] = agent,
            ["sessions"] = Array.Empty<object>(),
            ["executionLog"] = Array.Empty<object>(),
            ["metrics"] = Array.Empty<object>(),
            ["memory"] = Array.Empty<object>(),
            ["providers"] = Array.Empty<object>(),
            ["capabilities"] = Array.Empty<object>(),
            ["runs"] = runs ?? [],
            ["approvals"] = Array.Empty<object>(),
            ["artifacts"] = Array.Empty<object>(),
            ["checkpoints"] = Array.Empty<object>(),
            ["toolReceipts"] = Array.Empty<object>()
        };
        if (rawSecret is not null)
        {
            manifest["apiKey"] = rawSecret;
        }

        using var package = new MemoryStream();
        using (var archive = new ZipArchive(package, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(
                archive,
                "manifest.json",
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            foreach (var entryName in additionalEntries ?? [])
            {
                WriteEntry(archive, entryName, "untrusted");
            }
        }

        return package.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }

    private static AgentDefinition CreateAgent()
    {
        return new AgentDefinition(
            Guid.Parse("39f66f69-e0ee-458e-b85a-95ed1c316bf5"),
            "Remote package verifier",
            "Verification specialist",
            "Validates portable agent-package behavior.",
            "Verify the imported package.",
            AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            Model: "gpt-test",
            AgentWorkloadKind.Programming,
            AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: """{"responseMode":"concise"}""",
            IsTemplate: false,
            TemplateKey: "remote-package-verifier",
            AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: ["package-import", "verification"],
            CreatedAtUtc: FixedTimestamp,
            UpdatedAtUtc: FixedTimestamp);
    }

    private static ExecutionRunRecord CreateExecutionRun(Guid agentId, string metadataJson)
    {
        return new ExecutionRunRecord(
            Guid.NewGuid(),
            agentId,
            null,
            "External target export",
            "chat-session",
            Guid.NewGuid().ToString("N"),
            string.Empty,
            string.Empty,
            "test",
            "interactive",
            metadataJson,
            string.Empty,
            string.Empty,
            "test-provider",
            "test-model",
            ExecutionState.Idle,
            null,
            FixedTimestamp,
            FixedTimestamp,
            null,
            null,
            string.Empty,
            null,
            []);
    }
}
