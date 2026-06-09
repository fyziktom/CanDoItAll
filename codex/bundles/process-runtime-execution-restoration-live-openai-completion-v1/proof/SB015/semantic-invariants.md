# SB015 Semantic Invariants

## Status
Completed.

## Invariant SB015_INV_001
- Invariant ID: `SB015_INV_001`
- Source raw note: "Review real code, not only bundle report" and "Determine real test outcome."
- Expected behavior: Process execution supports workflow-backed role dispatch and direct-agent/fake-provider dispatch through process-owned routing and finalization, with persisted workflow links, process metadata, provider/model metadata, artifacts, and execution outcomes.
- Disallowed shallow implementation: Proving only enum values, candidate creation, or mock agent catalog seeding without execution and finalization evidence.
- Failing-first test: `bundle://proof/SB015/red-team/route-enum-only-proof-rejection.txt` rejects route-enum/source-only proof.
- Passing test: Six focused integration tests passed in `bundle://proof/SB015/transcripts/maf-direct-agent-execution-tests.txt`.
- Changed source files: No production source changed in SB015. Current source hashes are captured in `bundle://proof/SB015/manifest.md`.
- Production assertions: `bundle://proof/SB015/transcripts/maf-direct-agent-source-assertions.txt` cites workflow-backed role, direct-agent candidate, fake provider execution metadata, process tool profile, and process-owned finalizer surfaces.
- Red-team negative case: `bundle://proof/SB015/red-team/route-enum-only-proof-rejection.txt`
- Downstream dependency check: SB016-SB018 may start because both workflow-backed and direct-agent/fake-provider execution paths have source-backed proof.

## Shallow-Pass Trap
A fake Gate E closure could list `DirectAgent` and `WorkflowBackedRole` route names. SB015 rejects that by requiring workflow dispatch with state mapping, mock-provider durable process execution, direct-agent candidate fact preservation, artifact handoff metadata, and process-owned finalizer routing.

## Semantic Positive Proof
- `bundle://proof/SB015/transcripts/maf-direct-agent-execution-tests.txt`
- `bundle://proof/SB015/transcripts/maf-direct-agent-source-assertions.txt`

## Adversarial Negative Proof
- `bundle://proof/SB015/red-team/route-enum-only-proof-rejection.txt`

## Anti-Stub Audit
- `bundle://proof/SB015/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Matches are documentation and negative test assertions, not an execution-capable process-driver runtime host, process-driver registry, selector, or production `NotImplemented` path.

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Workflow run link | `ProcessWorkflowRunCoordinator` | Run detail readback | Links workflow run state and artifacts to the process step | Waiting-approval test rejects assuming all workflow runs complete |
| Direct-agent candidate facts | `ProcessDispatchCandidateFactory` | Direct-agent route handler | Carries binding, recovery, and cooperation data into direct execution | Candidate/finalizer tests reject unbound route selection |
| Mock provider execution run | Process mock catalog and dispatch runtime | Execution run readback and artifact projection | Persists completed provider/model/process metadata for direct process execution | Tests assert skipped branch has no execution run |
