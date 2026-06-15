using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessBrowserProofValidatorTests
{
    [Fact]
    public void Validate_accepts_current_run_browser_proof_record()
    {
        var fixture = CreateFixture();

        var result = ProcessBrowserProofValidator.Validate(fixture.Record, fixture.Context);

        Assert.True(result.IsValid, result.Diagnostic);
    }

    [Fact]
    public void Validate_rejects_stale_browser_proof_record_from_before_execution_start()
    {
        var fixture = CreateFixture(record => record with
        {
            CapturedAtUtc = DateTimeOffset.Parse("2026-06-02T12:29:59Z")
        });

        var result = ProcessBrowserProofValidator.Validate(fixture.Record, fixture.Context);

        Assert.False(result.IsValid);
        Assert.Contains("before the current execution run started", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_rejects_copied_browser_output_not_produced_by_current_execution()
    {
        var fixture = CreateFixture(record => record with
        {
            ToolOutputs =
            [
                new BrowserProofToolOutput(ToolContractCatalog.BrowserTakeScreenshot, ".playwright-mcp/copied-page.png"),
                new BrowserProofToolOutput(ToolContractCatalog.BrowserSnapshot, ".playwright-mcp/page.yml"),
                new BrowserProofToolOutput(ToolContractCatalog.BrowserConsoleMessages, ".playwright-mcp/console.log")
            ]
        });

        var result = ProcessBrowserProofValidator.Validate(fixture.Record, fixture.Context);

        Assert.False(result.IsValid);
        Assert.Contains("was not produced by the current execution", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_rejects_wrong_runtime_host()
    {
        var fixture = CreateFixture(record => record with
        {
            RuntimeHost = record.RuntimeHost with
            {
                HostUrl = "http://127.0.0.1:61235"
            }
        });

        var result = ProcessBrowserProofValidator.Validate(fixture.Record, fixture.Context);

        Assert.False(result.IsValid);
        Assert.Contains("runtime host URL", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_rejects_wrong_database_profile()
    {
        var fixture = CreateFixture(record => record with
        {
            RuntimeHost = record.RuntimeHost with
            {
                DatabaseProfileFingerprint = "wrong-profile"
            }
        });

        var result = ProcessBrowserProofValidator.Validate(fixture.Record, fixture.Context);

        Assert.False(result.IsValid);
        Assert.Contains("database profile fingerprint", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_rejects_missing_cleanup_receipt_for_kept_alive_runtime_host()
    {
        var fixture = CreateFixture(record => record with
        {
            CleanupReceipt = new RuntimeCleanupReceiptRecord(
                string.Empty,
                CleanupAttempted: false,
                CleanupProcessIds: [],
                CleanupCompletedAtUtc: null)
        });

        var result = ProcessBrowserProofValidator.Validate(fixture.Record, fixture.Context);

        Assert.False(result.IsValid);
        Assert.Contains("cleanup receipt", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_rejects_evidence_path_from_another_process_run()
    {
        var otherRunId = Guid.NewGuid();
        var fixture = CreateFixture(record => record with
        {
            EvidenceArtifactPaths =
            [
                $"artifacts/process-runs/{otherRunId:D}/browser/page.png"
            ]
        });

        var result = ProcessBrowserProofValidator.Validate(fixture.Record, fixture.Context);

        Assert.False(result.IsValid);
        Assert.Contains("current process run", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    private static BrowserProofFixture CreateFixture(Func<ProcessBrowserProofRecord, ProcessBrowserProofRecord>? mutate = null)
    {
        var processRunId = Guid.NewGuid();
        var processStepRunId = Guid.NewGuid();
        var executionRunId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var startedAtUtc = DateTimeOffset.Parse("2026-06-02T12:30:00Z");
        var record = new ProcessBrowserProofRecord(
            ProcessBrowserProofValidator.SchemaVersion,
            processRunId,
            processStepRunId,
            executionRunId,
            projectId,
            new RuntimeHostIdentityRecord(
                HostUrl: "http://127.0.0.1:61234",
                Route: "/",
                DatabaseProfileId: "profile-001",
                DatabaseProfileFingerprint: "fingerprint-001",
                StartupReceiptPath: $"artifacts/process-runs/{processRunId:D}/runtime/startup.json",
                KeepAlive: true),
            new BrowserProofViewport(1280, 720),
            [
                new BrowserProofToolOutput(ToolContractCatalog.BrowserTakeScreenshot, ".playwright-mcp/page.png"),
                new BrowserProofToolOutput(ToolContractCatalog.BrowserSnapshot, ".playwright-mcp/page.yml"),
                new BrowserProofToolOutput(ToolContractCatalog.BrowserConsoleMessages, ".playwright-mcp/console.log"),
                new BrowserProofToolOutput(ToolContractCatalog.BrowserPressKey, ".playwright-mcp/interaction.txt")
            ],
            [
                $"artifacts/process-runs/{processRunId:D}/browser/page.png",
                $"artifacts/process-runs/{processRunId:D}/browser/page.yml",
                $"artifacts/process-runs/{processRunId:D}/browser/console.log"
            ],
            [ToolContractCatalog.BrowserPressKey],
            new RuntimeCleanupReceiptRecord(
                CleanupReceiptPath: $"artifacts/process-runs/{processRunId:D}/runtime/startup.json",
                CleanupAttempted: true,
                CleanupProcessIds: [1234, 1235],
                CleanupCompletedAtUtc: startedAtUtc.AddMinutes(2)),
            startedAtUtc.AddMinutes(1));

        record = mutate?.Invoke(record) ?? record;

        var context = new ProcessBrowserProofValidationContext(
            processRunId,
            processStepRunId,
            executionRunId,
            projectId,
            startedAtUtc,
            RuntimeHostUrl: "http://127.0.0.1:61234",
            DatabaseProfileId: "profile-001",
            DatabaseProfileFingerprint: "fingerprint-001",
            SuccessfulBrowserOutputPaths: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".playwright-mcp/page.png",
                ".playwright-mcp/page.yml",
                ".playwright-mcp/console.log",
                ".playwright-mcp/interaction.txt"
            },
            SuccessfulBrowserToolNames: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ToolContractCatalog.BrowserTakeScreenshot,
                ToolContractCatalog.BrowserSnapshot,
                ToolContractCatalog.BrowserConsoleMessages,
                ToolContractCatalog.BrowserPressKey
            },
            RequiresRepresentativeInteraction: true,
            RequiresCleanupReceipt: true);

        return new BrowserProofFixture(record, context);
    }

    private sealed record BrowserProofFixture(
        ProcessBrowserProofRecord Record,
        ProcessBrowserProofValidationContext Context);
}
