using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Integration;

public sealed class AgentRecoveryModelsTests
{
    [Fact]
    public void FormatRepair_uses_in_place_repair_without_new_agent_execution()
    {
        var decision = AgentRecoveryDecisionFactory.FormatRepair(
            "Structured output JSON was malformed.",
            "execution-001");
        var context = AgentRecoveryContextBuilder.Build(
            Guid.NewGuid(),
            Guid.NewGuid(),
            decision,
            packet: null);

        Assert.Equal(AgentRecoveryMode.FormatRepair, decision.Mode);
        Assert.Equal(AgentFailureCategory.StructuredOutputInvalid, decision.FailureCategory);
        Assert.False(AgentRecoveryDecisionFactory.RequiresNewAgentExecution(decision.Mode));
        Assert.Equal(AgentRecoverySessionStrategy.None, context.SessionStrategy);
    }

    [Fact]
    public void ProviderFailure_uses_fresh_provider_fallback_session()
    {
        var decision = AgentRecoveryDecisionFactory.Create(
            AgentFailureCategory.ProviderFailure,
            "Primary provider returned a transient failure.",
            attemptNumber: 2,
            sourceExecutionRunId: "execution-002");
        var context = AgentRecoveryContextBuilder.Build(
            Guid.NewGuid(),
            Guid.NewGuid(),
            decision,
            packet: null);

        Assert.Equal(AgentRecoveryMode.ProviderFallbackRetry, decision.Mode);
        Assert.True(AgentRecoveryDecisionFactory.RequiresNewAgentExecution(decision.Mode));
        Assert.Equal(AgentRecoverySessionStrategy.FreshSession, context.SessionStrategy);
    }

    [Fact]
    public void ApprovalContinuation_uses_same_compatible_session()
    {
        var decision = AgentRecoveryDecisionFactory.ApprovalContinuation(
            "Continue after approved mutation.",
            attemptNumber: 1,
            sourceExecutionRunId: "execution-003");
        var context = AgentRecoveryContextBuilder.Build(
            Guid.NewGuid(),
            Guid.NewGuid(),
            decision,
            packet: null);

        Assert.Equal(AgentRecoveryMode.ApprovalContinuation, decision.Mode);
        Assert.Equal(AgentRecoverySessionStrategy.SameCompatibleSession, context.SessionStrategy);
    }

    [Fact]
    public void QaRejectionPacket_preserves_target_step_artifact_and_rework_finding()
    {
        var processRunId = Guid.NewGuid();
        var implementationStepRunId = Guid.NewGuid();
        var qaStepRunId = Guid.NewGuid();

        var packet = AgentReworkPacketFactory.CreateQaRejectionPacket(
            processRunId,
            implementationStepRunId,
            qaStepRunId,
            "Calculator keypad does not update the visible display.",
            "external-target/C/app/Calculator/Components/Pages/Home.razor",
            sourceExecutionRunId: "execution-004");
        var summary = AgentReworkPromptRenderer.RenderPacketSummary(packet);

        Assert.Equal(processRunId, packet.ProcessRunId);
        Assert.Equal(implementationStepRunId, packet.StepRunId);
        Assert.Equal(qaStepRunId, packet.SourceQaStepRunId);
        Assert.Equal(AgentFailureCategory.QaRejected, packet.FailureCategory);
        Assert.Contains(packet.Findings, item => item.Source == "QA");
        Assert.Contains(packet.ArtifactsToInspect, item => item.Path.EndsWith("Home.razor", StringComparison.Ordinal));
        Assert.Contains("smallest change", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(packet.Id.ToString("D"), summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ManualRerunPacket_includes_operator_directive_and_minimal_delta_instruction()
    {
        var packet = AgentReworkPacketFactory.CreateManualRerunPacket(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Implement calculator UI",
            "Keep the current host and repair only the broken divide-by-zero behavior.",
            [
                new AgentReworkArtifactRef(
                    "Calculator page",
                    "Calculator/Components/Pages/Home.razor",
                    "Verify before editing.")
            ]);
        var directive = AgentReworkPromptRenderer.RenderRecoveryDirective(
            new AgentRecoveryDecision(
                AgentRecoveryMode.ReworkContinuation,
                AgentFailureCategory.HumanRequestedRerun,
                "Manual rerun.",
                AttemptNumber: 1,
                SourceExecutionRunId: null,
                packet.Id),
            packet,
            legacyDirective: "Legacy manual rerun details.");

        Assert.Equal(AgentRecoveryMode.ReworkContinuation, packet.RecoveryMode);
        Assert.Equal(AgentFailureCategory.HumanRequestedRerun, packet.FailureCategory);
        Assert.Equal("Keep the current host and repair only the broken divide-by-zero behavior.", packet.HumanDirective);
        Assert.Contains("Do not regenerate the entire application", directive, StringComparison.Ordinal);
        Assert.Contains("Legacy manual rerun details.", directive, StringComparison.Ordinal);
    }

    [Fact]
    public void ProofFingerprint_reuses_successful_receipt_when_relevant_inputs_match()
    {
        var sourceHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Calculator/Domain/CalculatorEngine.cs"] = "hash-a"
        };
        var artifactHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["artifacts/build.log"] = "hash-b"
        };
        var finishedAtUtc = DateTimeOffset.UtcNow;
        var receipt = AgentProofFingerprintService.CreateReceipt(
            "workspace_dotnet_test",
            "dotnet test Calculator.Tests.csproj",
            "external-target/C/app",
            sourceHashes,
            artifactHashes,
            "windows;net10",
            "10.0.100",
            AgentProofStatus.Succeeded,
            finishedAtUtc.AddSeconds(-30),
            finishedAtUtc,
            "Tests passed.");

        var decision = AgentProofFingerprintService.EvaluateReuse(
            receipt,
            sourceHashes,
            artifactHashes,
            finishedAtUtc.AddMinutes(5),
            TimeSpan.FromHours(1));

        Assert.True(decision.CanReuse);
        Assert.NotNull(decision.ReusableProof);
        Assert.Equal(receipt.Fingerprint.Hash, decision.ReusableProof.FingerprintHash);
    }

    [Theory]
    [InlineData("Calculator/Domain/CalculatorEngine.cs")]
    [InlineData("Calculator/Calculator.csproj")]
    [InlineData("Calculator.slnx")]
    public void BuildAndTestProofs_are_invalidated_by_code_or_project_files(string path)
    {
        Assert.True(AgentProofFingerprintService.InvalidatesBuildOrTestProof(path));
    }

    [Theory]
    [InlineData("Calculator/Components/Pages/Home.razor")]
    [InlineData("Calculator/wwwroot/app.css")]
    [InlineData("Calculator/wwwroot/site.js")]
    public void BrowserProofs_are_invalidated_by_ui_or_static_asset_files(string path)
    {
        Assert.True(AgentProofFingerprintService.InvalidatesBrowserProof(path));
    }

    [Fact]
    public void RecoveryLedger_escalates_after_identical_failure_budget_is_exhausted()
    {
        var processRunId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var nextDecision = AgentRecoveryDecisionFactory.Create(
            AgentFailureCategory.FinalizerMissing,
            "The finalizer was not called.",
            attemptNumber: 4,
            sourceExecutionRunId: "execution-005");
        var ledger = Enumerable.Range(1, 3)
            .Select(index => AgentRecoveryLedger.CreateEntry(
                processRunId,
                stepRunId,
                nextDecision with
                {
                    AttemptNumber = index
                },
                "OpenAI",
                "gpt-5.4",
                providerFallbackCount: 0,
                recordedAtUtc: now.AddMinutes(-index)))
            .ToList();

        var loopDecision = AgentRecoveryLedger.EvaluateLoopControl(
            ledger,
            nextDecision,
            maxIdenticalFailures: 3,
            maxProviderFallbacks: 2,
            now);

        Assert.True(loopDecision.ShouldEscalate);
        Assert.Contains("Failure signature repeated", loopDecision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void RecoveryLedger_enforces_provider_fallback_budget_separately()
    {
        var processRunId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var providerDecision = AgentRecoveryDecisionFactory.Create(
            AgentFailureCategory.ProviderFailure,
            "Provider failed.",
            attemptNumber: 3,
            sourceExecutionRunId: "execution-006");
        var ledger = Enumerable.Range(1, 2)
            .Select(index => AgentRecoveryLedger.CreateEntry(
                processRunId,
                stepRunId,
                providerDecision with
                {
                    AttemptNumber = index
                },
                "OpenAI",
                "gpt-5.4",
                providerFallbackCount: index,
                recordedAtUtc: now.AddMinutes(-index)))
            .ToList();

        var loopDecision = AgentRecoveryLedger.EvaluateLoopControl(
            ledger,
            providerDecision,
            maxIdenticalFailures: 10,
            maxProviderFallbacks: 2,
            now);

        Assert.True(loopDecision.ShouldEscalate);
        Assert.Contains("Provider fallback budget exhausted", loopDecision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void RecoveryLedger_blocks_attempt_until_backoff_expires()
    {
        var now = DateTimeOffset.UtcNow;
        var decision = AgentRecoveryDecisionFactory.Create(
            AgentFailureCategory.CriticalToolFailure,
            "Build failed.",
            attemptNumber: 1,
            sourceExecutionRunId: "execution-007",
            nextAttemptAtUtc: now.AddMinutes(3));
        var ledger = new[]
        {
            AgentRecoveryLedger.CreateEntry(
                Guid.NewGuid(),
                Guid.NewGuid(),
                decision,
                "OpenAI",
                "gpt-5.4",
                providerFallbackCount: 0,
                recordedAtUtc: now)
        };

        Assert.False(AgentRecoveryLedger.CanAttemptNow(ledger, now));
        Assert.True(AgentRecoveryLedger.CanAttemptNow(ledger, now.AddMinutes(4)));
    }
}
