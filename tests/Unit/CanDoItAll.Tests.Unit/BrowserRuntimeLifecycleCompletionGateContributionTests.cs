using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class BrowserRuntimeLifecycleCompletionGateContributionTests
{
    public enum StaticProofFault { None, MissingStartup, DifferentCleanup, DifferentBrowserHost }

    [Theory]
    [InlineData(StaticProofFault.None)]
    [InlineData(StaticProofFault.MissingStartup)]
    [InlineData(StaticProofFault.DifferentCleanup)]
    [InlineData(StaticProofFault.DifferentBrowserHost)]
    public void Validate_uses_latest_managed_static_host_and_requires_its_own_correlated_proof(StaticProofFault fault) {
        var context = CreateContext(Guid.NewGuid(), "http://127.0.0.1:5173", "http://127.0.0.1:5173");
        var receipts = context.ToolReceipts!.Select(receipt => receipt with {
            ToolName = receipt.ToolName switch {
                "workspace_sample_run" => ToolContractCatalog.WorkspaceDotNetRun,
                "workspace_sample_stop" => ToolContractCatalog.WorkspaceDotNetStop,
                _ => receipt.ToolName
            }
        }).ToArray();
        var start = receipts.Max(receipt => receipt.CompletedAtUtc).AddSeconds(1);
        const string startup = "startupReceipt=artifacts/process-runs/test/tool-runs/static/startup.json";
        const string host = "hostUrl=http://127.0.0.1:5174";
        var issue = new BrowserRuntimeLifecycleCompletionGateContribution().Validate(context with {
            Assignment = context.Assignment with {
                LaunchVariables = new Dictionary<string, string> {
                    [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts] = JsonSerializer.Serialize(new[] {
                        ToolContractCatalog.WorkspaceDotNetRun, ToolContractCatalog.WorkspaceDotNetStop, "browser_navigate"
                    })
                }
            },
            ToolReceipts = [.. receipts,
                receipts[0] with { ToolName = ToolContractCatalog.WorkspaceStaticServe,
                    RequestSummary = fault == StaticProofFault.MissingStartup ? host : $"{startup}; {host}",
                    StartedAtUtc = start, CompletedAtUtc = start.AddSeconds(1) },
                receipts[1] with { RequestSummary = fault == StaticProofFault.DifferentBrowserHost ? "url=http://127.0.0.1:5173" : host,
                    StartedAtUtc = start.AddSeconds(2), CompletedAtUtc = start.AddSeconds(3) },
                receipts[3] with { RequestSummary = fault == StaticProofFault.DifferentCleanup ? receipts[3].RequestSummary : startup,
                    StartedAtUtc = start.AddSeconds(4), CompletedAtUtc = start.AddSeconds(5) }
            ]
        });

        if (fault == StaticProofFault.None) {
            Assert.Null(issue);
        } else {
            Assert.NotNull(issue);
            Assert.Equal("process.adapter.runtime_lifecycle_correlation_missing", issue.Code);
        }
    }

    [Fact]
    public void Static_host_requirement_enables_lifecycle_validation() {
        Assert.True(BrowserRuntimeLifecycleCompletionGateContribution.TryResolveBrowserRuntimeLifecycleToolNames(
            new HashSet<string> { ToolContractCatalog.WorkspaceStaticServe, ToolContractCatalog.WorkspaceDotNetStop, "browser_navigate" }, out var names));
        Assert.Equal(ToolContractCatalog.WorkspaceStaticServe, names.RunToolName);
    }

    [Fact]
    public void Validate_accepts_current_execution_browser_proof_for_the_started_host()
    {
        var executionRunId = Guid.NewGuid();
        var contribution = new BrowserRuntimeLifecycleCompletionGateContribution();

        var issue = contribution.Validate(CreateContext(
            executionRunId,
            runHost: "http://127.0.0.1:5173",
            browserHost: "http://127.0.0.1:5173"));

        Assert.Null(issue);
        Assert.Equal(ProcessCompletionGateContributionStage.BeforeToolReceiptEvidence, contribution.Stage);
    }

    [Fact]
    public void Validate_rejects_browser_proof_for_a_different_host()
    {
        var executionRunId = Guid.NewGuid();
        var contribution = new BrowserRuntimeLifecycleCompletionGateContribution();

        var issue = contribution.Validate(CreateContext(
            executionRunId,
            runHost: "http://127.0.0.1:5173",
            browserHost: "http://127.0.0.1:5174"));

        Assert.NotNull(issue);
        Assert.Equal("process.adapter.runtime_lifecycle_correlation_missing", issue.Code);
        Assert.Contains("not correlated", issue.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("runtime-provider")]
    [InlineData("agent-tool-trace")]
    public void Validate_uses_command_lifecycle_receipts_when_later_invocation_traces_exist(string traceFamily) {
        var executionRunId = Guid.NewGuid();
        var context = CreateContext(executionRunId, "http://127.0.0.1:5173", "http://127.0.0.1:5173");
        var receipts = context.ToolReceipts!;
        var traceCompletedAt = receipts.Max(receipt => receipt.CompletedAtUtc).AddSeconds(1);
        var contribution = new BrowserRuntimeLifecycleCompletionGateContribution();

        var issue = contribution.Validate(context with {
            ToolReceipts = [
                .. receipts,
                receipts[0] with {
                    ToolFamily = traceFamily,
                    RequestSummary = "workspace_sample_run|targetPath=src/App/App.csproj",
                    CompletedAtUtc = traceCompletedAt
                },
                receipts[3] with {
                    ToolFamily = traceFamily,
                    RequestSummary = "workspace_sample_stop|startupReceiptPath=artifacts/process-runs/test/tool-runs/runtime/startup.json",
                    CompletedAtUtc = traceCompletedAt
                }
            ]
        });

        Assert.Null(issue);
    }

    [Fact]
    public void Validate_rejects_invocation_traces_without_command_lifecycle_receipts() {
        var executionRunId = Guid.NewGuid();
        var context = CreateContext(executionRunId, "http://127.0.0.1:5173", "http://127.0.0.1:5173");
        var contribution = new BrowserRuntimeLifecycleCompletionGateContribution();

        var issue = contribution.Validate(context with {
            ToolReceipts = context.ToolReceipts!
                .Select(receipt => receipt with { ToolFamily = "runtime-provider" })
                .ToArray()
        });

        Assert.NotNull(issue);
        Assert.Contains("current execution-run host lifecycle", issue.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_rejects_latest_command_lifecycle_mismatch_even_when_earlier_pair_matches() {
        var executionRunId = Guid.NewGuid();
        var context = CreateContext(executionRunId, "http://127.0.0.1:5173", "http://127.0.0.1:5173");
        var receipts = context.ToolReceipts!;
        var contribution = new BrowserRuntimeLifecycleCompletionGateContribution();

        var issue = contribution.Validate(context with {
            ToolReceipts = [
                .. receipts,
                receipts[0] with {
                    RequestSummary = "startupReceipt=artifacts/process-runs/test/tool-runs/different/startup.json; hostUrl=http://127.0.0.1:5173",
                    CompletedAtUtc = receipts.Max(receipt => receipt.CompletedAtUtc).AddSeconds(1)
                }
            ]
        });

        Assert.NotNull(issue);
        Assert.Contains("same startup.json receipt", issue.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_accepts_matching_cleanup_before_a_different_hosts_later_stop() {
        var context = CreateContext(Guid.NewGuid(), "http://127.0.0.1:5173", "http://127.0.0.1:5173");
        var receipts = context.ToolReceipts!;
        var later = receipts.Max(receipt => receipt.CompletedAtUtc).AddSeconds(1);

        var issue = new BrowserRuntimeLifecycleCompletionGateContribution().Validate(context with {
            ToolReceipts = [.. receipts, receipts[3] with {
                RequestSummary = "startupReceipt=artifacts/process-runs/test/tool-runs/static-host/startup.json; hostUrl=http://127.0.0.1:5174",
                StartedAtUtc = later,
                CompletedAtUtc = later.AddSeconds(1)
            }]
        });

        Assert.Null(issue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Validate_correlates_latest_host_when_hosts_stop_in_either_order(bool newestStopsLast) {
        var context = CreateContext(Guid.NewGuid(), "http://127.0.0.1:5173", "http://127.0.0.1:5173");
        var receipts = context.ToolReceipts!;
        var start = receipts.Max(receipt => receipt.CompletedAtUtc).AddSeconds(1);
        var secondHost = "startupReceipt=artifacts/process-runs/test/tool-runs/second/startup.json; hostUrl=http://127.0.0.1:5174";
        var issue = new BrowserRuntimeLifecycleCompletionGateContribution().Validate(context with {
            ToolReceipts = [
                .. receipts.Take(3),
                receipts[0] with { RequestSummary = secondHost, StartedAtUtc = start, CompletedAtUtc = start.AddSeconds(1) },
                receipts[1] with { RequestSummary = "url=http://127.0.0.1:5174", StartedAtUtc = start.AddSeconds(2), CompletedAtUtc = start.AddSeconds(3) },
                receipts[3] with { RequestSummary = secondHost, StartedAtUtc = start.AddSeconds(newestStopsLast ? 6 : 4), CompletedAtUtc = start.AddSeconds(newestStopsLast ? 7 : 5) },
                receipts[3] with { StartedAtUtc = start.AddSeconds(newestStopsLast ? 4 : 6), CompletedAtUtc = start.AddSeconds(newestStopsLast ? 5 : 7) }
            ]
        });

        Assert.Null(issue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Validate_rejects_browser_proof_outside_the_matching_host_lifetime(bool afterStop) {
        var context = CreateContext(Guid.NewGuid(), "http://127.0.0.1:5173", "http://127.0.0.1:5173");
        var receipts = context.ToolReceipts!;
        var timestamp = afterStop ? receipts[3].CompletedAtUtc.AddSeconds(1) : receipts[0].StartedAtUtc.AddSeconds(-1);

        var issue = new BrowserRuntimeLifecycleCompletionGateContribution().Validate(context with {
            ToolReceipts = receipts.Select(receipt => receipt.ToolName.StartsWith("browser_", StringComparison.Ordinal)
                ? receipt with { StartedAtUtc = timestamp, CompletedAtUtc = timestamp }
                : receipt).ToArray()
        });

        Assert.NotNull(issue);
        Assert.Contains("Browser proof is not correlated", issue.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_rejects_matching_cleanup_from_another_execution() {
        var context = CreateContext(Guid.NewGuid(), "http://127.0.0.1:5173", "http://127.0.0.1:5173");
        var receipts = context.ToolReceipts!;
        var issue = new BrowserRuntimeLifecycleCompletionGateContribution().Validate(context with {
            ToolReceipts = [.. receipts.Take(3), receipts[3] with { ExecutionRunId = Guid.NewGuid() }]
        });

        Assert.NotNull(issue);
        Assert.Contains("current execution-run host lifecycle", issue.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_rejects_matching_cleanup_before_the_host_started() {
        var context = CreateContext(Guid.NewGuid(), "http://127.0.0.1:5173", "http://127.0.0.1:5173");
        var receipts = context.ToolReceipts!;
        var timestamp = receipts[0].StartedAtUtc.AddSeconds(-1);
        var issue = new BrowserRuntimeLifecycleCompletionGateContribution().Validate(context with {
            ToolReceipts = [.. receipts.Take(3), receipts[3] with { StartedAtUtc = timestamp, CompletedAtUtc = timestamp }]
        });

        Assert.NotNull(issue);
        Assert.Contains("same startup.json receipt", issue.Summary, StringComparison.Ordinal);
    }

    private static ProcessCompletionGateContext CreateContext(
        Guid executionRunId,
        string runHost,
        string browserHost)
    {
        var startupReceipt = "artifacts/process-runs/test/tool-runs/runtime/startup.json";
        var assignment = new ProcessRuntimeStepAssignment(
            ProcessRunId.New(),
            ProcessInstancePlanId.New(),
            ProcessStepInstanceId.New(),
            "runtime-proof",
            "qa",
            "qa",
            "QA",
            ProcessLaunchExecutorKinds.Agent,
            "agent",
            "Agent",
            "Capture runtime proof.",
            "sha256:readiness",
            "Test assignment.",
            [ArtifactSlotId.New()],
            [],
            [],
            ProcessOperationContractNames.ExternalProductTargetReadOnly,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts] = JsonSerializer.Serialize(new[]
                {
                    "workspace_sample_run",
                    "browser_navigate",
                    "browser_snapshot",
                    "workspace_sample_stop"
                })
            },
            BranchGate: null,
            DateTimeOffset.UtcNow);
        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            BranchOutcomeKey = "accepted",
            Reason = "Runtime proof accepted."
        };

        return new ProcessCompletionGateContext(
            assignment,
            output,
            [
                CreateReceipt("workspace_sample_run", $"startupReceipt={startupReceipt}; hostUrl={runHost}", executionRunId),
                CreateReceipt("browser_navigate", $"url={browserHost}", executionRunId),
                CreateReceipt("browser_snapshot", $"url={browserHost}", executionRunId),
                CreateReceipt("workspace_sample_stop", $"startupReceipt={startupReceipt}; hostUrl={runHost}", executionRunId)
            ],
            executionRunId);
    }

    private static ToolExecutionReceiptRecord CreateReceipt(
        string toolName,
        string requestSummary,
        Guid executionRunId)
        => new(
            Guid.NewGuid(),
            executionRunId,
            "test",
            toolName,
            "ReadOnlyWorkspace",
            "NotRequired",
            "Test receipt.",
            requestSummary,
            ".",
            "Succeeded (exit 0)",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
}
