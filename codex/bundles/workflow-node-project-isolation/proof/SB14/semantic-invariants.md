# SB14 Semantic Invariants

## Raw Request Closure

- R01-R18 are closed by the SB01-SB14 proof chain. SB14 specifically closes final cleanup, final regression, documentation, workbook, performance review, and validator readiness.
- Base-up execution order was preserved: workflow abstractions/builders, workflow core/runtime, hardening, executor abstractions/categories/plugins, executor hardening, templates, MAF adapter, API/UI/Workbench adoption, adoption hardening, then final closure.
- Small and medium UI viewport tests were intentionally skipped because the current execution request scopes the app to large-screen desktop.

## No Fallback

- `src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows` contains zero files.
- Static no-fallback proof found no old workflow fallback symbols in the checked source/test/bundle scope.
- API/UI/Workbench adoption files do not directly reference MAF workflow compiler/backend/event/LLM internals, the old built-in executor alias, or Microsoft Agents workflow package APIs.
- Host and module registration delegate workflow composition to `AddMafWorkflowAdapterServices(...)`; runtime and executor core registration are owned by the adapter extension.

## Compatibility

- Persisted workflow model contracts remain in `CanDoItAll.AgentFramework.Models`.
- Executor ids, template keys, workflow definitions, runtime events, and process/workflow integration behavior are covered by focused unit, component, integration, and browser proof.
- Template loading still validates repository templates through the descriptor-aware template services.
- Default executor descriptors remain partitioned by standard category and aggregated through `AddStandardWorkflowExecutors(...)`.

## Plugin Behavior

- Plugin descriptor projection, source/trust metadata, package wrapping, grant evaluation, bundled plugin behavior, and email workflow integration are covered by the final integration slice.
- Plugin failures remain explicit; no plugin fallback path was added during final cleanup.
- Plugin executor conventions are documented in `docs\workflow-maf-hardening.md`.

## UI, API, And Workbench

- Workflow page component tests passed with typed diagnostic display coverage.
- Workflow API, MAF handoff, plugin catalog/email behavior, and process-to-workflow email scenario integration tests passed.
- Large-screen Playwright proof passed for workflow shell and Workbench workflow-node interaction.
- Fresh SB14 browser pass/fail proof is in Playwright transcripts; screenshots are copied into `proof/SB14/browser/` from the latest matching large-screen proof set because the rerun did not emit new named screenshots.

## Diagnostics

- `WorkflowFailureDiagnosticEnvelope` remains the shared typed diagnostic contract.
- `WorkflowFailureDisplayFormatter` remains the UI/Workbench display boundary for typed diagnostics and redacted legacy fallback text.
- No-generic-error/redaction audit passed for workflow/runtime/executor/plugin/template/MAF/API/UI/Workbench paths.
- Future diagnostics must carry retryability, repair hint, redacted technical detail, and the most specific available workflow/node/executor/plugin/package/tool/operation/backend context.

## File Responsibility

- New workflow/executor/template/adapter behavior is in focused owner projects.
- Existing large UI page and Workbench project-structure orchestration files are documented approved exceptions for current responsibilities only.
- New parsing, diagnostics, runtime, template, adapter, executor, or plugin logic must stay outside those large files.
- Anti-stub audit passed after narrowing the search to actual stub markers and excluding legitimate domain placeholder terminology.

## Performance

- SB14 fixed two real serializer-option allocation candidates by caching redacted JSON and project-structure task metadata serializer options.
- Final focused performance scan found 0 critical findings.
- Remaining serializer option construction sites are static option initialization or static `Create*` helpers.
- LINQ/list allocation candidates are not treated as critical without measured hot-loop evidence.

## Completed Validator Semantic Contract Addendum

- Invariant ID: SB14-final-closure
- Source raw note: R01-R18 workflow-node project isolation closure evidence for SB14.
- Expected behavior: The SB14 scope remains closed by its recorded proof artifacts and downstream SB14 final regression.
- Disallowed shallow implementation: Do not replace the recorded source/test proof with summary-only closure or silent fallback behavior.
- Failing-first test: N/A - process/no production behavior metadata addendum; adversarial negative proof remains in the SB14 transcript set where applicable.
- Passing test: See bundle://proof/SB14/transcripts/ for the SB14 passing command transcript set and SB14 final regression transcripts.
- Changed source files: See bundle://proof/SB14/manifest.md and bundle://proof/SB14/changed-file-hashes.txt for the final closure hash set.
- Production assertions: Production behavior is asserted by the SB14 proof chain and SB14 final unit/component/integration/browser regression.
- Red-team negative case: SB14 no-fallback, no-generic, anti-stub, and responsibility audits guard the final state.
- Downstream dependency check: SB14 final closure revalidated downstream workflow, executor, plugin, template, MAF adapter, API, UI, Workbench, and process integration paths.
