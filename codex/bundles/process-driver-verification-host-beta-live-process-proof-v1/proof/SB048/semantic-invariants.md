# SB048 Semantic Invariants

## SB048_INV_001 Host Denials Are Typed And Auditable
- Source raw note: SB046 requires host failure categories and reason codes.
- Expected behavior: every host denial carries a typed `ProcessVerificationHostFailureCategory`, typed `ProcessVerificationHostDenialCode`, message, audit record, process/step identity, requester identity, and mutation-denial flags.
- Disallowed shallow implementation: string-only denial text, exception-only denial flow, or success-only diagnostics proof.
- Positive proof: `bundle://proof/SB046/transcripts/host-failure-category-focused-tests.txt`.
- Source proof: `bundle://proof/SB046/transcripts/host-failure-category-source-assertions.txt`.
- Red-team negative case: `bundle://proof/SB048/transcripts/red-team-observability-shallow-proof-rejection.txt`.

## SB048_INV_002 Operator Readback Projects Denials Without Mutation
- Source raw note: SB047 requires operator troubleshooting/readback tests.
- Expected behavior: manager readback projects denied verification attempts with denial category, denial code, message, audit record id, audit counts, observation hash, zero diagnostics, zero responses, and no process/transition/finalizer mutation permissions.
- Disallowed shallow implementation: readback that only covers success, hides denial details, drops audit evidence, or reports diagnostics as approval.
- Positive proof: `bundle://proof/SB047/transcripts/operator-troubleshooting-readback-focused-tests.txt`.
- Boundary scan: `bundle://proof/SB048/transcripts/gate-p-observability-boundary-source-scan.txt`.

## SB048_INV_003 Observability Does Not Introduce Runtime Hooks
- Expected behavior: adding categories/readback fields does not add generic process-driver host, registry, selector, DI hook, manager command, endpoint mapping, or mutation-allowed flags.
- Disallowed shallow implementation: observability implemented through hidden runtime registration, fallback selectors, or mutation-capable driver hooks.
- Positive proof: `bundle://proof/SB048/transcripts/gate-p-observability-focused-tests.txt`.
- Anti-stub audit: `bundle://proof/SB048/transcripts/gate-p-observability-anti-stub-audit.txt`.
- Downstream dependency check: security, release-candidate, operator-smoke, and final closure phases must preserve typed denial/readback evidence.

## Production Behavior Artifact Matrix
| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Host denial taxonomy | `ProcessVerificationHostDenialClassifier` | Host denial focused test | SB046 transcript | Red-team rejects string-only denial text |
| Manager denial readback | `ProcessManagerReadOnlyVerificationReadbackDto` | SB047 readback test | Gate P focused transcript | Red-team rejects success-only diagnostics proof |
| Observability no-mutation boundary | Host/readback source scan | Manager readback DTO exposes false mutation permissions | Gate P proof index | Boundary scan rejects runtime hooks and mutation-allowed flags |

## Gate Result
Gate P is semantically adequate for observability. Host failures are typed and auditable, operator readback exposes denial troubleshooting evidence, and no runtime hook or mutation permission was introduced.
