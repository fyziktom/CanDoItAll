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
