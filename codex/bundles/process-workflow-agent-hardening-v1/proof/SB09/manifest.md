# SB09 Final Red-Team QA Manifest

- Status: Passed.
- Run date: 2026-06-02
- Scope: SB01-SB08 proof manifests, stale-lineage rejection, browser proof, usage disclosure, workflow side-effect idempotency, genericity, and final validator.

## Proof Artifacts
- `proof/SB09/final-red-team-report.md`
- `proof/SB09/fake-proof-resistance.md`
- `proof/SB09/changed-file-hashes.md`
- `proof/SB09/final-validator-output.txt`
- `proof/SB08/manifest.md`
- `proof/SB08/scenarios/*/browser-proof.md`
- `proof/SB08/scenarios/*/usage-summary.json`
- `proof/SB08/scenarios/*/genericity-audit.md`

## Commands
- `python .\codex\bundles\process-workflow-agent-hardening-v1\scripts\validate_bundle.py --root .\codex\bundles\process-workflow-agent-hardening-v1 --stage prepared`
- `.\codex\bundles\process-workflow-agent-hardening-v1\scripts\run_sb08_multidomain_e2e.ps1`
- `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests.RecordArtifactAsync_normalizes_null_optional_text_fields|FullyQualifiedName~ProcessesServiceIntegrationTests.TransitionStepAsync_SB01_INV_001_allows_automation_completion_with_matching_execution_lineage_required_artifact|FullyQualifiedName~ProcessesServiceIntegrationTests.TransitionStepAsync_SB01_INV_002_allows_automation_completion_when_transition_context_is_inferred_from_step_artifacts|FullyQualifiedName~RuntimeHostedWorkerPolicyIntegrationTests" --logger "console;verbosity=minimal"`
- `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests.TransitionStepAsync_SB10_INV_001_rejects_stale_execution_lineage_required_artifact_on_manual_completion" --logger "console;verbosity=minimal"`
- `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~AutomationRuntimeIntegrationTests.Concurrent_connector_enqueue_with_same_idempotency_key_returns_single_command" --logger "console;verbosity=minimal"`
- `rg -n "tetris-mini-game|expense-tracker-lite|plant-watering-planner|study-kanban-flashcards|recipe-pantry-planner" .\src .\Templates .\codex\skills -g "!**/bin/**" -g "!**/obj/**"`
