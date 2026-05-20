# Assumptions And Risks

## Assumptions

- The trusted curator mode is operator-only and can use `CognitiveMemoryActorKind.User` with actor id `cognitive-memory-operator-ui`.
- Manual approval bypass is acceptable only inside the curator service and must be visible in metadata, mutation audit, and UI.
- Direct LLM mode can use the configured default provider profile and provider diagnostics chat API.
- Agent mode can use the configured default agent or an explicitly selected agent and suppress tool approval requirements for the curator run.

## Risks

- A direct LLM call can answer from the recalled context, but it does not automatically expose the exact model reasoning. The implementation must preserve the recall trace and context refs it supplied.
- Automatic correction detection can misclassify ordinary discussion as memory update. The service must use explicit action heuristics and allow visible captured-item review in the same panel.
- Applying trusted human input immediately can corrupt memory if the operator misspeaks. Metadata must mark actor, mode, confidence, and source so dreaming can re-evaluate later.

## Critical Path Risks

- If curator turns do not store recall trace ids and included memory record ids, later corrections cannot target the wrong memory that produced the bad answer.
- If correction capture stays review-gated, the feature fails the user's explicit "skip manual confirmations/approvals" requirement.
- If direct LLM and agent modes share no common result contract, the UI will drift and downstream memory capture will become mode-specific.

## Validation Risks

- Real voice provider validation may fail without configured credentials or microphone access.
- Browser proof must verify the rendered Curator tab and controls, not only compile tests.
- Unit tests must cover the trusted capture path because UI tests alone cannot prove memory artifacts were written correctly.

## Reopen Triggers

- Reopen subbundle 01 if any downstream phase cannot identify affected memory ids for a correction.
- Reopen subbundle 02 if direct mode or agent mode bypasses the shared capture contract.
- Reopen subbundle 03 if voice controls cannot trigger the same send/capture path as text.
- Reopen subbundle 04 if tests pass but browser proof cannot show the Curator tab or captured improvement state.
