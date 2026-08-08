# SB05 — File conversation cross-instance CAS

        **Depends on:** SB04  
        **Required before merge:** Yes

        ## Goal

        Make file-store compare-and-swap true across all scoped instances sharing a root.

        ## Required work

        1. Introduce a process-wide canonical-path keyed coordinator or equivalently tested lock-file design.
2. Protect Create, Replace, and Delete read-check-write sequences across separate instances.
3. Use reference-counted cleanup or another bounded lock lifecycle.
4. Preserve atomic replacement and remove temporary files in finally blocks.
5. Document whether guarantees are process-wide or cross-process and do not overclaim.

        ## Primary files

        - `src/MAF/Common/CanDoItAll.AgentFramework.Llm.Conversations/FileLlmConversationStore.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/FileLlmConversationStoreTests.cs`

        ## Acceptance

        - [x] Two store instances racing one revision admit exactly one winner.
- [x] Concurrent create admits one creator.
- [x] Replace/delete race does not corrupt storage.
- [x] Injected failure/cancellation leaves no temp file.
- [x] Existing round-trip/corruption tests remain green.

        ## Proof requirements

        Create `proof/proof-manifest.json` and `SESSION-HANDOFF.md`. Record starting/ending SHA, changed
        files, commands, exit codes, test counts, architecture checks, bugs found, deviations, residual
        risk, and whether the next subbundle is unlocked.

## Execution contract

- **Owned finding:** MRG-005.
- **Proof tier:** Governed.
- **Progression gate:** SB06 unlocks only after independent-instance create/replace/delete races and fault/cancellation temp hygiene pass.
- **Reopen trigger:** Proof uses one store instance, coordination is unbounded, storage corruption appears, or the implementation claims untested cross-process safety.

## C# Architecture Impact

Create a real shared-resource concurrency boundary for file conversation persistence.

## Boundary Ownership

Llm.Conversations owns the file-store coordinator; Abstractions remains persistence-implementation-free.

## Dependency Direction

The implementation continues to depend only on LLM contracts/Models and BCL primitives; no app/module dependency is allowed.

## Pattern Decision

Use canonical-path keyed bounded coordination; reject instance-local semaphores and global unbounded dictionaries.

## Testability Contract

Construct independent store instances sharing a root and inject/force fault and cancellation paths that inspect durable and temp files.

## Partial Class Policy

Prefer a top-level cohesive coordinator when extraction is needed; no nested manager or new partial store.

## Architecture Proof Required

Governed race transcripts, changed-file hashes, temp-file source assertions, guarantee-scope statement, and direct coordinator/store tests.

## Gate result

- **Status:** Complete
- **Decision:** Pass
- **Evidence:** `proof-manifest.json`, `SESSION-HANDOFF.md`, and `../../proof/SB05`
- **Next subbundle:** SB06 unlocked
