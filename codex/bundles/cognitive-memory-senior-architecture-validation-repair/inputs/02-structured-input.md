# Structured Input

## Objectives

- Verify that the closed v2 architecture and LB4U follow-up bundles are structurally valid and backed by current code evidence.
- Audit current Cognitive Memory code for C# architecture, EF Core query shape, allocation-prone patterns, and maintainability risks.
- Repair concrete defects or performance risks that are small enough to fix safely in this pass.
- Validate Cognitive Memory API behavior with a focused truth-source probe and document memory quality.
- Leave a completed bundle with raw-note closure and proof.

## Hard Constraints

- Preserve original invariants: raw source provenance is truth, canonical memory changes are governed, Qdrant is a rebuildable projection, and probes do not directly mutate truth.
- Do not read or ingest secret/router password files.
- Do not hide provider, projection, or model errors behind silent fallback behavior.
- Keep fixes small and behavior-focused.

## Working Assumptions

- The previous LB4U bundle's live OpenAI/Ollama proof is accepted as historical evidence only; this pass must still run fresh local validation where feasible.
- No UI route or markup is intentionally changed by this repair.
- Live Cognitive Memory API validation can use a small local source corpus if the full LB4U environment or providers are unavailable.
