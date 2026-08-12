# B02 independent Workbench gate review

## Decision

`NO-GO.`

B02 is not ready to close. The typed compiler, host-correct executable selection, optional terminal/elevation boundaries, path containment, UI state model, and frozen evidence package are materially sound, but four gate blockers remain. B03 must stay blocked until they are corrected and independently re-reviewed.

## Blocking findings

### B02-IND-001 — Direct launches abandon the B01-owned process identity (`P0`)

- `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureRuntimeAdapters.cs:121-140` starts an `IWorkspaceProcessSession`, calls `Detach()`, logs the returned PID, and then discards the full owned identity. No Workbench registry, lease, stop path, shutdown cleanup, or recovery handoff receives it.
- `tests/Unit/CanDoItAll.Tests.Unit/ProjectStructureRuntimeAdapterTests.cs:13-33` makes detachment the expected success behavior, while lines 213-247 expressly reject waiting or termination. The actual-host test uses the short-lived `dotnet --version` command, so it cannot prove cleanup of a long-running `.NET watch`, Node/Tailwind, Docker, or Python process.
- Deferring Manager supervision to B03 does not close this gap: B03 owns Manager-launched processes, whereas these processes are launched by Workbench. The behavior violates the accepted lifecycle rule in `architecture/02-execution-plan-and-process-lifecycle.md:50-61` and reopens B01 `EXEC-006`: a kept-alive process can survive the Workbench/UI/host lifecycle without an owner capable of verified termination.

Required correction: hand the session identity to one explicit Workbench lifecycle/lease owner, prove shutdown/disposal and explicit stop behavior without foreign-process termination, and retain or recover sufficient exact identity if the intended lifetime crosses a scope or restart. Do not create a second low-level process host or silently assign Workbench ownership to the Manager-only B03 registry.

### B02-IND-002 — Script approval is declarative but unenforced (`P0`)

- Explicit shell plans set `RequiresApproval: true` in `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureRuntimePlan.cs:265-308`.
- `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureRuntimeLauncher.cs:141-186` validates targets and capabilities, then launches the plan without reading `RequiresApproval` or receiving approval evidence.
- The only repository uses of this B02 `RequiresApproval` member are its construction and a test fixture; there is no production consumer. `src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.RuntimeLaunch.cs:5-12` invokes the launcher directly, with no confirmation or typed authorization handoff.

This makes the B02-T03 claim that scripts have separate side-effect/approval rules false. A button click cannot satisfy a contract that is neither surfaced nor enforced, especially because the launcher is also a callable service boundary.

Required correction: make approval an enforced, typed launch precondition for approval-requiring plans, surface a truthful confirmation/authorization flow, and prove direct, terminal, and elevated requests fail closed without that evidence. Remove the field instead only if the architecture/tasks are deliberately amended to define a different coherent policy.

### B02-IND-003 — Ambiguous PowerShell host options are silently migrated (`P1`)

- `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureLegacyRuntimeCommandMigrator.cs:120-146` rejects encoded-command tokens but otherwise scans from argv index 1 until it finds the first `-Command`/`-c`; it does not validate or consume the preceding PowerShell host-option grammar.
- Consequently, ambiguous or contradictory values such as `pwsh -File other.ps1 -Command dotnet run` and host options with their own operands can be reinterpreted as the allowlisted `dotnet run` payload instead of requiring operator repair.
- The negative tests in `tests/Unit/CanDoItAll.Tests.Unit/ProjectStructureRuntimePlanCompilerTests.cs:76-88` cover encoded, dynamic, and chained content, but not ambiguous pre-command host options. This contradicts NODE-006, `architecture/03-workbench-runtime-and-terminal.md:60-68`, and the evidence/review statements that ambiguous legacy content remains unresolved.

Required correction: parse only a small explicit allowlist of safe PowerShell host-option shapes with defined arity before exactly one `-Command`, reject conflicting/unknown/duplicate execution selectors, and add positive plus ambiguous-negative migration tests on both hosts.

### B02-IND-004 — Agent capability projection discloses resolved physical paths (`P1`)

- Runtime path authority correctly resolves workspace values and authorized opaque external-target aliases to physical paths in `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureRuntimePathResolver.cs:41-57` and lines 114-142.
- The execution plan necessarily carries those physical paths, and `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureRuntimePlan.cs:321-329` plus lines 387-405 can include physical project/script targets in `DisplayCommand`.
- `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureNodeActionCapabilityResolver.cs:61-69` copies both `DisplayCommand` and the physical `WorkingDirectory` into action capabilities. `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/WorkbenchProjectStructureRuntimeGateway.cs:399-406` then returns both through the agent-facing gateway.

An agent authorized to use an opaque external-target alias can therefore receive the resolved host root in its capability response. This defeats the Core PATH-007 non-disclosure property even though path containment itself remains correct. The current screenshots do not render these fields, and no agent-projection regression checks physical-root absence.

Required correction: separate the physical execution plan from a logical/opaque capability projection. Agent/API/UI output must use canonical logical paths or the authorized alias and a redacted display projection; add workspace and external-alias tests proving that physical roots and environment values are absent.

## Independently verified evidence

- Governed manifest integrity: all 35 source hashes and all 13 test/build/screenshot/host-evidence hashes recompute exactly; all referenced files exist. The manifest contains four failing-first records and seven source assertions.
- TRX counters independently parse to Windows 87/87, Linux 87/87, Components 24/24, and default/headless Playwright 1/1 each, with zero failed, skipped, or undiscovered tests.
- The three affected Release build logs report zero warnings and zero errors.
- The source-reference manifest reconciles to 41 records, 41 unique IDs, 41 unique paths, and zero missing paths.
- The four governed screenshots were visually inspected. They truthfully show available, dependency-missing, headless terminal-only, and foreign-host-path states at the stated 1600x1000 viewport and contain no visible secret or physical path.
- The schema-3 artifact scan accounts for 15 candidates as 10 scanned text artifacts, four PNGs, and one control output, with zero oversized/unreadable text files and zero findings. It loaded no private sentinels, so the result proves the configured scanner rules and coverage accounting, not arbitrary-secret non-disclosure; the screenshot inspection and source review remain necessary.
- The ordinary launcher contains no `Process.Start`; the two B02 `Process.Start` sites are isolated to terminal presentation and Windows `runas`. No new project/build/package reference is needed for the reviewed design.

## Non-blocking residuals and deferred proof

- Genuine macOS execution and terminal presentation remain honestly deferred under `RUNTIME-MACOS-VALIDATION-001`; deterministic macOS fixtures are not actual-host evidence.
- The Linux proof establishes direct execution and policy behavior, not an enabled desktop-terminal deployment profile. Such a profile must validate its configured executable and argument prefix before claiming support.
- The targeted validation ladder is proportionate for this gate, but it does not replace the later hosted, aggregate full-suite, and final R4 evidence.
- A dedicated sentinel input would strengthen the next redaction refresh. Its current absence is not independently blocking because the reviewed artifacts contain no secret-bearing configuration source and all binary evidence was inspected, but it must not be described as sentinel-backed proof.

## Re-entry criteria

Re-review may be bounded to the four findings above plus refreshed evidence consistency. At minimum it must include focused Windows/Linux regressions for owned long-running cleanup/foreign identity, enforced script approval, ambiguous PowerShell wrapper repair, and logical/opaque agent capability projection; refreshed governed hashes and schema-3 coverage; and unchanged architecture/dependency direction.

Final decision: `B02 Workbench gate NO-GO`. B03 remains blocked.

## Re-review

### Decision

`GO.`

All four findings from the initial independent review are closed on the final frozen remediation source. The first remediation of B02-IND-001 briefly retained a cancellation-sensitive hosted-shutdown path; independent re-review rejected that intermediate snapshot. The final implementation closes that defect as well. After normal canonical status, index, checksum, and final portable-validation bookkeeping, B03 alone may become eligible.

### Finding closure

- **B02-IND-001 closed:** `ProjectStructureRuntimeSessionRegistry` is the host-lifetime Workbench owner of exact B01 sessions. The direct adapter no longer detaches or discards identities. The registry rejects duplicate live node starts, exposes exact-node stop across scoped adapters, observes natural completion, and drains all retained sessions during host shutdown. Its singleton safely owns one explicit DI child scope for the scoped canonical process host; scoped launch adapters do not capture that scoped host into a singleton. Final shutdown is idempotent through one cached stop-core task. Gate acquisition and mandatory termination use non-cancellable cleanup, every per-session termination/disposal exception—including `OperationCanceledException`—is isolated and logged, remaining sessions are still attempted, and scope disposal happens after the full drain. Deterministic regressions prove both an already-cancelled hosted-stop token and per-session termination cancellation across two sessions; the focused registry/adapter class is 14/14.
- **B02-IND-002 closed:** approval is a typed launcher input. Compatibility overloads default to `NotGranted`, and every direct, terminal, or elevated launch of a plan with `RequiresApproval` fails before delegation unless `OperatorConfirmed` is supplied. The Workbench UI resolves the plan, opens a one-launch warning dialog, does not remember approval, and cancellation launches nothing. Unit and Playwright evidence cover rejection, one approved launch, dialog presentation/cancel, and absence of a physical workspace path from the dialog.
- **B02-IND-003 closed:** the PowerShell migrator now requires exactly one `-Command`/`-c`, permits only the small flag-only `-NoLogo`, `-NoProfile`, and `-NonInteractive` host-option set before it, and rejects encoded, operand-bearing, conflicting, unknown, and duplicate execution-selector shapes for operator repair. Positive bounded-wrapper and negative `-File`, `-WorkingDirectory`, and duplicate-`-Command` regressions execute in both host slices.
- **B02-IND-004 closed:** `AgentExecution` capability projection returns empty resolved command and physical-working-directory fields, while execution continues to use the authorized physical plan internally. The gateway therefore cannot reverse an opaque external-target alias into an agent-visible host root. A private-root sentinel regression proves it is absent from command, working-directory, and guidance output; operator UI projection remains separately scoped.

### Evidence reconciliation

- The final governed manifest recomputes with 37/37 source hashes and 13/13 test, build, screenshot, and host-evidence hashes matching; no referenced artifact is missing.
- Authoritative TRX counters independently parse to Windows 98/98, Linux 98/98, Components 24/24, default Playwright 1/1, and headless Playwright 1/1, with zero failed or not-executed tests. The lifecycle-only final correction is additionally represented in the refreshed both-host unit slices, and the affected Workbench Release build is current with zero warnings/errors.
- The runtime source-reference manifest reconciles to 43 records, 43 unique IDs, 43 unique paths, and zero missing paths.
- Schema-3 coverage reconciles all 15 candidates as 10 scanned text artifacts, four inspected/hash-bound PNGs, and one control output, with zero oversized/unreadable gaps and zero findings. As before, no private scanner sentinel was loaded, so this is rule/coverage proof rather than sentinel-backed proof.
- The portable runtime validator independently passes before this appended review at 321 files, zero errors, and zero warnings with checksums skipped. Final index/checksum regeneration and the post-review validator remain canonical bookkeeping.

### Residual boundaries

- The Workbench registry is intentionally an in-memory host-lifetime owner, not durable cross-application-restart supervision. B03 retains its separate Manager-launched-process ownership scope.
- Genuine macOS, configured Linux/macOS desktop-terminal profiles, hosted validation, the aggregate full suite, and final R4 remain deferred exactly as previously recorded; B02 makes no support claim from deterministic fixtures or Linux Docker alone.
- No full suite was rerun for this bounded remediation. The targeted invalidation decision is acceptable because the final edit is confined to Workbench shutdown cleanup and is covered by refreshed Windows/Linux lifecycle slices plus the current affected-project build.

Final re-review decision: `B02 Workbench gate GO`. No B02 blocker remains.
