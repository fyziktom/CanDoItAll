# SB01 Proof Debt Audit

## Entry Gate

- Result: Passed.
- Basis: prepared-stage bundle validation passed, SB01 has no production-code prerequisite, and SB01 is explicitly an audit-only phase.

## Local Source Availability

| Source | Local result | Closure impact |
| --- | --- | --- |
| Current hardening bundle | Present | Used as the executable source of truth. |
| `process-run-output-manager-artifact-tuning-v1` bundle | Not present under `codex/bundles` | Treat claims from reviewed-state as secondary notes, not artifact-backed closure proof. |
| Prior MAF/process final preflight bundle | Not present under `codex/bundles` | Prior blockers remain open until covered by current source/tests or later subbundle proof. |

## Proof Debt Classification

| Debt item | Classification | Owner | Current evidence |
| --- | --- | --- | --- |
| Broad runtime integration proof timed out | Open | SB08, SB15, SB18 | Current bundle requires named proof slices and timeout-proof harness. |
| Session/stream-error proof blocked | Open | SB08 | Current source has MAF/runtime tests, but no prior local preflight artifact is present. |
| Tool approval/MCP policy proof blocked | Open | SB08, SB10 | `AgentToolInvocationPolicyTests` and capability filtering tests exist; execution must run focused slices. |
| A2A/handoff/workflow proof blocked | Open | SB08 | A2A and MAF runtime tests exist; execution must run named slices. |
| Trace correlation proof blocked | Open | SB08, SB15 | Needs named MAF/runtime proof transcript. |
| Dedupe/hash race proof blocked | Open | SB04 | `ProcessArtifactIdentityService` exists; race/recovery behavior needs SB04 proof. |
| Manager recovery/operator approval proof blocked | Open | SB07, SB13 | Manager chat/resolver exists; reason/confidence and UI proof remain SB07/SB13 work. |
| Seeded invalid-artifact live browser proof unavailable | Open | SB03, SB13, SB18 | Operator read-model tests cover invalid statuses; browser proof is still required if UI changes. |
| External output folder grounding missed project-structure target | Superseded by current focused tests, revalidated in SB05 | SB05 | Current grounding tests and dispatch code contain external target handling; SB05 must prove false-positive rejection. |
| Selected-run manager chat resolution needed assignment-first behavior | Partially closed, revalidated in SB07 | SB07 | Resolver has assigned-manager tests; SB07 must add explainable diagnostics if missing. |
| Project-structure projection produced noisy artifact folder nodes | Partially closed, revalidated in SB06 | SB06 | Current projection source exists; SB06 must prove explicit policy and no child noise. |

## Classification Summary

- Closed now: none; SB01 does not close runtime behavior by itself.
- Partially closed or superseded by current source/tests: targeted output grounding, selected-run manager resolution, and projection folder collapse.
- Open and assigned: broad integration timeout, MAF slices, hash/race recovery, manager recovery, seeded invalid-artifact browser proof, and final red-team closure.
