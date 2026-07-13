# SB09 - Executor Refactoring Hardening Checkpoint

## Status

- `Completed`

## Objective

Force a refactoring-hardening checkpoint after executor abstractions, shared helpers, default category projects, and plugin executor adapters are in place. This checkpoint must prove executor isolation is real before templates and MAF adapter adoption begin.

## Success Criteria

- Default, plugin, runtime package, and feature-module executors compose through executor abstractions/core without MAF-owned fallback paths.
- Descriptor parity, id stability, settings schema compatibility, grants, side effects, deterministic preview, cancellation, and explicit failure behavior are proven.
- Performance and diagnostics issues in executor helper/category/plugin code are scanned, triaged, and fixed or explicitly assigned.
- The workbook and traceability show every executor source has a target owner and validation owner.
- No default/plugin/module executor failure collapses to a generic message without node, executor, source, plugin/package/type/tool, retryability, redacted detail, and repair hint.
- Moved executor code passes file-size/responsibility checks and does not create a new category monolith.

## Covered Inputs

- R07, R08, R09, R13, R14, R15, R17, R18.
- Architect note requiring forced refactoring-hardening subbundles.
- Performance review findings for executor descriptor materialization and helpers.

## Prerequisites

- SB06 completed.
- SB07 completed.
- SB08 completed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Core`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Plugins.Abstractions`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory`
- `C:\repositories\CanDoItAll\src\plugins`
- `C:\repositories\CanDoItAll\codex\bundles\workflow-node-project-isolation\architecture\03-executor-category-boundary.md`

## Deliverables

- Executor hardening report.
- Combined executor catalog and descriptor parity test suite covering default, plugin, runtime package, and feature-module executor sources.
- Plugin compatibility and security review summary.
- Focused performance scan summary for executor projects.
- No-generic-error diagnostic matrix for default, plugin, feature-module, external provider/tool, timeout, cancellation, payload, grant, OAuth, package-load, and activation failures.
- File-size/responsibility report for executor category projects and shared helpers.
- Cleanup changes limited to executor isolation issues.
- Updated execution report gate status.

## Dependency Impact

- SB10 template loading uses executor descriptors for node materialization. SB11 MAF adapter composes default and plugin executors through the executor catalog. SB12 UI display depends on descriptor category/source metadata. If SB09 is weak, later phases can falsely pass with incomplete executor coverage.

## Validation Depth

- `Critical executor/plugin hardening`
- Build, unit, integration, package-loading, security/secret masking, side-effect, diagnostics, architecture, and performance proof.

## Implementation Steps

1. Run focused builds/tests for executor abstraction/core/default/plugin projects.
2. Run combined descriptor parity tests over default, plugin, runtime package, and feature-module executor examples.
3. Run architecture checks for forbidden MAF ownership and circular dependencies.
4. Run focused performance scans for repeated JSON options, allocation-heavy descriptor materialization, LINQ chains in repeated paths, regex usage, async/cancellation behavior, and unbounded output buffering.
5. Run no-generic-error assertions across all executor categories and plugin paths listed in `inventories/06-error-state-inventory.md`.
6. Review logs for actionable state, repair hints, retryability, and sensitive-data masking.
7. Run file-size/responsibility scans and verify large executor moves were split into focused helpers/services.
8. Fix only executor-scope defects or document deferrals with an owning subbundle.
9. Update proof manifests, semantic invariants, workbook, and execution report.

## Scope Exceptions

- Template loader migration is SB10.
- MAF backend/compiler adapter isolation is SB11.
- Browser-visible executor display validation is SB12.

## Do Not Do

- Do not start template, MAF backend, API, UI, or Workbench adoption.
- Do not waive plugin security regressions as acceptable technical debt.
- Do not leave old MAF registration paths alive for fallback execution.
- Do not pass the checkpoint if a plugin/external executor failure is only repairable by reading a stack trace or raw provider logs.

## Acceptance Checklist

- [x] Combined executor build/test suite passes.
- [x] Descriptor parity and stable ids are proven for default, plugin, runtime package, and module-provided executor sources.
- [x] Plugin grants, trust/source, side effects, secret masking, and package loading pass.
- [x] Architecture checks show no executor implementation remains MAF-owned.
- [x] Performance findings are fixed or explicitly owned.
- [x] Failure diagnostics matrix passes for default, plugin, package, grant, OAuth, provider/tool, timeout, cancellation, and payload failures.
- [x] File-size/responsibility findings are fixed or explicitly owned.
- [x] Execution report marks SB09 as passed before SB10 starts.

## Execution Notes

- Added `WorkflowExecutorHardeningCheckpointTests` for combined default/plugin/runtime package/feature-module descriptor parity, source context, no MAF fallback, file-size/responsibility bounds, plugin invocation diagnostics, plugin activation diagnostics, and bundled plugin serializer ownership.
- Hardened `PluginWorkflowExecutorActivationException` with strongly typed activation failure kind, retryability, repair hint, and redacted technical detail.
- Consolidated Gmail and Office365 workflow executor `JsonSerializerOptions` into shared per-plugin static helpers.
- Updated workbook Summary, Validation Matrix, Plugin Consequences, and Error States rows for SB09 closure.
- Kept template loading, MAF adapter isolation, and UI/API/Workbench adoption out of scope for SB10-SB12.

## Validation Notes

- Plugin executor boundary build passed with 0 warnings and 0 errors.
- Gmail and Office365 bundled plugin builds passed with 0 warnings and 0 errors after serializer cleanup.
- Unit project no-dependencies build passed with 0 warnings and 0 errors.
- New `WorkflowExecutorHardeningCheckpointTests` passed: `5/5`.
- Combined executor/plugin hardening regression slice passed: `36/36`.
- Plugin catalog and email plugin integration proof uses an alternate output path because the default Web bin output is locked by an already-running `CanDoItAll.Web` process.
- Static ownership, performance, no-generic-error, anti-stub, workbook, prepared-validator, and closure-audit transcripts are recorded under `proof/SB09/transcripts/`.

## Proof Required

- `proof/SB09/manifest.md` with build/test transcripts, descriptor parity output, architecture check transcript, performance scan transcript, security review notes, and changed file hashes.
- `proof/SB09/semantic-invariants.md` covering id stability, descriptor parity, plugin compatibility, redaction, typed explicit failures, retryability, repair hints, no MAF fallback, file responsibility, and side-effect receipts.
- Semantic Adequacy Gate proof including adversarial plugin grant/secret/fallback cases, positive default and plugin execution cases, and anti-stub audit.

## Browser Validation Logging

- `N/A`. Browser-visible executor display proof is SB12.

## Progression Gate

- SB10 is blocked until SB09 passes. Any default or plugin executor without descriptor parity and security/side-effect proof must be resolved or explicitly removed from downstream template support with an approved exception.

## Suggested Agent Prompt

```text
Implement SB09 only. Harden executor isolation after SB06-SB08. Run combined executor/default/plugin tests, descriptor parity, architecture checks, no-generic-error diagnostics review, security masking review, file-size/responsibility review, and focused performance scans. Fix only executor-scope defects and record proof. Do not start template or MAF adoption.
```
