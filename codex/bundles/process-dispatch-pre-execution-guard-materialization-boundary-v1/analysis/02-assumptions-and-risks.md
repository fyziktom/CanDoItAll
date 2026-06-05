# Assumptions And Risks

## Assumptions

- The branch compiles before this bundle starts.
- Existing focused dispatch/candidate tests are still available.
- Upstream artifact materialization behavior is already covered by at least some integration tests, but Codex must inventory exact test names before production movement.
- Process Core remains out of scope.
- Driver production API remains out of scope.

## Critical Path Risks

1. **Hidden side effects inside pure-looking helpers**
   - Mitigation: split decision helpers from coordinators that write journal, transition status, or rerun upstream steps.

2. **Changed materialization fingerprint**
   - Mitigation: add failing-first/parity tests for the current fingerprint output.

3. **Changed duplicate journal behavior**
   - Mitigation: test existing fingerprint returns no duplicate request.

4. **Changed downstream block transition fields**
   - Mitigation: assert `BlockCause`, `SuppressAutomationDispatch`, `DecidedBy`, reason, and status fields.

5. **Changed rerun request directive**
   - Mitigation: snapshot exact directive text shape and artifact-title aggregation.

6. **Premature driver abstraction**
   - Mitigation: driver readiness stays documentation-only.

## Validation Risks

- A build-only pass is insufficient.
- A helper-exists-only test is insufficient.
- Tests must prove both no-materialization-target and runnable-target branches.

## Reopen Triggers

Reopen earlier subbundles if:

- `TryRequestMissingUpstreamArtifactMaterializationAsync` changes return semantics.
- `ProcessJournalEntry` details/correlation id drift.
- `ProcessAgentStepRerunRequest` fields change.
- `Dispatch.cs` still contains fingerprint, block reason, directive, and journal construction after migration.
- Any new production `ProcessCore`, `ProcessDriver`, `DriverPack`, or `IProcessDriverPack` appears.
