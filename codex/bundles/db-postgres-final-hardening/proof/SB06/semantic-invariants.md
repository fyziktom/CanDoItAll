# SB06 semantic invariants

## Invariant protected

Process dispatch should claim cheaply before loading full candidate/run details.

## Producer/consumer lifecycle

Candidate headers are produced by the initial query. Claim ownership is produced by `TryClaimStepDispatchAsync`. Full candidate loading consumes only successful claims.

## Positive proof

Source context shows `LoadDispatchCandidateHeadersAsync`, then `TryClaimStepDispatchAsync`, then `LoadDispatchCandidateAsync`.

## Adversarial negative proof

The audit looked for the inverse ordering and captured the claim-first context.

## Anti-stub proof

Proof cites concrete source context rather than test-only placeholders.
