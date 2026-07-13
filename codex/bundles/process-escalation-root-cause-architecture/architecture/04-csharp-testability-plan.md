# C# Testability Plan

## Characterization Tests

- Prove current blocked projection does not expose enough diagnostic detail for a `NeedsManager` blocked result.
- Prove current result receipt storage cannot reconstruct the actionable blocked reason without external state.
- Prove current capability scope can require receipts but cannot fully express readiness across tools, MCPs, skills, suppressions, and allowed operations.
- Prove a management-only step can be assigned to an agent that globally has development skills without a step-level suppression contract.

## Isolated Unit Tests

- Diagnostic normalization: safe summary, category, retry safety, idempotency, capability/tool identifiers.
- Artifact/result lineage: produced artifact refs, missing artifact diagnostics, content hash/ref mapping.
- Readiness resolver: required runtime tool missing, runtime tool denied, MCP missing, MCP denied, skill missing, skill suppressed, allowed operation missing, browser tools disabled.
- Recovery classifier: generic missing artifact, denied capability, timeout, child-run blocked, provider failure, instruction non-compliance, unknown.
- Domain driver policy: .NET setup recovery can request scaffold repair without touching generic runtime.

## Negative Tests

- Generic runtime/dispatcher rejects or fails architecture test if `.NET`, `Blazor`, `Calculator`, `Tetris`, screenshot, or Playwright-specific rules appear in forbidden layers.
- A non-UI software step must not require Playwright or screenshot proof.
- A management-only step with development suppression must not receive development tools/skills in MAF context.
- A missing required capability must not be converted into `Completed`.
- Manager fallback must not retry without a recorded failure category and recovery decision.

## Integration And Composition Smoke Tests

- Launch preview reports step capability readiness and HR matching mismatches.
- Assignment persistence carries readiness contract and scope override into MAF execution metadata.
- MAF capability composer applies step suppressions and allowed operations.
- Process projection readback surfaces blocked diagnostic summaries and artifact lineage.
- Recovery classification is recorded when a blocked child run bubbles to a parent step.

## Fake Provider, Tool, Driver Proof

- Fake runtime tool catalog with present/missing/denied tools.
- Fake MCP catalog with present/missing server and tool.
- Fake skill catalog with global skill present but step-suppressed.
- Fake driver classifier for a non-software process to prove generic boundaries.
- Fake .NET driver classifier to prove domain behavior is isolated.

## End-To-End Proof

- Replay a simple .NET delivery run that reaches validation without unnecessary manager escalation.
- Replay a UI/browser proof step where Playwright/screenshot tools are required and available.
- Replay a management-only step where development skills are globally available but suppressed by step contract.
- Re-run process API readback and confirm blocked diagnostics are actionable if any step blocks.
