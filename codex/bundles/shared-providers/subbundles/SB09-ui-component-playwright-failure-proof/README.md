# SB09 — UI component, Playwright, screenshot, accessibility, and recovery proof

State: `LOCKED`  
Proof tier: `Governed`  
Depends on: `SB08`  
Next on pass: `SB10`

## Objective

Harden and prove the desktop UI against real central/client behavior, overlays, focus, failures, concurrency, and existing provider regression.

## Observable outcome

Focused browser evidence demonstrates the complete operator workflows and visually inspected normal/error/open-overlay states.

## Inputs and current-state anchors

- Bundle root execution contract and architecture documents.
- Current repository state, not only the prepared SHA.
- Relevant source/test impact maps.
- Completed proof and handoff from every dependency.
- Current mandatory SharedInfo skills.

## Scope

- Complete component coverage gaps found in SB08.
- Add stable data/test selectors only where semantic role/label is insufficient.
- Run focused Playwright against the multi-instance or controlled real Web hosts.
- Cover central publish/unpublish, source add/test, catalog import, hybrid list, imported detail, sync/unavailable/recovery.
- Cover unauthorized, source offline, source identity mismatch, stale/unpublished and concurrency conflict.
- Inspect keyboard navigation, focus trap/restore, labels and non-color status.
- Capture normal and relevant open-overlay screenshots at supported desktop viewport.
- Record first viewport, sizing and scroll owner findings.
- Fix UI defects with focused component/browser reruns only.
- Do not run full Playwright suite.

## Out of scope

- No mobile viewport matrix for CanDoItAll app.
- No final stable aggregate.
- No OpenAPI snapshot.

## Implementation sequence

1. Use current Playwright fixture conventions and deterministic source data.
2. Prefer roles/labels over brittle CSS selectors.
3. Avoid arbitrary delays; wait on observable state/network.
4. Inspect screenshots, not merely create files.
5. Record normal and open-overlay screenshots per current UI skill.
6. Verify no token/secret appears in DOM, screenshot, trace or console.
7. Verify source outage/recovery without page reload where expected.
8. Run only the named shared-provider Playwright topic once after component checks are green.

## C# Architecture Impact

This subbundle is architecture-significant. Re-read
`architecture/00-csharp-current-state-inventory.md` through
`architecture/04-csharp-testability-plan.md`, update the affected checkpoint, and stop rather
than use a boundary workaround.

## Boundary Ownership

Components remain Workspace-owned. Playwright/test support owns browser setup and artifacts.

## Dependency Direction

No product dependency change expected. Any service contract change reopens owning backend gate.

Record before and after `ProjectReference`/namespace direction even when no reference is
expected to change. A no-change result is still evidence.

## Pattern Decision

Behavioral browser test with semantic selectors and screenshot inspection.

Do not introduce an adjacent alternative pattern without reopening the owning ADR and
recording why the selected pattern failed.

## Testability Contract

Real rendered UI and deterministic backend; component tests isolate state logic.

Every new behavior needs one realistic positive proof and one meaningful negative proof. Test
existence, file counts, status codes alone, or mocked self-assertions do not prove behavior.

## Partial Class Policy

UI fixes remain cohesive child components; no giant test helper or component partial.

A large partial or monolithic file is a gate failure unless the architecture review documents
a narrow unavoidable reason.

## Architecture Proof Required

- Component list/run results.
- Focused Playwright list/run results.
- Screenshots and inspection notes.
- Accessibility/focus/scroll evidence.
- Browser console/network error review.
- Secret DOM/trace/screenshot scan.

## Test selection

| Topic | Owning project/lane | Stable filter | Planned expected discovery | Selection reason |
| --- | --- | --- | ---: | --- |
| `SharedProviderUiComponentTests` | `tests/Solutions/CanDoItAll.Tests.Components.slnx` | `FullyQualifiedName~SharedProvider` | 28 | Frozen combined component topic after SB08 implementation. |
| `SharedProviderManagementPlaywrightTests` | `tests/Solutions/CanDoItAll.Tests.Playwright.slnx` | `FullyQualifiedName~SharedProviderManagementPlaywrightTests` | 10 | One focused browser lane for real central/client workflows and screenshots. |

Before running a test topic:

1. build the owning production/test assembly;
2. run `--list-tests` when it is a .NET test lane;
3. compare actual discovery with the planned count;
4. update the planned count only before execution and with a written implementation-based
   reason;
5. reject zero discovery;
6. record transcript and counts in `proof/proof-manifest.json`.

Do not run an unfiltered project or broader lane unless this subbundle explicitly owns it.

## Acceptance criteria

- All UI acceptance criteria pass.
- Normal and open-overlay screenshots are inspected.
- No secret rendered or captured.
- Existing local provider workflow remains green.
- UI gate is PASS.

## Negative proof

- Unauthorized/offline/identity mismatch/conflict states are visible and actionable.
- Remote fields cannot be changed via UI.
- Overlay cannot lose keyboard focus or hide primary close action.
- No nested unintended page scroll owners.

## Semantic invariants

- Playwright proof is focused and deterministic.
- Screenshots are inspected, not count-only.
- No UI artifact contains credentials.

## Evidence artifacts

At minimum:

- completed `proof/proof-manifest.json`;
- command transcripts under `proof/transcripts/`;
- changed-file inventory;
- architecture/reference artifacts;
- focused behavior artifacts;
- completed `SESSION-HANDOFF.md`;
- updated root `STATUS.md` and traceability rows.

## Progression gate

Pass only when every acceptance criterion, architecture assertion, focused build/test, and
negative proof is backed by an artifact. On pass mark this subbundle `DONE`, unlock only
`SB10`, and update the owning review.

On failure, keep downstream work locked. Do not call a missing proof a residual risk.

## Reopen triggers

- Backend contract changes.
- Any required screenshot state is absent.
- Playwright uses flaky timing or wrong viewport.
- Secret appears in browser artifacts.

## Execution checklist

- [ ] Current branch/commit/worktree captured.
- [ ] Mandatory skills loaded.
- [ ] Bundle and subbundle readiness validated.
- [ ] Dependencies are `DONE`.
- [ ] Before architecture/reference evidence captured.
- [ ] Scope implemented without widening.
- [ ] Affected production projects built.
- [ ] Test discovery recorded and nonzero.
- [ ] Focused positive/negative tests passed.
- [ ] Security/redaction checks passed where applicable.
- [ ] After architecture/reference evidence captured.
- [ ] Proof manifest completed with artifact hashes.
- [ ] Session handoff completed.
- [ ] Status/traceability/review updated.
