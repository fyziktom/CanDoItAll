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

        - [ ] Two store instances racing one revision admit exactly one winner.
- [ ] Concurrent create admits one creator.
- [ ] Replace/delete race does not corrupt storage.
- [ ] Injected failure/cancellation leaves no temp file.
- [ ] Existing round-trip/corruption tests remain green.

        ## Proof requirements

        Create `proof/proof-manifest.json` and `SESSION-HANDOFF.md`. Record starting/ending SHA, changed
        files, commands, exit codes, test counts, architecture checks, bugs found, deviations, residual
        risk, and whether the next subbundle is unlocked.
