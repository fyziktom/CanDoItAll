# SB01 — Current-State Reconciliation And Executable Baseline

## Status

- `Ready`

## Objective

- Replace the stale WIP resume state with a truthful, current-commit execution baseline and prove that inherited backend foundations are reachable through the repository's current source graph and test lanes.

## Success Criteria

- Actual start commit, sibling dependency commits, dirty-state exclusions, and all drift after `a8e3f87e...` are recorded.
- The old package blocker is classified as superseded, not silently deleted.
- WIP SB00-SB13 claims are mapped to retained, reopened, pending, or deferred successor ownership.
- Current focused filters have recorded nonzero expected/actual discovery and inherited foundation tests pass or every failure has a downstream owner.
- CP0 architecture/current-state review passes.

## Covered Inputs

- BC-001 through BC-005.
- WIP status/proof/checksum contradictions and commits after `dea90cfd...`.
- Current `docs/testing.md`, CI source graph, and CodeAnalytics baseline.

## Prerequisites

- none.

## Exact Source References

- `repo://codex/bundles/Simple-Llm-Chats-Hardening-Sse/bundle-status.json`
- `repo://codex/bundles/Simple-Llm-Chats-Hardening-Sse/EXECUTION-PROGRESS.md`
- `repo://codex/bundles/Simple-Llm-Chats-Hardening-Sse/CHANGE-CONTROL.md`
- `repo://codex/bundles/Simple-Llm-Chats-Hardening-Sse/CODEX-EXECUTION-CONTRACT.md`
- `repo://codex/bundles/Simple-Llm-Chats-Hardening-Sse/CHECKSUMS.sha256`
- `repo://codex/bundles/Simple-Llm-Chats-Backend-Api`
- `repo://docs/testing.md`
- `repo://.github/workflows/ci.yml`
- `repo://tests/Solutions`

## UI Composition Contract

- N/A — no browser-visible or UI source is in scope.

## Deliverables

- `proof/SB01/manifest.md`, `semantic-invariants.md`, and portable transcripts for Git/source graph, discovery, focused baseline, and CP0.
- Updated execution ledger with retained/reopened/pending status for every predecessor work unit.
- Frozen expected discovery for the named current foundation cases.

## Dependency Impact

- Critical foundation for every later work unit. Unclassified drift or zero discovery invalidates the whole plan.

## Validation Depth

- Proof tier: `Governed`.
- Test projects: `repo://tests/Solutions/CanDoItAll.Tests.Unit.slnx` and `repo://tests/Solutions/CanDoItAll.Tests.Integration.slnx`.
- Filter: exact `FullyQualifiedName=` union of the eight existing named cases below; use the current Unit or Integration lane for its owning case.
- Selection reason: current namespaces/solutions replaced all predecessor discovery evidence.
- Expected named cases: `Definition_name_and_conversation_title_are_distinct`, `Application_services_have_explicit_dependencies_without_service_location`, `Real_profile_scope_resolves_backend_without_generic_file_store_or_workflow_registration`, `Same_id_and_request_replays_without_a_second_provider_dispatch`, `Profile_switch_after_first_read_rejects_authoritative_return`, `DefinitionApi_ExposesSanitizedPerModelThinkingEffortAndRejectsDuplicateJsonEffort`, `ConversationApi_UsesBoundedPageAndExposesPinnedDefinitionRevision`, and `Independent_stores_apply_one_cross_process_cas_winner`.
- Expected discovery: 8 total, exactly one per named case; list Unit and Integration unions separately and record their sum before execution.
- Invalidation keys: Git head, sibling commit, test solution, namespace/filter, DI registration, product project reference, WIP checksum/status.
- Broad-gate decision: not required here; required once at SB10 for the cross-cutting final union.

## Implementation Steps

1. Verify Git status and record the application/sibling source commits; exclude only known bundle-authoring changes.
2. Map every commit after the WIP candidate to source/test/build/proof owners.
3. Run the old bundle checksum verification and record mismatches as historical invalidation evidence.
4. Query the current project/reference/service-registration graph and compare it to the prepared architecture inventory.
5. Build the five current LLM Chat product projects directly in Release with source dependencies.
6. List current foundation tests and freeze exact discovery; then run the bounded selected classes.
7. Record inherited failures as failing-first inputs to SB02-SB09; no unexplained failure may pass CP0.
8. Produce the Governed manifest, hashes, semantic invariants, transcripts, and independent CP0 decision.

## C# Architecture Impact

- Inventory only; no product architecture change is allowed in SB01.

## Boundary Ownership

- Confirms current Core/Persistence/Composition/Web/ProviderRuntime ownership.

## Dependency Direction

- Must match `bundle://architecture/02-csharp-dependency-direction.md` with zero cycles.

## Pattern Decision

- Historical proof is input, never current closure evidence.

## Testability Contract

- Exact lane/filter discovery precedes execution; no stale count or `CanDoItAll.slnx` test command is accepted.

## Partial Class Policy

- No partial class/type may be introduced.

## Architecture Proof Required

- Project graph, service lifetime inventory, CodeAnalytics results, source assertions, and independent CP0 review.

## Scope Exceptions

- Do not fix product defects in this work unit; classify and assign them.

## Do Not Do

- Do not edit the predecessor bundle to make checksums/status green.
- Do not reuse old pass counts, package-mode commands, or proof heads.
- Do not run the broad Stable aggregate.

## Acceptance Checklist

- [ ] Actual source/dependency commits and worktree exclusions recorded.
- [ ] Every predecessor SB classified.
- [ ] Current discovery and focused baseline recorded.
- [ ] No unexplained failure.
- [ ] CP0 Governed proof passes.

## Proof Required

- Exact commands, working directory, timestamps/run labels, expected/actual discovery, result summaries, changed-file hashes, semantic invariants, and CP0 review under `proof/SB01`.

## Browser Validation Logging

- N/A — backend/process-only proof.

## Progression Gate

- SB02/SB08 may begin only after CP0 passes and every baseline failure has a named owner.

## Reopen Triggers

- Any source/test/build/CI change before SB09 reopens the affected SB01 inventory/discovery row and downstream checkpoint.

## Suggested Agent Prompt

```text
Execute SB01 only. Reconcile current state and capture Governed baseline proof; do not repair product code. Stop on unclassified drift, zero/unexpected discovery, or an unexplained failure.
```
