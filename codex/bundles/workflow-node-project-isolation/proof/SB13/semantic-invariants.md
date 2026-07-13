# SB13 Semantic Invariants

## SB13-ADOPT-001 - API/UI/Workbench Have No Hidden MAF Fallback

- Source raw note: forced hardening checkpoints must prove each closed logical block is a good base before downstream cleanup.
- Expected behavior: API, workflow UI, canvas editor, and Workbench workflow-node adoption surfaces do not reference MAF compiler/backend/event/LLM internals, the removed built-in executor alias, or Microsoft Agents workflow package APIs directly.
- Disallowed shallow implementation: keep the UI working by leaving a hidden `MafWorkflowCompiler`, `MafInProcessWorkflowExecutionBackend`, or `AddBuiltInWorkflowExecutors` fallback in adoption surfaces.
- Adversarial negative proof: `bundle://proof/SB13/transcripts/architecture-no-fallback-check.txt`.
- Semantic positive proof: `bundle://proof/SB13/transcripts/focused-adoption-hardening-tests.txt`; `ApiUiWorkbenchAdoptionDoesNotReferenceMafInternalsOrOldExecutorAliases`.
- Passing transcript: `bundle://proof/SB13/transcripts/combined-hardening-unit-tests.txt`.
- Changed source files and hashes: `bundle://proof/SB13/changed-file-hashes.txt`.
- Production assertions: `repo://tests/CanDoItAll.Tests.Unit/WorkflowAdoptionHardeningCheckpointTests.cs`; `bundle://proof/SB13/transcripts/architecture-no-fallback-check.txt`.
- Red-team negative case: direct MAF compiler/backend or removed alias reference fails the guard test and static audit.
- Downstream dependency check: SB14 can perform cleanup only after this no-fallback proof.

## SB13-DIAG-002 - Typed Diagnostic Display Is Centralized And Not Rebuilt In UI

- Source raw note: API/UI/Workbench diagnostic display must be repairable, typed, redacted, and not reconstructed from exception strings.
- Expected behavior: typed diagnostic payload parsing remains inside `WorkflowFailureDisplayFormatter`; UI and Workbench adoption surfaces consume formatter output and do not deserialize `WorkflowFailureDiagnosticEnvelope` directly.
- Disallowed shallow implementation: copy diagnostic-envelope deserialization into Blazor or Workbench files, or render raw event messages for failed workflow states.
- Adversarial negative proof: `bundle://proof/SB13/transcripts/no-generic-error-audit.txt`.
- Semantic positive proof: `bundle://proof/SB13/transcripts/focused-adoption-hardening-tests.txt`; `WorkflowUiAndWorkbenchAdoptionUseTypedFailureDisplayBoundary`; `TypedDiagnosticDeserializationStaysOutOfUiAndWorkbenchAdoptionCode`.
- Passing transcript: `bundle://proof/SB13/transcripts/component-workflows-page-tests.txt`.
- Changed source files and hashes: `bundle://proof/SB13/changed-file-hashes.txt`.
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Workflows.Core/WorkflowFailureDisplayFormatter.cs`; `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.cs`; `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureWorkflowNodeService.cs`.
- Red-team negative case: `@workflowEvent.Message`, `ToUserMessage(workflowEvent.Message)`, `Message = message`, or direct envelope deserialization in consumers fails the guard tests or no-generic audit.
- Downstream dependency check: SB14 must keep these guards in final regression.

## SB13-HARDEN-003 - Stale Pre-Adoption Guard Was Corrected

- Source raw note: hardening checkpoints must not hide contradictions from later phase proof.
- Expected behavior: executor hardening now agrees with SB11 adapter isolation: the old `AgentFramework.Maf/Runtime/Workflows` folder is empty, and adapter files live in `Workflows.MafAdapter`.
- Disallowed shallow implementation: keep a stale SB09 assertion that expects old MAF workflow files to remain, which would allow future cleanup to borrow trust from false proof.
- Adversarial negative proof: first combined unit run failed on `WorkflowExecutorHardeningCheckpointTests.ExecutorOwnershipAuditHasNoMafFallbackOrCategoryMonolith`; the updated transcript is `bundle://proof/SB13/transcripts/combined-hardening-unit-tests.txt`.
- Semantic positive proof: `bundle://proof/SB13/transcripts/combined-hardening-unit-tests.txt` passed 37/37 after the stale guard fix.
- Passing transcript: `bundle://proof/SB13/transcripts/unit-build-after-stale-guard-fix.txt`; `bundle://proof/SB13/transcripts/combined-hardening-unit-tests.txt`.
- Changed source files and hashes: `bundle://proof/SB13/changed-file-hashes.txt`.
- Production assertions: `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorHardeningCheckpointTests.cs`; `repo://tests/CanDoItAll.Tests.Unit/MafWorkflowAdapterIsolationTests.cs`.
- Red-team negative case: an old MAF workflow file reappearing fails both executor hardening and adapter isolation tests.
- Downstream dependency check: SB14 cleanup can rely on the old MAF workflow folder being empty.

## SB13-PERF-004 - Adoption Performance And File Responsibility Are Disposed

- Source raw note: use performance analysis and forced refactoring-hardening checkpoints to avoid copied monoliths and weak bases.
- Expected behavior: SB13 records focused performance scan, file-size/responsibility review, and explicit disposition for existing large UI files without speculative rewrite.
- Disallowed shallow implementation: mark hardening complete with no performance scan, no file-size review, or broad UI refactor outside checkpoint scope.
- Adversarial negative proof: `bundle://proof/SB13/transcripts/performance-scan.txt`; `bundle://proof/SB13/transcripts/file-size-responsibility-review.txt`.
- Semantic positive proof: `bundle://proof/SB13/transcripts/performance-scan.txt` reports 0 critical findings; `bundle://proof/SB13/transcripts/file-size-responsibility-review.txt` records approved exceptions and centralization guard proof.
- Passing transcript: `bundle://proof/SB13/transcripts/performance-scan.txt`; `bundle://proof/SB13/transcripts/file-size-responsibility-review.txt`.
- Changed source files and hashes: `bundle://proof/SB13/changed-file-hashes.txt`.
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Workflows.Core/WorkflowFailureDisplayFormatter.cs`; `repo://tests/CanDoItAll.Tests.Unit/WorkflowAdoptionHardeningCheckpointTests.cs`.
- Red-team negative case: duplicated typed diagnostic parsing or new stub/generic phrases in adoption files fails `WorkflowAdoptionHardeningCheckpointTests` or the static audits.
- Downstream dependency check: SB14 must document approved large-file exceptions and run a final broad scan before completed closure.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Adoption no-fallback guard | `repo://tests/CanDoItAll.Tests.Unit/WorkflowAdoptionHardeningCheckpointTests.cs`; `bundle://proof/SB13/transcripts/focused-adoption-hardening-tests.txt` | `repo://src/CanDoItAll.Web/Api/WorkflowsApi.cs`; `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.cs`; `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureWorkflowNodeService.cs` | `bundle://proof/SB13/transcripts/combined-hardening-unit-tests.txt`; `bundle://proof/SB13/transcripts/integration-adoption-smoke-tests.txt`; `bundle://proof/SB13/transcripts/playwright-workbench-workflow-node-large.txt` | `bundle://proof/SB13/transcripts/architecture-no-fallback-check.txt`; `bundle://proof/SB13/transcripts/anti-stub-audit.txt` |
| Typed diagnostic display boundary | `repo://src/CanDoItAll.AgentFramework.Workflows.Core/WorkflowFailureDisplayFormatter.cs`; `repo://tests/CanDoItAll.Tests.Unit/WorkflowAdoptionHardeningCheckpointTests.cs` | `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor`; `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.WorkflowNodes.cs` | `bundle://proof/SB13/transcripts/component-workflows-page-tests.txt`; `bundle://proof/SB13/transcripts/playwright-workflow-shell-large.txt` | `bundle://proof/SB13/transcripts/no-generic-error-audit.txt` |

## Raw Note Closure

- Forced hardening checkpoint: `Solved for SB13`; SB14 final closure remains.
- API/UI/Workbench no-fallback adoption: `Solved for SB13`; final cleanup remains SB14.
- Performance and no copied monoliths: `Partially solved`; SB13 recorded 0 critical findings and approved existing UI file-size exceptions, SB14 must document them in final docs.
- Browser proof: `Solved for large-screen-only SB13 scope`; small and medium viewport tests intentionally skipped by user instruction.

## Completed Validator Semantic Contract Addendum

- Invariant ID: SB13-final-closure
- Source raw note: R01-R18 workflow-node project isolation closure evidence for SB13.
- Expected behavior: The SB13 scope remains closed by its recorded proof artifacts and downstream SB14 final regression.
- Disallowed shallow implementation: Do not replace the recorded source/test proof with summary-only closure or silent fallback behavior.
- Failing-first test: N/A - process/no production behavior metadata addendum; adversarial negative proof remains in the SB13 transcript set where applicable.
- Passing test: See bundle://proof/SB13/transcripts/ for the SB13 passing command transcript set and SB14 final regression transcripts.
- Changed source files: See bundle://proof/SB13/manifest.md and bundle://proof/SB14/changed-file-hashes.txt for the final closure hash set.
- Production assertions: Production behavior is asserted by the SB13 proof chain and SB14 final unit/component/integration/browser regression.
- Red-team negative case: SB14 no-fallback, no-generic, anti-stub, and responsibility audits guard the final state.
- Downstream dependency check: SB14 final closure revalidated downstream workflow, executor, plugin, template, MAF adapter, API, UI, Workbench, and process integration paths.
