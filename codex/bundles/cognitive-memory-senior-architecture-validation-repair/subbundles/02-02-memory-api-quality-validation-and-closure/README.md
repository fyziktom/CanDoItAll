# 02 Memory API Quality Validation And Closure

## Status

- `Completed`

## Objective

Validate the repaired Cognitive Memory implementation through tests and HTTP API behavior, then close the bundle with honest residual risks.

## Success Criteria

- Cognitive Memory unit/integration/component validation passes or exact failures are recorded.
- Web app API status is checked through `/api/access/status` and `/api/cognitive-memory/status`.
- Recall or probe output is assessed against source truth for usefulness, source backing, and noise.
- No secret/router-password content appears in validation output.
- Default snapshot API output does not include resolved/rejected review history unless explicitly requested.
- English recall terms activate Czech LB4U source-backed pricing and certification facts.
- Contact lines are redacted from rendered recall context.
- This bundle passes prepared and completed validation.

## Covered Inputs

- SR-020, SR-021, SR-022, SR-030, SR-031.

## Prerequisites

- `01-01-query-shape-and-architecture-repairs` closure gate passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.cs`
- `C:\repositories\CanDoItAll\docs\cognitive-memory-api.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Settings\CognitiveMemoryStagedSourceManifest.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Settings\CognitiveMemoryExternalSourceTextExtractor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Consolidation\CognitiveMemoryConsolidationFactExtractor.cs`
- `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-followup-lb4u-validation-refactor\reviews\01-execution-report.md`

## Deliverables

- Fresh validation commands and outcomes in `reviews/01-execution-report.md`.
- API status/recall evidence or precise environment blocker.
- Memory quality assessment covering source sufficiency, noise, and secret exclusion.
- Code repairs for snapshot review-history noise, bilingual recall activation, contact redaction, and graph-expansion ranking pressure.
- Completed raw-note closure table.

## Dependency Impact

- This is the final closure phase.
- Weak proof here blocks declaring the previous implementation fully validated by this senior pass.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Run broader Cognitive Memory tests after targeted repair tests.
2. Start or reach the web app.
3. Check access and Cognitive Memory status endpoints.
4. Run focused recall/probe validation when the API is reachable.
5. Compare response context with source truth and inspect for noise or secret content.
6. Run prepared and completed bundle validators.
7. Update root status, execution report, and raw-note closure.

## Scope Exceptions

- If a live provider, PostgreSQL profile, or local web startup is unavailable, record the exact blocker and still complete code/test validation.
- Do not re-run the entire historical LB4U OpenAI/Ollama cycle unless the local environment is already configured for it.

## Do Not Do

- Do not ingest excluded secret/router-password files.
- Do not mutate canonical truth directly from probes.
- Do not treat generated summaries as raw truth.
- Do not mark API quality solved if status/recall proof is missing.

## Acceptance Checklist

- [x] Broader Cognitive Memory tests pass.
- [x] API status checked.
- [x] Recall/probe quality assessed or blocker recorded.
- [x] Bundle validators pass for prepared and completed stages.
- [x] Raw notes closed.

## Proof Required

- Targeted unit tests from subbundle 01.
- Broader Cognitive Memory unit/integration/component tests where feasible.
- `Invoke-RestMethod` evidence for `/api/access/status` and `/api/cognitive-memory/status`.
- Recall/probe response summary with source references and noise assessment when API state permits.
- `validate_bundle.py <bundle> --stage prepared`
- `validate_bundle.py <bundle> --stage completed`

## Browser Validation Logging

- N/A - no browser-visible UI or markup changed.

## Progression Gate

- The bundle can close only when tests, API validation or blocker diagnostics, raw-note closure, and completed-stage bundle validation agree.

## Suggested Agent Prompt

```text
Validate this bundle's repaired Cognitive Memory implementation through tests and the HTTP API. Compare memory answers with source truth, inspect for noise and secret leakage, update execution evidence, and do not pass closure if proof is missing.
```
