using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

using static CanDoItAll.Tests.Playwright.Smoke.AgentUiJourneySupport;

namespace CanDoItAll.Tests.Playwright.Smoke;

internal sealed class UiEvidence(string scenario) {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;
    private readonly List<object> runs = [];
    private object? provider;

    internal Dictionary<string, object> Targets { get; } = new(StringComparer.Ordinal);

    internal Dictionary<string, object> Observations { get; } = new(StringComparer.Ordinal);

    internal OwnerCounts? CountsBefore { get; set; }

    internal OwnerCounts? CountsAfter { get; set; }

    internal bool Passed { get; set; }

    // What this run actually did. A runner outcome of passed says nothing about it: the gate can be closed, or
    // the journey can be a rehearsal that stops before its first Send.
    internal string Execution { get; set; } = "live";

    internal int ModelRequests { get; private set; }

    internal async Task DescribeProviderAsync(LiveUiHost host, AgentDefinition agent) {
        await host.BoundProviderAsync(agent.ProviderProfileId!.Value);
        provider = await host.SeedAsync(async services => {
            var profile = (await services.GetRequiredService<IAgentFrameworkWorkspaceService>().ListProvidersAsync())
                .Single(item => item.Id == agent.ProviderProfileId);
            var model = ManagedSeedProviderFallbacks.ResolveModel(agent, profile);
            Assert.Equal(ProviderKind.OpenAi, profile.Kind);
            Assert.Equal(ManagedSeedProviderFallbacks.OpenAiDefaultModel, model);
            return (object)new { kind = profile.Kind, name = profile.Name, transport = profile.Transport, model, agentId = agent.Id, agentName = agent.Name };
        });
    }

    internal void RecordRun(string label, ExecutionRunDetail detail)
        => runs.Add(new {
            label,
            executionRunId = detail.Run.Id,
            chatSessionId = detail.Run.ChatSessionId,
            state = detail.Run.State,
            outcome = detail.Run.Outcome,
            providerName = detail.Run.ProviderName,
            model = detail.Run.Model,
            sourceKind = detail.Run.SourceKind,
            toolAdmissionBatches = detail.Run.ToolAdmission?.Batches.Length ?? 0,
            usageObservations = detail.UsageObservations.Count,
            resultSummary = Sanitize(detail.Run.ResultSummary),
            executionLog = detail.ExecutionLog.OrderBy(item => item.CreatedAtUtc).TakeLast(12).Select(item => new {
                phase = item.Phase,
                state = item.State,
                message = Sanitize(item.Message)
            }),
            createdAtUtc = detail.Run.CreatedAtUtc,
            completedAtUtc = detail.Run.CompletedAtUtc,
            proposals = Proposals(detail).Select(item => new {
                proposalId = item.IntentId.Value,
                toolName = item.Payload.ToolName,
                effect = item.Payload.Effect,
                requiresApproval = item.RequiresApproval,
                state = item.State,
                approvalStatus = item.ApprovalStatus,
                approvalId = item.ApprovalId,
                effectState = item.EffectState
            }),
            approvals = detail.Approvals.Select(item => new {
                approvalId = item.ApprovalId,
                toolName = item.ToolName,
                decision = item.Status,
                requestedAtUtc = item.RequestedAtUtc,
                decidedAtUtc = item.DecidedAtUtc
            }),
            toolInvocations = detail.ToolReceipts.OrderBy(item => item.StartedAtUtc).Select(item => new {
                toolName = item.ToolName,
                providerKey = item.RuntimeToolProviderKey,
                riskClass = item.RiskClass,
                outcome = item.InvocationOutcome,
                effectState = item.EffectState,
                effectSourceKind = item.EffectSourceKind,
                effectSourceId = item.EffectSourceId,
                failureCode = item.FailureCode,
                completedAtUtc = item.CompletedAtUtc
            })
        });

    // A failed journey still leaves the persisted run of the watched agent behind, sanitized.
    internal async Task RecordLatestRunUnlessPassedAsync(LiveUiHost host) {
        if (Passed || host.WatchedAgentId is not { } agentId) {
            return;
        }

        try {
            RecordRun("latest-run-after-failure", await host.SeedAsync(services => ReadLatestRunAsync(services, agentId)));
        } catch (Exception exception) {
            Observations["failureRunRead"] = exception.GetType().Name;
        }
    }

    // The gate was closed, so nothing was started. The manifest still records that this scenario did not run,
    // because the runner reports the skipped journey as a pass.
    internal static async Task WriteNotRunAsync(string scenario, string reason) {
        var evidence = new UiEvidence(scenario) { Execution = "not-run" };
        evidence.Observations["notRunReason"] = reason;
        var directory = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "live-agent-smoke",
            $"{evidence.startedAtUtc:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "evidence.json"), JsonSerializer.Serialize(new {
            scenario,
            execution = evidence.Execution,
            passed = false,
            startedAtUtc = evidence.startedAtUtc,
            completedAtUtc = DateTimeOffset.UtcNow,
            modelRequests = new { used = 0, bound = MaximumModelRequestsPerExecution },
            observations = evidence.Observations
        }, Json));
    }

    internal async Task WriteAsync(LiveUiHost host) {
        var proofPassed = Passed;
        ModelRequests = await host.CountModelRequestsAsync();
        var journalRead = false;
        try {
            Observations["providerJournal"] = await host.ReadProviderJournalAsync();
            journalRead = true;
        } catch (Exception exception) {
            Observations["providerJournalReadFailure"] = exception.GetType().Name;
        }
        Passed = proofPassed && journalRead && (Execution != "live" || host.ProviderJournalRequests > 0 && ModelRequests > 0);
        try {
            await host.CaptureHostLogAsync();
            await File.WriteAllTextAsync(host.Artifact("evidence.json"), JsonSerializer.Serialize(new {
                scenario, execution = Execution, passed = Passed, startedAtUtc, completedAtUtc = DateTimeOffset.UtcNow,
                provider,
                requestReservations = new { count = ModelRequests, bound = MaximumModelRequestsPerExecution },
                watchdog = new { host.StopReason, host.ToolAdmissionBatches, host.WatchdogReadFailures, host.WatchdogReadFailureType },
                targets = Targets, countsBefore = CountsBefore, countsAfter = CountsAfter, observations = Observations, runs
            }, Json));
        } catch (Exception exception) when (!proofPassed) {
            Console.Error.WriteLine($"Failure evidence could not be finalized: {exception.GetType().Name}. Attempt={host.ArtifactDirectory}");
        }
        if (proofPassed) {
            Assert.True(Passed, "Passed proof requires complete evidence and actual provider journal requests for live claims.");
        }
    }

}
