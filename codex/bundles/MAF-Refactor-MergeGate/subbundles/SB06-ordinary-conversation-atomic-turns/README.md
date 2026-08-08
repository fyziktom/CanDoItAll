# SB06 — Ordinary conversation atomic turns

        **Depends on:** SB05  
        **Required before merge:** Yes

        ## Goal

        Make provider adoption, transcript, acceleration, and active-turn state one recoverable transaction.

        ## Required work

        1. Persist pre-turn provider and acceleration compensation data in ActiveTurn when Adopt changes them.
2. Restore pre-turn provider and acceleration on provider failure, cancellation, explicit abandonment, and crash recovery.
3. Reject RenameAsync while ActiveTurn exists.
4. Reserve capacity for both user and assistant entries before provider invocation.
5. Validate unique entry ids and exact ActiveTurn user entry/turn identity.
6. Keep revision monotonic during rollback.
7. Do not add ordinary-chat UI, API, streaming, summarization, or branches.

        ## Primary files

        - `src/MAF/Common/CanDoItAll.AgentFramework.Llm.Abstractions/LlmConversationContracts.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Llm.Conversations/LlmConversationService.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Llm.Conversations/FileLlmConversationStore.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/LlmConversationServiceTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/FileLlmConversationStoreTests.cs`

        ## Acceptance

        - [x] Failed or abandoned Adopt restores the original provider and acceleration.
- [x] Successful Adopt remains unchanged.
- [x] Rename during active turn fails typed without changing state.
- [x] Near-capacity turn fails before ILlmInvocationPort is called.
- [x] Corrupted ActiveTurn metadata fails typed on load.
- [x] No ordinary failure leaves an orphaned active turn.

        ## Proof requirements

        Create `proof/proof-manifest.json` and `SESSION-HANDOFF.md`. Record starting/ending SHA, changed
        files, commands, exit codes, test counts, architecture checks, bugs found, deviations, residual
        risk, and whether the next subbundle is unlocked.

## Execution contract

- **Owned findings:** MRG-006, MRG-007, and MRG-008.
- **Proof tier:** Governed.
- **Progression gate:** SB07 unlocks only after failure/cancel/abandon/recovery compensation, active-turn mutation guards, identity validation, and pre-provider capacity admission pass.
- **Reopen trigger:** Any ordinary failure leaves turn-owned state, rename mutates an active turn, or the provider is invoked without two available transcript slots.

## C# Architecture Impact

Make turn-owned state an explicit durable transaction/compensation boundary without adding product features.

## Boundary Ownership

Llm.Abstractions owns bounded durable state contracts; Llm.Conversations owns orchestration, validation, compensation, and persistence mapping.

## Dependency Direction

LLM projects remain agent/workspace/process/MAF free and depend inward on LLM contracts and Models.

## Pattern Decision

Use explicit durable compensation state and one compensation constructor/path; reject rollback reconstructed from admitted state.

## Testability Contract

Direct service/store tests cover semantic positive turns and adversarial failure, cancellation, abandonment, crash recovery, rename, capacity, and corruption.

## Partial Class Policy

Keep the service cohesive without partials, nested state managers, or duplicated compensation branches.

## Architecture Proof Required

Governed invariant matrix, failing-first/passing transcripts, persistence round-trip assertions, anti-stub audit, and downstream successful-turn smoke.

## Gate result

- **Status:** Complete
- **Decision:** Pass
- **Evidence:** `proof-manifest.json`, `SESSION-HANDOFF.md`, and `../../proof/SB06`
- **Next subbundle:** SB07 unlocked
