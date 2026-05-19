# Structured Input

## Goal

- Execute the Cognitive Memory P1 beta-hardening roadmap as a follow-up bundle.

## P1 Scope From Roadmap

- Version the HTTP API contract and add examples for common flows.
- Add live Qdrant/provider projection tests, provider-failure integration tests, and runbooks.
- Add retention/cleanup policy for traces, candidates, probe turns, distributed jobs, and related operational records.
- Add operator audit views for mutation commands, claim/evidence changes, and projection rebuild failures.
- Harden external source ingestion with clear size limits, extraction error details, and sensitive-content policy.
- Add performance baselines for large manifests and recall runs.
- Continue decomposing older broad services when the change is safe and source-compatible.

## Acceptance Bar

- The final state must be described honestly: if beta is not reached, docs must say why.
- Any environment-gated validation must include deterministic local proof plus an executable runbook.
- Browser proof is required if rendered Blazor operator UI changes.
