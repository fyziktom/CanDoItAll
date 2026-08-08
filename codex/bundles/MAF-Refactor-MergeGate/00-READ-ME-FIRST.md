# CanDoItAll MAF refactor — final merge-gate follow-up v3

**Repository:** `fyziktom/CanDoItAll`  
**Branch:** `maf-refactor`  
**Reviewed HEAD:** `79a6c0d7de353acfae3511e2671baf7daee2b498`  
**Executor:** Codex GPT-5.6 Sol, xHigh reasoning  
**Scope:** merge blockers and architectural closure only

## Mission

Close the remaining defects without reopening the completed MAF refactor. The narrow runtime ports,
dependency repair, process-recovery extraction, floating turn-context model, per-proposal approvals,
workspace-scope bundle, runtime-state envelope v2, lightweight LLM port, and ordinary-conversation
project separation are the accepted baseline.

## Mandatory rules

- Execute SB00–SB09 in order.
- Write failing characterization tests before production fixes.
- Stop at a failed gate.
- Keep source comments in English.
- Use CodeAnalysis MCP and the C#/.NET architecture skills.
- Do not weaken authority, approvals, process policy, workspace isolation, or tests.
- Do not add ordinary-chat product features.
- Do not reintroduce broad `IAgentRuntime` or product references into MAF.
- Produce proof manifests and session handoffs for every subbundle.

Start with `01-REVIEW-VERDICT.md`, `02-FINDINGS-REGISTER.md`, and `03-EXECUTION-ORDER.md`.

## Bundle compatibility map

This bundle predates the canonical CanDoItAll bundle scaffold but preserves the required semantic roles:

| Semantic role | Bundle surface |
|---|---|
| Source inputs and normalized requirements | `manifest.json`, `01-REVIEW-VERDICT.md`, `02-FINDINGS-REGISTER.md` |
| Current repository and architecture evidence | `architecture/00-verified-baseline.md`, `architecture/09-csharp-execution-guard.md` |
| Dependency and execution plan | `03-EXECUTION-ORDER.md`, `plan/architecture-checkpoints.md` |
| Work units | `subbundles/SB00-*` through `subbundles/SB09-*` |
| Traceability | `plan/traceability.md` |
| Status and proof | `05-EXECUTION-STATUS.md`, per-subbundle proof and handoff files |
| Closure | finding closure in `05-EXECUTION-STATUS.md`, then `reviews/FINAL-MERGE-DECISION.md` |

The bundle-specific structural validator remains authoritative for its external shape. The CanDoItAll
bundle validator is applied as a manual semantic readiness and closure gate.

## Proof-tier policy

- `Governed`: SB00, SB01, SB03, SB04, SB05, SB06, and SB09.
- `Behavioral`: SB02, SB07, and SB08.
- Every subbundle still produces the bundle-required `proof/proof-manifest.json` and
  `SESSION-HANDOFF.md`.
- Governed subbundles additionally produce the artifact-backed manifest, semantic invariants,
  transcripts, hashes, source assertions, and downstream or verifier proof required by the active
  bundle workflow skill.
