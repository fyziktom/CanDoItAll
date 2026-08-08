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

        - [ ] Failed or abandoned Adopt restores the original provider and acceleration.
- [ ] Successful Adopt remains unchanged.
- [ ] Rename during active turn fails typed without changing state.
- [ ] Near-capacity turn fails before ILlmInvocationPort is called.
- [ ] Corrupted ActiveTurn metadata fails typed on load.
- [ ] No ordinary failure leaves an orphaned active turn.

        ## Proof requirements

        Create `proof/proof-manifest.json` and `SESSION-HANDOFF.md`. Record starting/ending SHA, changed
        files, commands, exit codes, test counts, architecture checks, bugs found, deviations, residual
        risk, and whether the next subbundle is unlocked.
