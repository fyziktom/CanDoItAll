# Cognitive Memory Probing Workbench Repair

This bundle repairs the gap between the original Cognitive Memory probing architecture and the current implementation. The target is an operator-usable Dialogue Workbench plus a governed feedback-to-repair path, validated against the loaded AI Tap/Faucet and Curacao Glass factory projects.

## Profile

- `initiative`

## Mission

- Make Cognitive Memory probing feel like a controlled maintenance conversation: users can ask random project questions, inspect what memory used, mark facts as correct or wrong, submit correction text, create review/regression artifacts, and approve repairs without allowing chat to bypass source truth or mutation authority.

## Outcome Contract

- Requested outcome: implement a minimum complete probing repair loop from UI chat turn through feedback, review-gated correction candidate, approved memory repair, and regression evidence.
- Hard constraints: no direct truth mutation from chat; source data remains in bundles/database; use existing Blazor component patterns; validate with AI Tap and Curacao Glass realistic project memories.
- Evidence required before closure: prepared and completed bundle validation, targeted tests, API smoke against PostgreSQL, browser proof for the workbench, and raw-note closure rows.
- Known blockers or explicit scope exceptions: generated Epistemic Drive question queues may remain a follow-up if free-dialogue probing and feedback repair are implemented and validated first.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report

## Recommended Execution Order

1. `subbundles/01-01-probing-feedback-repair-core`
2. `subbundles/02-02-dialogue-workbench-ui-and-validation`
3. Final closure audit and validator pass.

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Completed`
- Subbundle gate review: `01 completed, 02 completed`
- Final closure gate: `Passed`
- Browser validation analytics: `Completed`

## Final Evidence

- Backend repair path: `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~CognitiveMemoryAdvancedServicesTests|FullyQualifiedName~CognitiveMemoryReviewUiServiceTests" --no-restore -m:1` passed 11/11.
- Web build: `dotnet build C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj --no-restore -m:1` passed with 0 warnings and 0 errors.
- API smoke: `validation/evidence/api-smoke/api-probe-smoke-results.json` proves AI Tap and Curacao probes returned 48 sections, 96 included source refs, no missing required source-truth terms, and one AI Tap probe-feedback review was approved into memory record `0220c9c6-b1e0-4df7-9d4f-a956f4f9d478`.
- Browser proof: `validation/evidence/browser/probe-workbench-desktop.png` and `validation/evidence/browser/probe-workbench-mobile.png`.
- Root cause repaired during validation: probe turns persisted large source-grounded warning/metadata payloads into bounded PostgreSQL varchar fields; persistence is now explicitly truncated while the returned context pack remains rich.
