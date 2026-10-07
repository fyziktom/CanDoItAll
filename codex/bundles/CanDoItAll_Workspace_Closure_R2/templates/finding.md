# Finding ID — descriptive title

Status: OPEN. Severity: to assess. Attribution: unproven until evidenced.

## Observable symptom and exact reproduction

Record current source pair, fixture/profile/circuit/run/operation identity, inputs and ordered
steps. Describe the earliest failing invariant. Separate expected from observed results.

## Ownership and call graph

Name the initiating renderer, session, adapter, actual owner, admission/authority, persisted
identity, acknowledgement and subsequent read/disposal steps. Include relevant exact paths.

## Timeline and retained effects

Record UTC and ordering barriers; distinguish rejected, admitted, committed, partial, unknown
and observed. A read result is not causal proof of a previous unacknowledged operation.

## Evidence and attribution

Link sanitized original attempt and corrected controls with hashes. State missing raw data.
Compare pre/post source only in isolated worktrees without changing the user's checkout.
Differentiate product regression, legacy behavior, harness fault, environment and provider.

## Small repair or required separate design

Explain affected modules, contracts, tests, performance/lifetime risk and why a bounded repair
is or is not appropriate. Map a complex problem into concrete follow-up slices with entry
conditions and final proof. Do not weaken current guards as a workaround.

## Disposition

Exact correction commits, repeatable controls, affected whole-application tests, remaining
blocks and whether this finding prevents next-module readiness. Mapping alone leaves it open.
