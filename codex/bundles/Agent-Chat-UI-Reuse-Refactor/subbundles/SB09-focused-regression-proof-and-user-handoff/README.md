# SB09 — Focused regression proof and user handoff

## Status

`pending`

## Proof tier

`Governed`

## Dependency

- Depends on: SB08
- Closure checkpoint: CP5
- Owned requirements: UIR-002, UIR-003, UIR-005, UIR-070, UIR-071, UIR-072, UIR-073, UIR-074, UIR-075, UIR-076, UIR-077, UIR-078, UIR-079, UIR-080, UIR-081, UIR-082

## Objective

Run the final affected-scope and browser proof once, prepare the manual agent-chat regression checklist, and stop in an explicit awaiting-user-verification state.

## Success criteria

The subbundle creates one coherent, independently provable outcome, preserves existing Agent behavior, and does not perform work owned by a later subbundle.

## Scope

- Freeze the final Phase 1 diff and run final affected-scope analysis.
- Run focused project builds, required test selectors, source/dependency guards, and one final large-desktop browser regression pass.
- Run a broad Stable/full gate only if explicitly triggered.
- Prepare the manual Agent Chat checklist and leave the bundle awaiting user verification.

## Exact source anchors

Read these exact files plus nearby CSS, tests, project files, and every live reference found through CodeAnalytics:

- `all production and test files changed by SB02–SB08`
- `proof artifacts from CP0–CP4`
- `reviews/user-regression-handoff.md`
- `bundle-status.json`
- `EXECUTION-PROGRESS.md`

## Required deliverables

- `proof/SB09/final-changed-files-and-ranges.json`
- `proof/SB09/final-impacted-tests-request.json`
- `proof/SB09/final-impacted-tests-response.json`
- `proof/SB09/final-test-execution.json`
- `proof/SB09/final-build-execution.json`
- `proof/SB09/final-source-and-dependency-guards.txt`
- `proof/SB09/final-ui-parity.md`
- `proof/SB09/broad-gate-decision.md`
- `proof/SB09/user-regression-handoff.md`
- `proof/SB09/manifest.json`
- final status awaiting-user-agent-chat-regression

## Entry gate

1. Read the root bundle contract, architecture, requirements, traceability, and proof from prerequisites.
2. Load current SharedInfo skills and record hashes.
3. Verify the live source and tests still match this responsibility boundary.
4. Run the current subbundle validator at entry.
5. Stop for repair when prerequisites are missing, stale, contradicted, or source drift changes ownership.

## Implementation sequence

1. Confirm CP0–CP4 are closed and no prerequisite evidence is stale.
2. Freeze the aggregate Phase 1 diff from the execution base to final candidate head.
3. Call impacted-test analysis with actual changed paths/ranges and all relevant workspaces.
4. Run every required selector and verify expected nonzero discovery.
5. Resolve conditional selectors and promotion triggers.
6. Run focused affected project builds.
7. Run source, phase exclusion, dependency, cycle, and architecture guards.
8. Run one final focused large-desktop browser pass across the highest-risk Agent flows and inspect evidence.
9. Decide whether a Stable/full gate is triggered. If yes, record the exact trigger and run it once. If no, record why affected-scope proof is sufficient.
10. Complete the exact route/setup manual checklist.
11. Update status and progress to awaiting-user-agent-chat-regression.
12. Stop. Do not create or execute the Simple Chat UI phase.

## Architecture and dependency gate

- Use the narrowest healthy CodeAnalytics snapshot for architecture-relevant changes.
- Keep source-neutral presentation free of AgentFramework, LlmChats, backend, persistence, and runtime dependencies.
- Reject cycles, wrong project-reference direction, service location, partial-class growth, facade-only extraction, and boolean-god components.
- Record what the old owner no longer owns after this subbundle.
- Run `csharp-architecture-review-gate` when this subbundle closes a named architecture checkpoint.

## Impacted-test protocol

For every production change in this subbundle:

1. derive actual diff files and one-based changed line ranges;
2. call `code_analytics_impacted_tests_get` with `behaviorIntent=Unknown`;
3. put inspected-only files in `contextOnlyPaths`;
4. verify healthy workspaces, resolved symbols, and nonzero source/test discovery;
5. run every required selector;
6. promote conditional selectors only when a returned trigger occurs;
7. use `BehaviorPreservingImplementation` only after conservative analysis justifies it;
8. record request, response, selectors, discovery counts, commands, results, containment, and promotion decisions.

## Focused test intent

- Aggregate impacted-test query with healthy workspaces and resolved changed symbols.
- Every required selector with expected nonzero discovery.
- Focused Agent component and browser proof.
- One broad Stable/full gate only when explicitly triggered.
- No second broad rerun to chase unrelated failures without classification and a concrete reopen decision.

## Browser/UI proof

- Mandatory final focused desktop pass.
- Cover catalog/switch, session rail, send/response, one approval/execution state, one attachment or prompt action, floating open/hide/reopen/stop, settings identity/provider/model/save, and one contextual or Process consumer.
- Voice may be manual when environment-dependent, but deterministic component callback/state proof remains required.

## Source and phase guards

- Run `scripts/check_repo_boundaries.py` against the repository and this subbundle base SHA.
- Inspect the actual diff for false negatives and semantic violations.
- No production UI may consume `Modules.LlmChats`.
- No Simple Chat catalog/filter/route/context/API/SSE feature may appear.
- No new partial file may expand the named large Agent UI types.
- No neutral source may use backend services, EF, persistence, or service location.

## Acceptance checklist

- [ ] All checkpoints pass.
- [ ] Every required selector ran with expected nonzero discovery.
- [ ] Affected builds and guards pass.
- [ ] Final browser proof is inspected.
- [ ] Broad gate decision is explicit and compliant.
- [ ] Manual checklist is complete.
- [ ] No phase exclusion was violated.
- [ ] Status is awaiting-user-agent-chat-regression.
- [ ] Simple Chat UI remains blocked pending user approval.

## Do not do

- Do not mark ready-for-simple-chat-ui.
- Do not begin a new bundle automatically.
- Do not run broad tests by habit.
- Do not hide unrelated failures; classify them and record whether they block or reopen.

## Proof manifest

Update `proof-manifest.json` in this subbundle and create the referenced repository proof artifacts. A path without meaningful evidence is not proof.

## Progression

- Complete only when every owned acceptance item and required proof passes.
- Reopen earlier work when later evidence invalidates it.
- When checkpoint `CP5` applies, record pass/reopen/repair/block before continuing.
- Do not start a later subbundle automatically when the gate is blocked.
