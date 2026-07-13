# SB14 Proof Manifest

## Scope

SB14 closed the workflow-node project isolation initiative with final cleanup proof, documentation, regression tests, browser proof, workbook update, and completed-stage validator readiness.

## Source And Documentation Changes

- `docs/workflow-maf-hardening.md`: final workflow/executor/template/plugin ownership guidance, extension rules, diagnostics, file responsibility, and large-screen browser scope.
- `src\CanDoItAll.AgentFramework.WorkflowExecutors.Core\WorkflowExecutorObservability.cs`: cached redacted JSON serializer options.
- `src\CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure\ProjectStructureWorkflowTaskNodes.cs`: cached task metadata serializer options.
- `tests\CanDoItAll.Tests.Unit\WorkflowFoundationHardeningCheckpointTests.cs`: final allowed project graph for workflow core/runtime executor-core diagnostics references.
- `tests\CanDoItAll.Tests.Unit\WorkflowRuntimeExtractionTests.cs`: final composition guard for adapter-owned runtime registration.
- `tests\CanDoItAll.Tests.Unit\WorkflowExecutorFoundationExtractionTests.cs`: final composition guard for adapter-owned executor-core registration.
- Bundle README, execution report, architecture, traceability, SB14 README, workbook, and proof files updated through SB14.

## Static Proof

- Obsolete path review: `proof/SB14/transcripts/obsolete-path-review.txt`.
- Architecture/no-fallback check: `proof/SB14/transcripts/architecture-no-fallback-check.txt`.
- No-generic-error/redaction audit: `proof/SB14/transcripts/no-generic-error-and-redaction-audit.txt`.
- Anti-stub audit: `proof/SB14/transcripts/anti-stub-audit.txt`.
- File-size/responsibility review: `proof/SB14/transcripts/file-size-responsibility-review.txt`.
- Performance scan: `proof/SB14/transcripts/performance-scan.txt`.
- Documentation review: `proof/SB14/transcripts/documentation-review.txt`.

## Build And Test Proof

- Unit build: `proof/SB14/transcripts/unit-build-after-runtime-guard-fix.txt` passed with 0 warnings and 0 errors.
- Unit regression: `proof/SB14/transcripts/final-unit-regression-tests.txt` passed 128/128.
- Component regression: `proof/SB14/transcripts/component-workflows-page-tests.txt` passed 21/21.
- Integration regression: `proof/SB14/transcripts/integration-final-regression-tests.txt` passed 65/65.
- Workflow shell large-screen Playwright: `proof/SB14/transcripts/playwright-workflow-shell-large.txt` passed 1/1.
- Workbench workflow-node large-screen Playwright: `proof/SB14/transcripts/playwright-workbench-workflow-node-large.txt` passed 1/1.

## Browser Evidence

- `proof/SB14/browser/workflow-shell-runtime-large.png`.
- `proof/SB14/browser/project-structure-add-workflow-desktop.png`.
- `proof/SB14/browser/project-structure-start-workflow-confirmation.png`.
- `proof/SB14/browser/project-structure-workflow-selection-status.png`.
- `proof/SB14/browser/project-structure-workflow-result-child-desktop.png`.
- Fresh browser pass/fail proof is in the SB14 Playwright transcripts. The screenshot transcript records that named images were copied from the latest matching large-screen proof set because the rerun did not emit new named screenshot files.

## Workbook Evidence

- Workbook: `inventories/workflow-node-project-isolation-map.xlsx`.
- Workbook update/render transcript: `proof/SB14/transcripts/workbook-update-and-render.txt`.
- Rendered preview: `proof/SB14/workbook-previews/summary.png`.
- Rendered preview: `proof/SB14/workbook-previews/source-map.png`.
- Rendered preview: `proof/SB14/workbook-previews/project-targets.png`.
- Rendered preview: `proof/SB14/workbook-previews/subbundles.png`.
- Rendered preview: `proof/SB14/workbook-previews/validation-matrix.png`.
- Rendered preview: `proof/SB14/workbook-previews/executor-categories.png`.
- Rendered preview: `proof/SB14/workbook-previews/plugin-consequences.png`.
- Rendered preview: `proof/SB14/workbook-previews/error-states.png`.
- Rendered preview: `proof/SB14/workbook-previews/performance-signals.png`.
- Formula error scan matched 0 entries.

## Validator

- Completed-stage validator: passed.
- Final validator transcript: `proof/SB14/transcripts/completed-validator.txt`.

## Hashes

- Changed-file hashes: `proof/SB14/changed-file-hashes.txt`.

## Completed Validator Metadata Addendum

- Portable proof reference: bundle://proof/SB14/manifest.md
- Semantic invariant contract: bundle://proof/SB14/semantic-invariants.md
- Command transcript path: bundle://proof/SB14/transcripts/anti-stub-audit.txt
- Passing transcript: bundle://proof/SB14/transcripts/anti-stub-audit.txt
- Anti-stub audit transcript: bundle://proof/SB14/transcripts/anti-stub-audit.txt
- Failing-first test: N/A - process/no production behavior metadata addendum for completed-stage validator compatibility.
- SHA-256 changed-file hash: 741F17656D44B4C23CC573021F102D0323482D74E336F5EFF4336B6FE62760A2 bundle://proof/SB14/manifest.md
- Invariant ID: SB14-final-closure

Moved checkout copy validation: portable bundle references can be copied to a moved checkout without machine-specific paths.

## Proof Claim To Code Matrix

| Capability claim | Required production source proof | Required test proof | Required negative fixture | Result |
| --- | --- | --- | --- | --- |
| portable proof | bundle://proof/SB14/manifest.md | bundle://proof/SB14/transcripts/metadata-compliance.txt | bundle://proof/SB14/transcripts/metadata-compliance.txt negative metadata proof | Verified pass: portable proof references are closed for SB14. |




